<#
.SYNOPSIS
  Download a draft release's .exe assets, sign them with a SimplySign-backed
  signtool, and re-upload them in place.

.DESCRIPTION
  Workflow:
    1. Verifies SimplySign Desktop has a usable code-signing cert in CurrentUser\My.
    2. Downloads .exe assets for the given release tag.
    3. Signs each with signtool (SHA-256, RFC 3161 timestamp).
    4. Verifies signatures.
    5. Re-uploads via `gh release upload --clobber`.

  After this completes, open the release in a browser, remove the UNSIGNED
  marker from the notes, and click Publish.

.PARAMETER Tag
  Release tag (e.g. v1.14.0).

.PARAMETER SubjectName
  Substring of the SimplySign certificate Subject/CN. Falls back to
  $env:TABLECLOTH_SIGN_SUBJECT.

.PARAMETER TimestampUrl
  RFC 3161 timestamp authority. Defaults to Certum's.

.PARAMETER Repo
  Target GitHub repository (owner/name). Defaults to yourtablecloth/TableCloth.
  Passed explicitly to gh because the work dir is a temp folder with no git
  remotes; without it gh fails with "no git remotes found".

.EXAMPLE
  $env:TABLECLOTH_SIGN_SUBJECT = 'Jung Hyun, Nam'
  .\tools\sign-release.ps1 -Tag v1.14.0
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$Tag,
  [string]$SubjectName = $env:TABLECLOTH_SIGN_SUBJECT,
  [string]$TimestampUrl = 'http://time.certum.pl',
  [string]$Repo = 'yourtablecloth/TableCloth',
  [string]$WorkDir = (Join-Path $env:TEMP "tablecloth-sign-$Tag")
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SubjectName)) {
  throw 'Provide -SubjectName or set $env:TABLECLOTH_SIGN_SUBJECT (substring of the SimplySign cert CN).'
}

foreach ($cmd in @('gh', 'signtool')) {
  if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) {
    throw "$cmd not found on PATH."
  }
}

$cert = Get-ChildItem Cert:\CurrentUser\My |
  Where-Object { $_.Subject -like "*$SubjectName*" -and $_.HasPrivateKey } |
  Select-Object -First 1
if (-not $cert) {
  throw "No certificate matching '$SubjectName' (with private key) found in CurrentUser\My. Is SimplySign Desktop logged in?"
}
Write-Host "Using certificate: $($cert.Subject)" -ForegroundColor Cyan
Write-Host "  Expires: $($cert.NotAfter)" -ForegroundColor DarkGray

if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
New-Item -ItemType Directory -Path $WorkDir | Out-Null

Push-Location $WorkDir
try {
  Write-Host "Downloading $Tag assets to $WorkDir..." -ForegroundColor Cyan
  & gh release download $Tag --repo $Repo --pattern '*.exe'
  if ($LASTEXITCODE -ne 0) { throw 'gh release download failed.' }

  $assets = @(Get-ChildItem -File -Filter *.exe)
  if ($assets.Count -eq 0) { throw 'No .exe assets downloaded.' }
  Write-Host "Signing $($assets.Count) asset(s):" -ForegroundColor Cyan
  $assets | ForEach-Object { Write-Host "  $($_.Name)" }

  $files = @($assets.FullName)

  & signtool sign /n $SubjectName /tr $TimestampUrl /td sha256 /fd sha256 /v $files
  if ($LASTEXITCODE -ne 0) { throw 'signtool sign failed.' }

  Write-Host "Verifying..." -ForegroundColor Cyan
  & signtool verify /pa /all $files
  if ($LASTEXITCODE -ne 0) { throw 'signtool verify failed.' }

  Write-Host "Re-uploading signed assets to release $Tag..." -ForegroundColor Cyan
  & gh release upload $Tag $files --repo $Repo --clobber
  if ($LASTEXITCODE -ne 0) { throw 'gh release upload failed.' }

  Write-Host ''
  Write-Host 'Done.' -ForegroundColor Green
  Write-Host 'Next: open the release on GitHub, remove the UNSIGNED marker, and click Publish.' -ForegroundColor Yellow
}
finally {
  Pop-Location
}
