#Requires -Version 5.1
<#
.SYNOPSIS
    무설치 Spork 부트스트랩 (레거시 / 완전 파라미터화형 참조 구현).

.DESCRIPTION
    [참고] 정식 데뷔 기본형은 이 스크립트를 쓰지 않는다. 기본 no-install-spork.wsb 는 런처
    (SporkBootstrap)를 GitHub 고정 URL 로 직접 받아 실행하는 인라인 LogonCommand 로 간소화됐다
    (docs/PARAMETERIZED_WSB_SPEC.md §0.5). 본 스크립트는 버전 핀/오프라인/사설 미러 등
    "완전 파라미터화형"(SPEC §3~§4)의 참조로만 보존한다.

    마운트 없는 바 Windows Sandbox 안에서 self-contained 포터블 Spork 를 받아 실행한다.

    동작 순서:
      1) 다운로드 "전"에 공용 DNS(8.8.8.8 / 1.1.1.1) 강제 — DNS 미설정 환경 대비(정상 환경엔 무해).
         (Spork 내부 DNS 보정은 Spork 를 받은 뒤에야 도므로 여기서 먼저 한다.)
      2) 포터블 Spork zip 다운로드 → 압축 해제.
      3) Spork.exe 실행.

.NOTES
    $SporkPortableZipUrl 의 기본값 자리표시자 __SPORK_PORTABLE_ZIP_URL__ 는 배포 웹앱
    (yourtablecloth.app)이 GitHub 최신 릴리스의 Spork_<버전>_<config>_<platform>_Portable.zip
    URL 로 치환하거나, 인자로 전달한다. "최신 태그 확인 → URL 해석"은 웹앱 로직(repo 스코프 밖).

    자산명 계약: docs/PORTABLE_MODE2_TODO.md 의 "다운로드 자산명 계약" 절 참조.
#>
param(
    [string] $SporkPortableZipUrl = '__SPORK_PORTABLE_ZIP_URL__'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# 1) 공용 DNS 보정 (다운로드 전). 실패해도 다운로드를 시도한다.
try {
    Get-NetAdapter -ErrorAction Stop |
        Where-Object { $_.Status -eq 'Up' } |
        ForEach-Object {
            netsh interface ipv4 set dns name="$($_.Name)" static 8.8.8.8 primary | Out-Null
            netsh interface ipv4 add dns name="$($_.Name)" addr=1.1.1.1 index=2 | Out-Null
        }
} catch {
    Write-Warning "DNS 설정 건너뜀: $_"
}

# 2) 포터블 Spork 다운로드 + 압축 해제.
$destDir = Join-Path $env:USERPROFILE 'Desktop\Spork'
$zipPath = Join-Path $env:TEMP 'Spork_Portable.zip'

Invoke-WebRequest -Uri $SporkPortableZipUrl -OutFile $zipPath

if (Test-Path $destDir) {
    Remove-Item -Path $destDir -Recurse -Force
}
Expand-Archive -Path $zipPath -DestinationPath $destDir -Force

# 3) 실행.
$exePath = Join-Path $destDir 'Spork.exe'
if (-not (Test-Path $exePath)) {
    throw "Spork.exe not found under $destDir"
}
Start-Process -FilePath $exePath
