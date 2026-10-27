param(
    [Parameter(Mandatory)][string]$HostExecutable,
    [Parameter(Mandatory)][string]$SeedManifest,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$LegacyControl,
    [switch]$KeepFixtures
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$utf8 = [System.Text.UTF8Encoding]::new($false)
if (-not ('TableCloth.SkillSmokeDiagnostics.ProcessEvidenceObserver' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot '../../ProcessEvidenceObserver.cs')
}

function Assert-LocalPath([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not [System.IO.Path]::IsPathFullyQualified($Path) -or $resolved.StartsWith('\\') -or $resolved.Substring(2).Contains(':')) {
        throw 'Only absolute local paths without alternate streams are accepted.'
    }
    for ($current = $resolved; $current; $current = [System.IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw 'Reparse points are not accepted.'
        }
    }
    return $resolved
}

function Assert-ChildPath([string]$Root, [string]$Path) {
    $resolved = Assert-LocalPath $Path
    $prefix = [System.IO.Path]::TrimEndingDirectorySeparator((Assert-LocalPath $Root)) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw 'Path is outside the intended root.' }
    return $resolved
}

function Write-Utf8([string]$Path, [string]$Content) {
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
    [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Get-Sha256([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

$HostExecutable = Assert-LocalPath $HostExecutable
$SeedManifest = Assert-LocalPath $SeedManifest
$EvidenceDirectory = Assert-LocalPath $EvidenceDirectory
$fixtureSource = Assert-LocalPath (Join-Path $PSScriptRoot 'concurrent-urlopen.py')
if (-not (Test-Path -LiteralPath $HostExecutable -PathType Leaf) -or [System.IO.Path]::GetFileName($HostExecutable) -ine 'TableCloth.exe') {
    throw 'An actual TableCloth.exe is required.'
}
$seed = Get-Content -LiteralPath $SeedManifest -Raw | ConvertFrom-Json
$ownedRoot = Assert-LocalPath ([string]$seed.ownedRoot)
$expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'TableCloth\ManagedAi\skill-execution'))
if (-not $ownedRoot.Equals($expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Seed ownedRoot must be the installed TableCloth skill-execution directory.'
}
foreach ($runtime in @($seed.runtimeFiles) + @(
    [pscustomobject]@{ path = $seed.pythonExecutable; sha256 = $seed.pythonSha256 },
    [pscustomobject]@{ path = $seed.nodeExecutable; sha256 = $seed.nodeSha256 }
)) {
    $runtimePath = Assert-ChildPath $ownedRoot ([string]$runtime.path)
    if ((Get-Sha256 $runtimePath) -cne ([string]$runtime.sha256).ToLowerInvariant()) { throw 'Seed runtime hash no longer matches.' }
}

[System.IO.Directory]::CreateDirectory($EvidenceDirectory) | Out-Null
$fixtureBase = Assert-ChildPath $ownedRoot (Join-Path $ownedRoot 'native-fixtures')
$fixtureDirectory = Assert-ChildPath $fixtureBase (Join-Path $fixtureBase ([guid]::NewGuid().ToString('N')))
[System.IO.Directory]::CreateDirectory($fixtureDirectory) | Out-Null
$package = Assert-ChildPath $fixtureDirectory (Join-Path $fixtureDirectory 'package')
[System.IO.Directory]::CreateDirectory($package) | Out-Null
$process = $null
$processStarted = $false
$processObserver = $null
$observations = $null

try {
    $entry = Assert-ChildPath $package (Join-Path $package 'concurrent-urlopen.py')
    [System.IO.File]::Copy($fixtureSource, $entry, $false)
    $scriptHash = Get-Sha256 $entry
    $registration = [ordered]@{
        skillId = 'python-network-concurrency'; kind = 'python-script'; scriptPath = $entry; sha256 = $scriptHash;
        packageRoot = $package; packageFiles = @(@{path=$entry;sha256=$scriptHash});
        description = 'Contained synthetic Python request/reply concurrency regression';
        requiresNetwork = $true; networkHosts = @('ecos.bok.or.kr')
    }
    $manifest = [ordered]@{
        ownedRoot = $ownedRoot; pythonExecutable = $seed.pythonExecutable; pythonSha256 = $seed.pythonSha256;
        nodeExecutable = $seed.nodeExecutable; nodeSha256 = $seed.nodeSha256;
        runtimeFiles = @($seed.runtimeFiles); skills = @($registration)
    }
    $manifestPath = Join-Path $fixtureDirectory 'manifest.json'
    $manifestJson = $manifest | ConvertTo-Json -Depth 16
    Write-Utf8 $manifestPath $manifestJson
    Write-Utf8 (Join-Path $EvidenceDirectory 'fixture-manifest.json') $manifestJson
    $inputJson = @{legacyControl=[bool]$LegacyControl.IsPresent} | ConvertTo-Json -Compress
    Write-Utf8 (Join-Path $EvidenceDirectory 'input.json') ($inputJson+"`n")

    $messages = @(
        (@{jsonrpc='2.0';id=1;method='initialize';params=@{protocolVersion='2025-06-18';capabilities=@{};clientInfo=@{name='python-network-concurrency';version='1'}}} | ConvertTo-Json -Depth 10 -Compress),
        (@{jsonrpc='2.0';method='notifications/initialized'} | ConvertTo-Json -Compress),
        (@{jsonrpc='2.0';id=2;method='tools/list';params=@{}} | ConvertTo-Json -Compress),
        (@{jsonrpc='2.0';id=3;method='tools/call';params=@{name='python-network-concurrency';arguments=@{arguments=@();stdin=$inputJson}}} | ConvertTo-Json -Depth 10 -Compress)
    )
    Write-Utf8 (Join-Path $EvidenceDirectory 'requests.jsonl') (($messages -join "`n")+"`n")
    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $HostExecutable
    $start.ArgumentList.Add('--skill-mcp'); $start.ArgumentList.Add($manifestPath)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.StandardInputEncoding = $utf8; $start.StandardOutputEncoding = $utf8; $start.StandardErrorEncoding = $utf8
    $hostSha256 = Get-Sha256 $HostExecutable
    $processObserver = [TableCloth.SkillSmokeDiagnostics.ProcessEvidenceObserver]::new($PID, $HostExecutable, $seed.pythonExecutable, $seed.nodeExecutable)
    $processObserver.Start()
    $process = [System.Diagnostics.Process]::new(); $process.StartInfo = $start
    if (-not $process.Start()) { throw 'The product broker did not start.' }
    $processStarted = $true
    $stdoutTask = $process.StandardOutput.ReadToEndAsync(); $stderrTask = $process.StandardError.ReadToEndAsync()
    foreach ($message in $messages) { $process.StandardInput.WriteLine($message) }
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(75000)) { $process.Kill($true); throw 'The contained concurrency fixture exceeded its deadline.' }
    $stdout = $stdoutTask.GetAwaiter().GetResult(); $stderr = $stderrTask.GetAwaiter().GetResult()
    $observations = @($processObserver.Stop())
    Write-Utf8 (Join-Path $EvidenceDirectory 'processes.json') (ConvertTo-Json -InputObject $observations -Depth 8)
    Write-Utf8 (Join-Path $EvidenceDirectory 'responses.jsonl') $stdout
    Write-Utf8 (Join-Path $EvidenceDirectory 'stderr.txt') $stderr
    $responses = @($stdout -split "`r?`n" | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
    $listResponse = @($responses | Where-Object { $_.id -eq 2 })
    $toolListed = $listResponse.Count -eq 1 -and $listResponse[0].PSObject.Properties.Name -contains 'result' -and
        @($listResponse[0].result.tools | Where-Object { $_.name -ceq 'python-network-concurrency' }).Count -eq 1
    $response = @($responses | Where-Object { $_.id -eq 3 })
    $execution = $null; $payload = $null
    if ($response.Count -eq 1 -and $response[0].PSObject.Properties.Name -contains 'result') {
        $execution = $response[0].result.content[0].text | ConvertFrom-Json
        try { $payload = $execution.stdout | ConvertFrom-Json } catch { }
    }
    $contained = $toolListed -and $null -ne $execution -and $execution.wasAppContainer -and
        -not $execution.timedOut -and -not $execution.outputLimited -and $null -ne $payload -and
        $payload.kind -ceq 'python-network-concurrency' -and $payload.syntheticResponder -and $payload.realNetworkRequests -eq 0
    $passed = $false
    if ($contained -and $LegacyControl.IsPresent) {
        $correlation = @($payload.checks | Where-Object { $_.name -ceq 'correlation' })
        $passed = $execution.status -ceq 'failed' -and $execution.exitCode -eq 1 -and $execution.failure -ceq 'worker-exit' -and
            $payload.legacyControl -and -not $payload.passed -and $correlation.Count -eq 1 -and
            -not $correlation[0].passed -and $correlation[0].finished -and $correlation[0].crossTalkObserved
    }
    elseif ($contained) {
        $expectedChecks = 'correlation','urlerror','httperror','timeout-release'
        $passed = $execution.status -ceq 'completed' -and $execution.exitCode -eq 0 -and -not $payload.legacyControl -and $payload.passed -and
            @($payload.checks).Count -eq 4 -and @($payload.checks | Where-Object { -not $_.passed -or $_.name -cnotin $expectedChecks }).Count -eq 0
    }
    $brokerObservations = @($observations | Where-Object { $_.Kind -eq 'broker' -and $_.ProcessId -eq $process.Id -and
        $_.ParentProcessId -eq $PID -and $_.ImagePath -ieq $HostExecutable -and $_.Sha256 -ceq $hostSha256 -and
        $_.TokenQuerySucceeded -and $_.IsAppContainer -eq $false -and $null -eq $_.QueryError })
    $directWorkerObservations = @($observations | Where-Object { $_.Kind -eq 'worker' -and $_.ParentProcessId -eq $process.Id })
    $verifiedWorkerObservations = @($directWorkerObservations | Where-Object {
        $_.ImagePath -ieq $seed.pythonExecutable -and $_.Sha256 -ceq ([string]$seed.pythonSha256).ToLowerInvariant() -and
        $_.TokenQuerySucceeded -and $_.IsAppContainer -eq $true -and $null -eq $_.QueryError })
    $independentProcessChecksPassed = $brokerObservations.Count -eq 1 -and $directWorkerObservations.Count -eq 1 -and
        $verifiedWorkerObservations.Count -eq 1
    $summary = [ordered]@{
        recordedUtc = [DateTimeOffset]::UtcNow.ToString('O'); host = $HostExecutable; hostSha256 = $hostSha256;
        hostProductVersion = (Get-Item -LiteralPath $HostExecutable).VersionInfo.ProductVersion;
        seedManifestSha256 = Get-Sha256 $SeedManifest; fixtureSha256 = $scriptHash;
        legacyFunctionControl = [bool]$LegacyControl.IsPresent; syntheticResponder = $true; realNetworkRequests = 0;
        brokerExitCode = $process.ExitCode; expectedOutcomeObserved = [bool]($passed -and $independentProcessChecksPassed);
        independentProcessChecksPassed = [bool]$independentProcessChecksPassed;
        execution = $execution; payload = $payload
    }
    Write-Utf8 (Join-Path $EvidenceDirectory 'summary.json') ($summary | ConvertTo-Json -Depth 30)
    if ($process.ExitCode -ne 0 -or -not $passed) { throw 'Expected contained regression outcome was not observed; inspect summary.json and responses.jsonl.' }
    if (-not $independentProcessChecksPassed) { throw 'Independent Win32 process/token observations did not establish the exact broker and one direct AppContainer Python worker; inspect processes.json.' }
    if ($LegacyControl.IsPresent) { Write-Output 'Contained legacy-function negative control reproduced reply cross-talk.' }
    else { Write-Output 'Contained production bootstrap passed concurrency, error, response-disposal, and deadline recovery checks.' }
}
finally {
    try {
        if ($null -ne $process) {
            if ($processStarted -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
            $process.Dispose()
        }
    }
    finally {
        if ($null -ne $processObserver) {
            try {
                if ($null -eq $observations) {
                    $observations = @($processObserver.Stop())
                    Write-Utf8 (Join-Path $EvidenceDirectory 'processes.json') (ConvertTo-Json -InputObject $observations -Depth 8)
                }
            }
            finally { $processObserver.Dispose() }
        }
    }
    if (-not $KeepFixtures -and (Test-Path -LiteralPath $fixtureDirectory)) {
        $checkedFixture = Assert-ChildPath $fixtureBase $fixtureDirectory
        Get-ChildItem -LiteralPath $checkedFixture -Force -Recurse | ForEach-Object { Assert-ChildPath $checkedFixture $_.FullName | Out-Null }
        Remove-Item -LiteralPath $checkedFixture -Recurse -Force
    }
}
