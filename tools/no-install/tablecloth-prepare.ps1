#Requires -Version 5.1
<#
.SYNOPSIS
    No-install TableCloth preparation script (simplified-lane worker).

.DESCRIPTION
    Downloaded and dot-sourced by the visible PowerShell window (-WindowStyle Normal,
    -NoExit) that no-install-spork.wsb spawns. Role: download the latest launcher
    (SporkBootstrap_<arch>.exe) with a progress bar, run it, then close the window.

    - DNS pre-correction (probe-then-fallback) must happen BEFORE this script can be
      downloaded (chicken-and-egg), so it stays inline in the .wsb, not here.
    - Designed to be dot-sourced: 'exit' closes the hosting window. On failure it
      shows the error and waits for Enter so the cause stays visible.
    - Published as the release asset 'tablecloth-prepare.ps1' (fixed URL:
      https://github.com/yourtablecloth/TableCloth/releases/latest/download/tablecloth-prepare.ps1).
      Logic changes ship with the next release without redistributing the .wsb.
    - IMPORTANT: keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less
      scripts using the legacy ANSI codepage and guest codepages vary by host
      language pack, so non-ASCII text (e.g. Korean) gets mangled and can break
      parsing. Localized user-facing text lives in the launcher GUI, which handles
      languages at runtime.

.PARAMETER SiteIds
    (Optional) Space-separated catalog site ids. When given, passed to the launcher
    as --site-ids to preselect sites (bank-specific deep-link .wsb variants; see
    the __SPORK_SITE_IDS__ placeholder in PARAMETERIZED_WSB_SPEC.md section 0.5).

.NOTES
    Asset-name / fixed-URL contract: docs/PARAMETERIZED_WSB_SPEC.md section 0.5,
    docs/EXPRESS_BOOTSTRAPPER_DESIGN.md section 10.
#>
param(
    [string] $SiteIds = ''
)

$ErrorActionPreference = 'Stop'

try { $Host.UI.RawUI.WindowTitle = 'TableCloth Setup' } catch { }

Write-Host ''
Write-Host '  Getting TableCloth ready. Please wait...' -ForegroundColor Cyan
Write-Host ''

# The launcher binary is per-arch, so detect before downloading.
$arch = if ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64' -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$launcherPath = Join-Path $env:TEMP 'SporkBootstrap.exe'

try {
    Write-Host '  [1/2] Downloading the setup tool...' -ForegroundColor Gray
    $ProgressPreference = 'Continue'
    Invoke-WebRequest "https://github.com/yourtablecloth/TableCloth/releases/latest/download/SporkBootstrap_$arch.exe" -OutFile $launcherPath

    Write-Host '  [2/2] Done. Starting TableCloth...' -ForegroundColor Green
    if ([string]::IsNullOrWhiteSpace($SiteIds)) {
        Start-Process $launcherPath
    }
    else {
        Start-Process $launcherPath -ArgumentList @('--site-ids', $SiteIds)
    }
    Start-Sleep -Seconds 2
    exit 0
}
catch {
    Write-Host ''
    Write-Host ('  Download failed: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host '  Check the network connection, then restart the sandbox.' -ForegroundColor Yellow
    $null = Read-Host '  Press Enter to close this window'
    exit 1
}
