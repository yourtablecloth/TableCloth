param(
    [string]$InstalledDirectory = (Join-Path $env:LOCALAPPDATA 'TableCloth\current'),
    [Parameter(Mandatory)][string]$ProbeDirectory,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedProductSha256
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [System.Text.UTF8Encoding]::new($false)

function Assert-LocalPath([string]$Path) {
    if (-not [System.IO.Path]::IsPathFullyQualified($Path)) { throw 'An absolute local path is required.' }
    $resolved = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($Path))
    if ($resolved.StartsWith('\\') -or $resolved.Substring(2).Contains(':')) { throw 'UNC paths and alternate streams are not accepted.' }
    for ($current = $resolved; $current; $current = [System.IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and
            ((Get-Item -LiteralPath $current -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Reparse points are not accepted: $current"
        }
    }
    return $resolved
}

function Assert-ChildPath([string]$Root, [string]$Path) {
    $resolved = Assert-LocalPath $Path
    $prefix = (Assert-LocalPath $Root) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Path is outside the intended directory.'
    }
    return $resolved
}

function Get-Sha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Write-Json([string]$Path, [object]$Value) {
    [System.IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 20), $utf8)
}

function Get-FileManifest([string]$Root) {
    $Root = Assert-LocalPath $Root
    $pending = [System.Collections.Generic.Stack[string]]::new()
    $pending.Push($Root)
    $entries = [System.Collections.Generic.List[object]]::new()
    while ($pending.Count -gt 0) {
        foreach ($item in Get-ChildItem -LiteralPath $pending.Pop() -Force) {
            $itemPath = Assert-ChildPath $Root $item.FullName
            if ($item.PSIsContainer) { $pending.Push($itemPath); continue }
            $entries.Add([pscustomobject]@{
                path = [System.IO.Path]::GetRelativePath($Root, $itemPath)
                size = $item.Length
                sha256 = Get-Sha256 $itemPath
            })
        }
    }
    return @($entries | Sort-Object path)
}

function Test-ManifestEqual([object[]]$Before, [object[]]$After) {
    if ($Before.Count -ne $After.Count) { return $false }
    for ($index = 0; $index -lt $Before.Count; $index++) {
        if ($Before[$index].path -cne $After[$index].path -or
            $Before[$index].size -ne $After[$index].size -or
            $Before[$index].sha256 -cne $After[$index].sha256) { return $false }
    }
    return $true
}

function Get-NativePeIdentity([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    $reader = [System.IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'The installed product is not a PE executable.' }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadUInt32()
        if ($peOffset -gt $stream.Length - 264) { throw 'The PE header offset is invalid.' }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw 'The PE signature is invalid.' }
        $machine = $reader.ReadUInt16()
        $stream.Position = $peOffset + 24
        $magic = $reader.ReadUInt16()
        $directoryOffset = switch ($magic) { 0x20B { 112 }; 0x10B { 96 }; default { throw 'The PE optional header is invalid.' } }
        $stream.Position = $peOffset + 24 + $directoryOffset + (14 * 8)
        $clrRva = $reader.ReadUInt32()
        $clrSize = $reader.ReadUInt32()
        if ($clrRva -ne 0 -or $clrSize -ne 0) { throw 'The installed product has a CLR header; a native payload is required.' }
        return [pscustomobject]@{ machine = ('0x{0:X4}' -f $machine); clrRva = $clrRva; clrSize = $clrSize; nativePe = $true }
    }
    finally { $reader.Dispose(); $stream.Dispose() }
}

function Invoke-Dotnet([string[]]$Arguments, [string]$Label) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($dotnetPath)
    $start.WorkingDirectory = $InstalledDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'The diagnostic process did not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        [System.IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Label.stdout.jsonl"), $stdout.GetAwaiter().GetResult(), $utf8)
        [System.IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Label.stderr.txt"), $stderr.GetAwaiter().GetResult(), $utf8)
        Write-Json (Join-Path $EvidenceDirectory "$Label.process.json") ([pscustomobject]@{
            executable = $dotnetPath; arguments = $Arguments; processId = $process.Id; exitCode = $process.ExitCode
        })
        return $process.ExitCode
    }
    finally { $process.Dispose() }
}

