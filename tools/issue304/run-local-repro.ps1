<#
.SYNOPSIS
    이슈 #304 로컬 재현용 Windows Sandbox 를 만들어 실행한다.

.DESCRIPTION
    local-repro.wsb.template 의 플레이스홀더를 이 머신의 실제 경로로 치환해
    local-repro.wsb 를 만든 뒤 실행한다. 게스트에서는 LogonCommand 로
    local-repro.cmd 가 돌면서 result.txt 를 이 폴더에 남긴다.

    wsb 의 HostFolder 는 절대 경로만 받으므로 템플릿 + 치환 구조를 쓴다.

.PARAMETER TableClothDirectory
    [04] 프로브가 실행해 볼 식탁보 설치 폴더. 기본값은 Velopack 설치 위치.

.PARAMETER NoLaunch
    wsb 파일만 만들고 실행하지 않는다.

.EXAMPLE
    ./run-local-repro.ps1
    ./run-local-repro.ps1 -TableClothDirectory "D:\Projects\TableCloth\publish\win-x64" -NoLaunch
#>
[CmdletBinding()]
param(
    [string] $TableClothDirectory = (Join-Path $env:LOCALAPPDATA 'TableCloth\current'),
    [switch] $NoLaunch
)

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$templatePath = Join-Path $here 'local-repro.wsb.template'
$wsbPath = Join-Path $here 'local-repro.wsb'
$resultPath = Join-Path $here 'result.txt'

if (-not (Test-Path $templatePath)) {
    throw "템플릿을 찾을 수 없습니다: $templatePath"
}

# leaf 이름이 곧 게스트 경로가 된다. 템플릿의 LogonCommand 는 Desktop\issue304 를 가리키므로
# 폴더 이름이 바뀌었다면 둘 다 함께 고쳐야 한다.
$leaf = Split-Path $here -Leaf
if ($leaf -ne 'issue304') {
    Write-Warning "이 폴더의 이름이 'issue304' 가 아닙니다($leaf). 템플릿의 LogonCommand 경로도 함께 수정하세요."
}

if (-not (Test-Path $TableClothDirectory)) {
    Write-Warning "식탁보 설치 폴더가 없습니다: $TableClothDirectory — [04] 프로브는 건너뜁니다."
}

# 직전 실행 결과가 섞이지 않도록 정리.
if (Test-Path $resultPath) { Remove-Item $resultPath -Force }

(Get-Content $templatePath -Raw).
    Replace('__DIAG_DIR__', $here).
    Replace('__TABLECLOTH_DIR__', $TableClothDirectory) |
    Set-Content $wsbPath -Encoding UTF8

Write-Host "생성됨: $wsbPath"
Write-Host "  진단 폴더 : $here"
Write-Host "  식탁보     : $TableClothDirectory"

if ($NoLaunch) {
    Write-Host "-NoLaunch 지정 — 실행하지 않고 종료합니다."
    return
}

Write-Host "샌드박스를 실행합니다. 게스트 콘솔이 닫히지 않고 남으니 확인 후 창을 닫으세요."
Write-Host "결과 파일: $resultPath"
Start-Process $wsbPath
