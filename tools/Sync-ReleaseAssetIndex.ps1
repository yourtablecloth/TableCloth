[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AssetDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]*$')][string]$Channel,
    [Parameter(Mandatory)][string]$InstallerName,
    [Parameter(Mandatory)][string]$PortableName
)

$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $AssetDirectory).Path
$indexPath = Join-Path $directory "assets.$Channel.json"
$index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json -AsHashtable -NoEnumerate
if ($index -isnot [Array]) { throw "Expected an asset array: $indexPath" }

# Velopack writes this index before the repository applies its public asset names.
# Keep hashes, sizes and every other field intact; only update these two names.
$names = @{ Installer = $InstallerName; Portable = $PortableName }
foreach ($type in $names.Keys) {
    $name = $names[$type]
    if ([string]::IsNullOrWhiteSpace($name) -or [IO.Path]::GetFileName($name) -cne $name -or $name.Contains('/') -or $name.Contains('\')) {
        throw "Expected a file name for ${type}: $name"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $directory $name) -PathType Leaf)) {
        throw "Renamed asset does not exist: $name"
    }
    $entries = @($index | Where-Object { $_.Type -eq $type })
    if ($entries.Count -ne 1) { throw "Expected exactly one $type entry in $indexPath" }
    $entries[0].RelativeFileName = $name
}

[IO.File]::WriteAllText($indexPath, ($index | ConvertTo-Json -Depth 50 -Compress) + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "Updated asset names in assets.$Channel.json"