function Preserve-ProbeOutput {
    if (-not $ownsEvidenceRoot) { return }
    if (-not (Test-Path -LiteralPath $ownedEvidenceRoot)) { return }
    $source = Assert-ChildPath $InstalledDirectory $ownedEvidenceRoot
    foreach ($entry in Get-ChildItem -LiteralPath $source -Force) {
        if (-not $entry.PSIsContainer -or $entry.Name -cnotmatch '^\d{8}T\d{9}Z-[0-9a-f]{8}$') {
            throw 'The owned evidence root contains an unexpected entry; refusing automatic cleanup.'
        }
    }
    $destination = Assert-ChildPath $EvidenceDirectory (Join-Path $EvidenceDirectory 'probe-output')
    if (Test-Path -LiteralPath $destination) { throw 'The evidence archive already exists; refusing to overwrite it.' }
    $sourceManifest = @(Get-FileManifest $source)
    [System.IO.Directory]::CreateDirectory($destination) | Out-Null
    foreach ($entry in $sourceManifest) {
        $sourceFile = Assert-ChildPath $source (Join-Path $source $entry.path)
        $destinationFile = Assert-ChildPath $destination (Join-Path $destination $entry.path)
        [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destinationFile)) | Out-Null
        [System.IO.File]::Copy($sourceFile, $destinationFile, $false)
    }
    $archiveManifest = @(Get-FileManifest $destination)
    if (-not (Test-ManifestEqual $sourceManifest $archiveManifest)) { throw 'The copied probe evidence does not match its source.' }
    Write-Json (Join-Path $EvidenceDirectory 'probe-output.files.json') $archiveManifest
    $script:preservedSourceManifest = $sourceManifest
    $script:outputPreserved = $true
}

