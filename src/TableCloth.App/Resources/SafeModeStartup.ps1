# This script is embedded in SafeMode.wsb, never mapped from the host.
# Keep it ASCII-only and use single quotes: it runs inside two argv/string layers.
$ErrorActionPreference = 'Stop';
$ProgressPreference = 'SilentlyContinue';
$Host.UI.RawUI.WindowTitle = 'TableCloth Safe Mode';
$log = Join-Path ([Environment]::GetFolderPath('Desktop')) 'tablecloth-safe-mode.log';
$env:TABLECLOTH_SAFE_MODE = '1';
$env:TABLECLOTH_SITE_IDS = '__SAFE_SITE_IDS__';
$env:TABLECLOTH_TARGET_URL = '__SAFE_TARGET_URL__';
try {
    $arch = if ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64' -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } elseif ($env:PROCESSOR_ARCHITECTURE -eq 'AMD64' -or $env:PROCESSOR_ARCHITEW6432 -eq 'AMD64') { 'x64' } else { throw 'Safe Mode supports x64 and ARM64 only.' };
    ('Safe Mode; prepare=https://github.com/yourtablecloth/TableCloth/releases/latest/download/tablecloth-prepare.ps1; guest channel=Retail; arch=' + $arch) | Out-File -LiteralPath $log -Encoding utf8;
    ('launcher=https://github.com/yourtablecloth/TableCloth/releases/latest/download/SporkBootstrap_' + $arch + '.exe') | Add-Content -LiteralPath $log;
    Write-Host 'Getting TableCloth ready in Safe Mode...' -ForegroundColor Cyan;
    if (__SAFE_DNS_FALLBACK__ -and -not (Resolve-DnsName -Name github.com -QuickTimeout -ErrorAction SilentlyContinue)) { Get-NetAdapter | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses 8.8.8.8,1.1.1.1 };
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072;
    $prepare = Join-Path $env:TEMP 'tablecloth-prepare.ps1';
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/yourtablecloth/TableCloth/releases/latest/download/tablecloth-prepare.ps1' -OutFile $prepare;
    $ids = $env:TABLECLOTH_SITE_IDS;
    if ($ids -and (Get-Content -Raw -LiteralPath $prepare).Contains('Start-Process $launcherPath -ArgumentList $launcherArgs')) { $env:TABLECLOTH_SITE_IDS = '"' + $ids + '"' };
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $prepare;
    $env:TABLECLOTH_SITE_IDS = $ids;
    if ($LASTEXITCODE -ne 0) { throw 'The preparation script failed.' };
    $deadline = (Get-Date).AddMinutes(10);
    $guest = $null;
    while ((Get-Date) -lt $deadline) {
        $guest = Get-Process -Name Spork -ErrorAction SilentlyContinue | Select-Object -First 1;
        if ($guest) { break };
        if (-not (Get-Process -Name SporkBootstrap -ErrorAction SilentlyContinue)) { throw 'The setup tool closed before Spork started. See spork-bootstrap.log.' };
        Start-Sleep -Seconds 1;
    };
    if (-not $guest) { throw 'Spork did not start within 10 minutes. See spork-bootstrap.log.' };
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($guest.Path);
    ('guest executable=' + $guest.Path + '; version=' + $version.FileVersion + '; product=' + $version.ProductVersion) | Add-Content -LiteralPath $log;
} catch {
    $_.Exception.Message | Add-Content -LiteralPath $log;
    Write-Host ('Safe Mode setup failed: ' + $_.Exception.Message) -ForegroundColor Red;
    $null = Read-Host 'Press Enter to close';
};
