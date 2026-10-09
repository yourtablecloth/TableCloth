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
    $packageUrl = 'https://github.com/yourtablecloth/TableCloth/releases/latest/download/Spork_' + $arch + '_Portable.zip';
    $imagesUrl = 'https://yourtablecloth.app/TableClothCatalog/Images.zip';
    ('Safe Mode; guest channel=Retail; arch=' + $arch + '; package=' + $packageUrl + '; images=' + $imagesUrl) | Out-File -LiteralPath $log -Encoding utf8;
    Write-Host 'Getting TableCloth ready in Safe Mode...' -ForegroundColor Cyan;
    if (__SAFE_DNS_FALLBACK__ -and -not (Resolve-DnsName -Name github.com -QuickTimeout -ErrorAction SilentlyContinue)) { Get-NetAdapter | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses 8.8.8.8,1.1.1.1 };
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 3072;
    $stage = Join-Path $env:TEMP ('tablecloth-safe-' + [Guid]::NewGuid().ToString('N'));
    $null = New-Item -ItemType Directory -Path $stage;
    $package = Join-Path $stage 'Spork.zip';
    $images = Join-Path $stage 'Images.zip';
    Write-Host 'Downloading TableCloth...' -ForegroundColor Gray;
    $response = Invoke-WebRequest -UseBasicParsing -Uri $packageUrl -OutFile $package -PassThru;
    ('package response=' + $response.BaseResponse.ResponseUri.GetLeftPart([UriPartial]::Path)) | Add-Content -LiteralPath $log;
    Add-Type -AssemblyName System.IO.Compression.FileSystem;
    $dest = Join-Path $stage 'Spork';
    $root = [IO.Path]::GetFullPath($dest) + [IO.Path]::DirectorySeparatorChar;
    $archive = [IO.Compression.ZipFile]::OpenRead($package);
    try {
        foreach ($entry in $archive.Entries) {
            $path = [IO.Path]::GetFullPath([IO.Path]::Combine($dest, $entry.FullName));
            if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'The application archive contains an invalid path.' };
        };
    } finally { $archive.Dispose() };
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $dest);
    $exe = Join-Path $dest 'current\Spork.exe';
    if (-not (Test-Path -LiteralPath $exe)) { $exe = Join-Path $dest 'Spork.exe' };
    if (-not (Test-Path -LiteralPath $exe)) { throw 'Spork.exe was not found in the application archive.' };
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe);
    ('guest executable=' + $exe + '; version=' + $version.FileVersion + '; product=' + $version.ProductVersion) | Add-Content -LiteralPath $log;
    try {
        Write-Host 'Downloading site logos...' -ForegroundColor Gray;
        $response = Invoke-WebRequest -UseBasicParsing -Uri $imagesUrl -OutFile $images -PassThru;
        ('images response=' + $response.BaseResponse.ResponseUri.GetLeftPart([UriPartial]::Path)) | Add-Content -LiteralPath $log;
        $imageDir = Join-Path ([IO.Path]::GetDirectoryName($exe)) 'images';
        $null = New-Item -ItemType Directory -Path $imageDir -Force;
        $archive = [IO.Compression.ZipFile]::OpenRead($images);
        $count = 0;
        try {
            foreach ($entry in $archive.Entries) {
                if ($entry.Name -match '^[A-Za-z0-9_.-]+\.png$') {
                    if ($entry.Length -gt 16777216 -or $count -ge 5000) { throw 'The image archive exceeds the size limit.' };
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $imageDir $entry.Name), $true);
                    $count++;
                };
            };
        } finally { $archive.Dispose() };
        if ($count -eq 0) { throw 'The image archive contains no site logos.' };
        ('site logos=' + $count) | Add-Content -LiteralPath $log;
    } catch {
        ('Site logos unavailable: ' + $_.Exception.Message) | Add-Content -LiteralPath $log;
        Write-Host 'Site logos could not be downloaded. The site list will use placeholder icons.' -ForegroundColor Yellow;
    };
    $arguments = @();
    if ($env:TABLECLOTH_SITE_IDS) { $arguments += ($env:TABLECLOTH_SITE_IDS -split ' ') };
    if ($env:TABLECLOTH_TARGET_URL) { $arguments += @('--target-url', $env:TABLECLOTH_TARGET_URL) };
    $quoted = @($arguments | ForEach-Object { '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"' });
    Write-Host 'Starting TableCloth in Safe Mode...' -ForegroundColor Green;
    $process = if ($quoted.Count) { Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) -ArgumentList $quoted -PassThru } else { Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) -PassThru };
    'Spork launch requested.' | Add-Content -LiteralPath $log;
    if ($process.WaitForExit(5000)) { throw ('Spork closed during startup; exit code=' + $process.ExitCode + '.') };
    ('Spork initial startup alive; pid=' + $process.Id) | Add-Content -LiteralPath $log;
} catch {
    $_.Exception.Message | Add-Content -LiteralPath $log;
    Write-Host ('Safe Mode setup failed: ' + $_.Exception.Message) -ForegroundColor Red;
    $null = Read-Host 'Press Enter to close';
};