$InstalledDirectory = Assert-LocalPath $InstalledDirectory
$expectedInstallation = Assert-LocalPath (Join-Path $env:LOCALAPPDATA 'TableCloth\current')
if (-not $InstalledDirectory.Equals($expectedInstallation, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'InstalledDirectory must be the current per-user TableCloth installation.'
}
if (-not (Test-Path -LiteralPath $InstalledDirectory -PathType Container)) { throw 'The installed product directory does not exist.' }
$ProbeDirectory = Assert-LocalPath $ProbeDirectory
$repository = Assert-LocalPath (Join-Path $PSScriptRoot '..\..')
$EvidenceDirectory = Assert-ChildPath (Join-Path $repository 'Artifacts') $EvidenceDirectory
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'EvidenceDirectory must be a new workspace directory.' }
if (-not (Test-Path -LiteralPath $ProbeDirectory -PathType Container)) { throw 'The built probe directory does not exist.' }
$product = Assert-ChildPath $InstalledDirectory (Join-Path $InstalledDirectory 'TableCloth.exe')
$ExpectedProductSha256 = $ExpectedProductSha256.ToLowerInvariant()
if ((Get-Sha256 $product) -cne $ExpectedProductSha256) { throw 'The installed executable does not match the independently supplied product hash.' }
$pe = Get-NativePeIdentity $product
$dotnetPath = Assert-LocalPath (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$dataset = Assert-ChildPath $PSScriptRoot (Join-Path $PSScriptRoot 'calculator-cases.json')
$ownedEvidenceRoot = Assert-ChildPath $InstalledDirectory (Join-Path $InstalledDirectory 'evidence')
if (Test-Path -LiteralPath $ownedEvidenceRoot) { throw 'An existing installation evidence directory would prevent exclusive cleanup ownership.' }
$sidecarSources = @(Get-ChildItem -LiteralPath $ProbeDirectory -File -Filter '*.dll') + @(
    (Get-Item -LiteralPath (Join-Path $ProbeDirectory 'AllSkillsProbe.deps.json')),
    (Get-Item -LiteralPath (Join-Path $ProbeDirectory 'AllSkillsProbe.runtimeconfig.json'))
)
if (-not ($sidecarSources.Name -contains 'AllSkillsProbe.dll')) { throw 'The built AllSkillsProbe.dll was not found.' }
foreach ($source in $sidecarSources) { $null = Assert-ChildPath $ProbeDirectory $source.FullName }
$before = @(Get-FileManifest $InstalledDirectory)
[System.IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null
Write-Json (Join-Path $EvidenceDirectory 'installation.before.files.json') $before
Write-Json (Join-Path $EvidenceDirectory 'installed-product.json') ([pscustomobject]@{
    installedDirectory = $InstalledDirectory; executable = $product; expectedSha256 = $ExpectedProductSha256
    actualSha256 = Get-Sha256 $product; pe = $pe; productVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($product).ProductVersion
    parentIsManagedDiagnosticSidecar = $true; productExecutableCopiedOrReplaced = $false
    nativeAotProvenance = 'Supplied package/build hash; absence of a CLR header alone does not distinguish NativeAOT from a managed apphost.'
})
$createdSidecars = [System.Collections.Generic.List[object]]::new()
$problems = [System.Collections.Generic.List[string]]::new()
$probeExit = $null
$verifyExit = $null
$ownsEvidenceRoot = $false
$outputPreserved = $false
$preservedSourceManifest = @()
$originalFilesUnchanged = $false
$updateSafeDataRootVerified = $false
$completedEvidence = $null
try {
    New-Item -ItemType Directory -Path $ownedEvidenceRoot -ErrorAction Stop | Out-Null
    $ownsEvidenceRoot = $true
    foreach ($source in $sidecarSources) {
        $destination = Assert-ChildPath $InstalledDirectory (Join-Path $InstalledDirectory $source.Name)
        $hash = Get-Sha256 $source.FullName
        if (Test-Path -LiteralPath $destination) {
            if ((Get-Sha256 $destination) -cne $hash) { throw "An existing installation file differs from the diagnostic dependency: $($source.Name)" }
            continue
        }
        [System.IO.File]::Copy($source.FullName, $destination, $false)
        $createdSidecars.Add([pscustomobject]@{ path = $destination; sha256 = $hash })
    }
    Write-Json (Join-Path $EvidenceDirectory 'sidecars.created.json') $createdSidecars.ToArray()
    $probeDll = Assert-ChildPath $InstalledDirectory (Join-Path $InstalledDirectory 'AllSkillsProbe.dll')
    $probeExit = Invoke-Dotnet -Arguments @($probeDll, '--discover', '--direct', '--calculator-cases', $dataset,
        '--chat', 'all', '--model', 'gpt-6-luna', '--nativepath', $product) -Label 'probe'
    foreach ($line in Get-Content -LiteralPath (Join-Path $EvidenceDirectory 'probe.stdout.jsonl')) {
        try { $record = $line | ConvertFrom-Json } catch { continue }
        if ($record.PSObject.Properties.Name -contains 'stage' -and $record.stage -eq 'completed') {
            if ($null -ne $completedEvidence) { throw 'The probe produced more than one completion record.' }
            $completedEvidence = Assert-ChildPath $ownedEvidenceRoot ([string]$record.details.evidenceDirectory)
            if ([System.IO.Path]::GetDirectoryName($completedEvidence) -ine $ownedEvidenceRoot -or
                [System.IO.Path]::GetFileName($completedEvidence) -cnotmatch '^\d{8}T\d{9}Z-[0-9a-f]{8}$') {
                throw 'The completion evidence directory is outside the expected invocation scope.'
            }
        }
    }
    Preserve-ProbeOutput
    if ($probeExit -ne 0) { throw "The installed-host probe failed with exit code $probeExit; partial evidence is retained." }
    if ($null -eq $completedEvidence -or -not $outputPreserved) { throw 'The completed probe did not identify a preserved evidence bundle.' }
    $archivedEvidence = Assert-ChildPath $EvidenceDirectory (Join-Path (Join-Path $EvidenceDirectory 'probe-output') ([System.IO.Path]::GetFileName($completedEvidence)))
    $startRecords = @(Get-Content -LiteralPath (Join-Path $archivedEvidence 'probe.jsonl') | ForEach-Object {
        $entry = $_ | ConvertFrom-Json
        if ($entry.stage -eq 'start') { $entry }
    })
    $expectedDataRoot = Assert-LocalPath (Join-Path $env:LOCALAPPDATA 'TableCloth.Data\ManagedAi')
    if ($startRecords.Count -ne 1 -or
        -not (Assert-LocalPath ([string]$startRecords[0].details.appOwnedRoot)).Equals($expectedDataRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'A current installed-package run must use TableCloth.Data\ManagedAi outside the installer-owned directory.'
    }
    $updateSafeDataRootVerified = $true
    $verifyExit = Invoke-Dotnet -Arguments @($probeDll, '--verify', $archivedEvidence, '--expected-product-sha256', $ExpectedProductSha256) -Label 'verify'
    if ($verifyExit -ne 0) { throw "Independent verification of the installed-host evidence failed with exit code $verifyExit." }
}
catch { $problems.Add($_.Exception.Message) }
finally {
    if ($ownsEvidenceRoot -and -not $outputPreserved -and (Test-Path -LiteralPath $ownedEvidenceRoot)) {
        try { Preserve-ProbeOutput } catch { $problems.Add('Evidence preservation failed: ' + $_.Exception.Message) }
    }
    if ($ownsEvidenceRoot -and $outputPreserved -and (Test-Path -LiteralPath $ownedEvidenceRoot)) {
        try {
            $cleanupRoot = Assert-ChildPath $InstalledDirectory $ownedEvidenceRoot
            $currentSource = @(Get-FileManifest $cleanupRoot)
            if (-not (Test-ManifestEqual $preservedSourceManifest $currentSource)) { throw 'Probe evidence changed after preservation; its source was retained.' }
            Remove-Item -LiteralPath $cleanupRoot -Recurse -Force
        }
        catch { $problems.Add('Evidence cleanup failed: ' + $_.Exception.Message) }
    }
    foreach ($sidecar in $createdSidecars) {
        try {
            $cleanupFile = Assert-ChildPath $InstalledDirectory $sidecar.path
            if ([System.IO.Path]::GetDirectoryName($cleanupFile) -ine $InstalledDirectory) { throw 'Sidecar cleanup is limited to tracked top-level files.' }
            $leaf = [System.IO.Path]::GetFileName($cleanupFile)
            if ([System.IO.Path]::GetExtension($leaf) -ine '.dll' -and
                $leaf -cnotin @('AllSkillsProbe.deps.json', 'AllSkillsProbe.runtimeconfig.json')) {
                throw 'Sidecar cleanup is limited to the diagnostic DLL and launch-file whitelist.'
            }
            if (-not (Test-Path -LiteralPath $cleanupFile -PathType Leaf)) { throw 'A tracked sidecar disappeared before cleanup.' }
            if ((Get-Sha256 $cleanupFile) -cne $sidecar.sha256) { throw 'A tracked sidecar changed; refusing to delete it.' }
            Remove-Item -LiteralPath $cleanupFile -Force
        }
        catch { $problems.Add('Sidecar cleanup failed: ' + $_.Exception.Message) }
    }
    try {
        $after = @(Get-FileManifest $InstalledDirectory)
        Write-Json (Join-Path $EvidenceDirectory 'installation.after.files.json') $after
        $originalFilesUnchanged = Test-ManifestEqual $before $after
        if (-not $originalFilesUnchanged) { $problems.Add('The installation file manifest differs after the diagnostic run.') }
    }
    catch { $problems.Add('Installation integrity comparison failed: ' + $_.Exception.Message) }
    $summary = [pscustomobject]@{
        passed = $problems.Count -eq 0 -and $probeExit -eq 0 -and $verifyExit -eq 0 -and $originalFilesUnchanged -and $updateSafeDataRootVerified
        installedDirectory = $InstalledDirectory; productSha256 = $ExpectedProductSha256
        probeExitCode = $probeExit; verificationExitCode = $verifyExit; outputPreserved = $outputPreserved
        originalPackageFilesUnchanged = $originalFilesUnchanged; originalPackageFileCount = $before.Count
        updateSafeDataRootVerified = $updateSafeDataRootVerified
        temporarySidecarsCreated = $createdSidecars.Count; evidenceDirectory = $EvidenceDirectory; failures = $problems.ToArray()
        scope = 'Managed diagnostic parent using the actual installed NativeAOT MCP child; this does not qualify the AOT GUI parent.'
    }
    Write-Json (Join-Path $EvidenceDirectory 'installed-probe.summary.json') $summary
    $summary | ConvertTo-Json -Depth 10
}
if ($problems.Count -gt 0) { throw ($problems -join [Environment]::NewLine) }
