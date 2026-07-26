<#
.SYNOPSIS
    이슈 #304 — 샌드박스 부팅 체인(LogonCommand → StartupScript.cmd → Spork)을
    식탁보 UI 없이 그대로 재현한다.

.DESCRIPTION
    SandboxBuilder 가 하는 일(설치 폴더를 staging\App 으로 복사 → StartupScript.cmd 생성
    → wsb 생성 → 실행)을 헤드리스로 흉내 낸다. 다만 StartupScript 에는 단계별
    브레드크럼이 들어가 있어, 부팅 체인이 **어디까지 갔는지**가 App\boot.log 에 남는다.

    App 폴더는 읽기·쓰기로 마운트되므로 boot.log 는 샌드박스가 살아 있는 동안에도
    호스트에서 그대로 읽힌다.

.PARAMETER Mode
    repro = 수정 전 그대로(citool 에 stdin 리다이렉트 없음)
    fixed = 수정 후(citool --refresh <nul)

.PARAMETER SourceDirectory
    App 으로 복사할 식탁보 설치 폴더. 기본값은 Velopack 설치 위치.

.PARAMETER LogonStyle
    LogonCommand 를 어떤 형태로 넣을지.
      direct  = 마운트의 .cmd 를 그대로 지정 (현행 SandboxBuilder 방식)
      wrapper = cmd.exe /c <.cmd 경로>  (System32 실행 파일을 거쳐 호출)
      probe   = cmd.exe /c 로 프로브 파일 기록 + dir + call <.cmd>
    "LogonCommand 자체가 안 도는가" vs "마운트의 .cmd 를 실행하지 못하는가" 를 가른다.

.PARAMETER NoLaunch
    staging 과 wsb 만 만들고 실행하지 않는다.

.EXAMPLE
    ./repro-boot-chain.ps1 -Mode repro -LogonStyle direct
    ./repro-boot-chain.ps1 -Mode fixed -LogonStyle wrapper
#>
[CmdletBinding()]
param(
    [ValidateSet('repro', 'fixed')]
    [string] $Mode = 'repro',
    [ValidateSet('direct', 'wrapper', 'probe')]
    [string] $LogonStyle = 'direct',
    [string] $SourceDirectory = (Join-Path $env:LOCALAPPDATA 'TableCloth\current'),
    [switch] $NoLaunch
)

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$work = Join-Path $env:TEMP 'tablecloth-issue304'
$appDir = Join-Path $work 'App'          # leaf 이름이 곧 게스트의 Desktop\App 이 된다
$bootLog = Join-Path $appDir 'boot.log'
$wsbPath = Join-Path $work 'repro.wsb'

if (-not (Test-Path $SourceDirectory)) {
    throw "식탁보 설치 폴더를 찾을 수 없습니다: $SourceDirectory"
}

# 이전 실행 잔재가 결과를 오염시키지 않도록 staging 을 통째로 새로 만든다.
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $appDir -Force | Out-Null

Write-Host "[1/4] 설치 폴더 복사: $SourceDirectory -> $appDir"
Copy-Item (Join-Path $SourceDirectory '*') $appDir -Recurse -Force
# Images.zip 은 SandboxBuilder 도 제외한다(게스트에는 풀린 images\ 를 따로 넣어줌).
Remove-Item (Join-Path $appDir 'Images.zip') -Force -ErrorAction SilentlyContinue

Write-Host "[2/4] StartupScript.cmd 생성 (mode=$Mode)"
$citoolStdin = if ($Mode -eq 'fixed') { '<nul' } else { '' }
$template = Get-Content (Join-Path $here 'startup-script.cmd.template') -Raw
$script = $template.Replace('__CITOOL_STDIN__', $citoolStdin)
# 실제 StartupScript.cmd 와 동일하게 ANSI(기본 코드페이지)로 떨군다.
[System.IO.File]::WriteAllText(
    (Join-Path $appDir 'StartupScript.cmd'), $script, [System.Text.Encoding]::Default)

Write-Host "[3/4] wsb 생성: $wsbPath (logonStyle=$LogonStyle)"
$guestApp = 'C:\Users\WDAGUtilityAccount\Desktop\App'
$probeFile = "$guestApp\logon-probe.txt"
# XML 이므로 & 와 > 는 반드시 엔티티로 이스케이프해야 한다.
$command = switch ($LogonStyle) {
    'direct'  { "$guestApp\StartupScript.cmd" }
    'wrapper' { "cmd.exe /c $guestApp\StartupScript.cmd" }
    'probe'   { "cmd.exe /c echo probe %DATE% %TIME% &gt; $probeFile &amp; dir $guestApp &gt;&gt; $probeFile &amp; call $guestApp\StartupScript.cmd" }
}

@"
<Configuration>
  <Networking>Enable</Networking>
  <vGPU>Disable</vGPU>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$appDir</HostFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>$command</Command>
  </LogonCommand>
</Configuration>
"@ | Set-Content $wsbPath -Encoding UTF8

if ($NoLaunch) {
    Write-Host "-NoLaunch 지정 — 실행하지 않고 종료합니다."
    Write-Host "  wsb      : $wsbPath"
    Write-Host "  boot.log : $bootLog"
    return
}

# WindowsSandboxServer 는 세션이 없어도 상주할 수 있으므로 판정에서 제외한다.
# 실제 세션이 살아 있는지는 RemoteSession/Client 프로세스로 본다.
if (Get-Process -Name 'WindowsSandboxRemoteSession', 'WindowsSandboxClient' -ErrorAction SilentlyContinue) {
    throw "Windows Sandbox 세션이 이미 실행 중입니다. 닫고 다시 시도하세요(동시에 하나만 가능)."
}

Write-Host "[4/4] 샌드박스 실행. 부팅 체인 진행 상황은 아래 파일에 실시간으로 쌓입니다."
Write-Host "  boot.log : $bootLog"
Start-Process $wsbPath
