#Requires -Version 5.1
<#
.SYNOPSIS
    무설치 식탁보 준비 스크립트 (간소화 레인 워커).

.DESCRIPTION
    no-install-spork.wsb 가 띄운 "보이는" PowerShell 창(-WindowStyle Normal, -NoExit)이
    릴리스 고정 URL 에서 이 스크립트를 내려받아 dot-source 로 실행한다. 역할: 최신 런처
    (SporkBootstrap_<arch>.exe)를 진행률과 함께 내려받아 실행하고 창을 닫는다.

    - DNS 선보정(probe-then-fallback)은 이 스크립트 다운로드보다 먼저 필요하므로(닭-달걀)
      .wsb 인라인에 남는다. 여기서는 하지 않는다.
    - dot-source 실행을 전제로 하므로 exit 가 곧 창 닫기다. 실패 시에는 오류를 표시하고
      Enter 입력을 기다린 뒤 닫는다(원인 확인 가능).
    - 게시: 릴리스 자산 tablecloth-prepare.ps1 (고정 URL:
      https://github.com/yourtablecloth/TableCloth/releases/latest/download/tablecloth-prepare.ps1).
      로직 수정은 .wsb 재배포 없이 다음 릴리스부터 반영된다.

.PARAMETER SiteIds
    (선택) 공백 구분 사이트 Id. 지정하면 런처에 --site-ids 로 전달되어 해당 사이트를
    사전선택한다(은행별 딥링크 .wsb 용, SPEC §0.5 의 __SPORK_SITE_IDS__ 플레이스홀더).

.NOTES
    자산명/고정 URL 계약: docs/PARAMETERIZED_WSB_SPEC.md §0.5, docs/EXPRESS_BOOTSTRAPPER_DESIGN.md §10.
#>
param(
    [string] $SiteIds = ''
)

$ErrorActionPreference = 'Stop'

try { $Host.UI.RawUI.WindowTitle = '식탁보 준비' } catch { }

Write-Host ''
Write-Host '  식탁보를 준비하고 있습니다. 잠시만 기다려 주세요...' -ForegroundColor Cyan
Write-Host ''

# 런처 바이너리는 arch 별이므로 다운로드 전에 판별한다.
$arch = if ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64' -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$launcherPath = Join-Path $env:TEMP 'SporkBootstrap.exe'

try {
    Write-Host '  [1/2] 준비 도구를 내려받는 중...' -ForegroundColor Gray
    $ProgressPreference = 'Continue'
    Invoke-WebRequest "https://github.com/yourtablecloth/TableCloth/releases/latest/download/SporkBootstrap_$arch.exe" -OutFile $launcherPath

    Write-Host '  [2/2] 준비 완료. 식탁보를 실행합니다...' -ForegroundColor Green
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
    Write-Host ('  다운로드에 실패했습니다: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host '  네트워크 연결을 확인한 뒤 샌드박스를 다시 시작해 주세요.' -ForegroundColor Yellow
    $null = Read-Host '  이 창을 닫으려면 Enter 키를 누르세요'
    exit 1
}
