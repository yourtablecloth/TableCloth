# 식탁보 Quick Start Guide

> 기준일: 2026-10-28 (Asia/Seoul). 대상: TableCloth `v1.22.0-preview.10`의 Windows Sandbox 실행 경로. 정식 배포와 구분한 평가용 Preview입니다.
>
> 검증 범위: GitHub 연결로 태그 소스, UI 문구, 릴리스 자산 이름·SHA-256 메타데이터와 동작 흐름을 대조했습니다. 공식 x64 Portable ZIP을 실제 내려받아 SHA-256 일치와 루트·current 폴더의 두 TableCloth.exe 서명 `Valid`, 파일 버전 `1.22.0.0`, 제품 버전 `1.22.0+85c48a99ff75ab971ed913f7db18a29c2ada6eea`를 확인했습니다. ARM64 패키지 검사와 앱 설치·실제 Sandbox 및 정부24 실행은 수행하지 않았습니다. 아래 성공 기준을 대상 PC에서 직접 확인하고 결과를 기록하세요.

## 목표와 성공 기준

Windows 호스트에서 식탁보를 실행하고, 격리한 Windows Sandbox 안의 Spork에서 **정부24**를 선택해 카탈로그가 지정한 소프트웨어 설치와 사이트 열기까지 확인합니다. 로그인, 인증서 사용, 민원 신청이나 결제는 첫 확인에 필요하지 않습니다.

다음을 모두 확인하면 이 가이드의 첫 결과를 얻은 것입니다.

1. Windows Sandbox 안에 Spork 카탈로그가 표시됩니다.
2. 정부24의 설치 단계에 실패가 없고, 설치 완료 상태를 확인합니다.
3. 게스트 브라우저가 카탈로그의 정부24 주소 `https://www.gov.kr/` 또는 해당 기관이 안내하는 공식 이동 주소를 엽니다.
4. Sandbox를 닫았다가 새로 시작하면 이전 게스트의 설치 상태가 유지되지 않습니다. 선택한 공유 Data 폴더의 즐겨찾기·사용 기록 등은 남을 수 있습니다.

**웹페이지 표시만으로 설치 성공을 판정하지 마세요.** Spork 소스는 설치에 실패해도 사용자가 문제를 확인하도록 사이트를 열 수 있습니다. 로그인·인증·민원 발급의 정상 동작은 별도 검증 대상입니다. [설치 결과 처리 소스](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/Spork.App/ViewModels/MainWindowViewModel.cs)

## 1. 준비물 확인

프로젝트가 지원하는 환경은 **Windows 11 Pro, Education 또는 Enterprise, x64/ARM64, Windows Sandbox, 인터넷 연결**입니다. Windows Home과 Windows 10은 이 가이드의 지원 범위에서 제외합니다. 개발용 .NET SDK나 Visual Studio는 공식 배포본 실행에 필요하지 않습니다. [지원 환경](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/README.md#지원-환경)

Windows Sandbox의 Microsoft 최소 요건은 가상화 활성화, RAM 4GB, 여유 저장 공간 1GB, CPU 2코어입니다. RAM 8GB와 SSD를 권장합니다. 실제 사이트 설치·브라우저 사용을 위한 여유 자원도 확보하세요. [Microsoft 설치 문서](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)

먼저 PowerShell에서 다음 **조회 명령**을 실행합니다. 아래 명령은 기능을 켜거나 설정을 바꾸지 않습니다.

```powershell
Get-ComputerInfo -Property WindowsProductName, WindowsVersion, OsBuildNumber, OsArchitecture
Get-CimInstance Win32_Processor | Select-Object Name, VirtualizationFirmwareEnabled
[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
Test-Path -LiteralPath "$env:WINDIR\System32\WindowsSandbox.exe"
```

관리자 PowerShell에서 기능 상태도 확인합니다.

```powershell
Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM |
    Select-Object FeatureName, State
```

기대 결과는 Windows 11 지원 에디션, `X64` 또는 `Arm64`, Sandbox 기능 `Enabled`입니다. 하이퍼바이저나 VM 구성에 따라 가상화 조회 값이 실제 사용 가능 여부를 충분히 설명하지 못할 수 있으므로, **시작 메뉴의 Windows Sandbox를 단독 실행해 바탕 화면이 뜨는지** 확인한 뒤 그 창을 닫습니다. 식탁보를 시작하기 전에 다른 Sandbox를 닫아 두세요.

기능이 꺼져 있으면 조직 정책을 확인한 후 **Windows 기능 켜기/끄기 > Windows Sandbox**를 선택하고, 요청하면 재부팅합니다. BIOS/UEFI의 가상화가 꺼져 있으면 장비 제조사 절차로 활성화합니다. 관리형 PC는 관리자 정책을 따릅니다. [Microsoft 절차](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)

## 2. 설치: 버전을 고정한 Portable 배치

첫 평가는 파일을 직접 배치하는 Portable 방식을 사용합니다. Portable은 자동 업데이트를 제공하지 않으므로, 확인한 버전과 설치 파일을 기록하기 쉽습니다. 설치 관리자 방식을 선호하면 아래 대안 절차를 사용하세요.

### 2-1. 공식 자산 다운로드와 해시 확인

일반 PowerShell에서 아래 블록을 한 번 실행합니다. 아키텍처를 자동 구분하고, 공식 `v1.22.0-preview.10` ZIP의 GitHub 자산 메타데이터와 SHA-256을 비교합니다. ARM64에서는 ARM64 패키지를 선택합니다.

```powershell
$evalArch = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) {
    'X64'   { 'x64' }
    'Arm64' { 'arm64' }
    default { throw '이 가이드는 Windows x64와 ARM64만 지원합니다.' }
}
$evalTag = 'v1.22.0-preview.10'
$evalName = "TableCloth-Preview_1.22.0.0_Release_${evalArch}_Portable.zip"
$evalRoot = Join-Path $env:LOCALAPPDATA 'TableCloth-Evaluation'
$evalZip = Join-Path $evalRoot $evalName
$evalApp = Join-Path $evalRoot "preview.10-$evalArch"
$evalHashes = @{
    x64 = 'e396674e8755ba0d532c0d97e8f1ae5a0cc4a0275c6da3d4dc0d1f07c3cf2674'
    arm64 = 'bbd1b994de44df8866f133ea3010a75838b2bc64e93d4803d4b64ef84e964fa6'
}
if (Test-Path -LiteralPath $evalApp) {
    throw "배치 폴더가 이미 있습니다. 기존 결과를 보존하고 다른 빈 폴더를 지정하세요: $evalApp"
}
New-Item -ItemType Directory -Path $evalRoot -Force | Out-Null
$evalUrl = "https://github.com/yourtablecloth/TableCloth/releases/download/$evalTag/$evalName"
Invoke-WebRequest -Uri $evalUrl -OutFile $evalZip
$evalHash = (Get-FileHash -LiteralPath $evalZip -Algorithm SHA256).Hash
if ($evalHash -ne $evalHashes[$evalArch]) {
    throw 'SHA-256 불일치. 압축을 풀거나 실행하지 말고 공식 릴리스를 다시 확인하세요.'
}
Expand-Archive -LiteralPath $evalZip -DestinationPath $evalApp
$evalExe = Join-Path $evalApp 'TableCloth.exe'
$evalPayloadExe = Join-Path $evalApp 'current\TableCloth.exe'
if (-not (Test-Path -LiteralPath $evalExe -PathType Leaf) -or
    -not (Test-Path -LiteralPath $evalPayloadExe -PathType Leaf)) {
    throw 'Portable 런처 또는 current 폴더의 실행 파일이 없습니다. 공식 배포 파일을 확인하세요.'
}
Get-Item -LiteralPath $evalExe | Select-Object FullName, @{Name='FileVersion';Expression={$_.VersionInfo.FileVersion}}
foreach ($evalFile in @($evalExe, $evalPayloadExe)) {
    $evalSignature = Get-AuthenticodeSignature -LiteralPath $evalFile
    $evalSignature | Select-Object Path, Status, StatusMessage, @{Name='Publisher';Expression={$_.SignerCertificate.Subject}}
    if ($evalSignature.Status -ne 'Valid') {
        throw '실행 파일 서명을 유효하다고 확인하지 못했습니다. 상태와 공식 배포 파일을 확인하세요.'
    }
}
```

다운로드가 실패하면 명령을 건너뛰지 말고 네트워크·프록시와 GitHub 접근을 확인합니다. 해시가 일치하고 두 서명이 `Valid`이면 다음 단계로 진행합니다. 확인한 x64 패키지의 서명 게시자는 `Open Source Developer, Jung Hyun Nam`입니다. Portable의 루트 `TableCloth.exe`는 런처이며 실제 프로그램은 `current\TableCloth.exe`에 있습니다. 실행할 때는 루트 런처를 사용합니다. 파일 버전은 `1.22.0.0`처럼 표시되므로 Preview 순번은 **다운로드한 태그와 ZIP 이름·해시**로 함께 구분합니다.

- [preview.10 릴리스와 전체 자산](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.22.0-preview.10)
- [x64 Portable ZIP](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_x64_Portable.zip)
- [ARM64 Portable ZIP](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_arm64_Portable.zip)
- [자산 메타데이터 API](https://api.github.com/repos/yourtablecloth/TableCloth/releases/tags/v1.22.0-preview.10)

### 대안: 설치 관리자 또는 정식 버전

설치 관리자는 [x64 EXE](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_x64.exe) 또는 [ARM64 EXE](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_arm64.exe)를 공식 릴리스에서 내려받아 서명과 게시자를 확인한 뒤 실행합니다. 이후 시작 메뉴에서 식탁보를 엽니다.

이미 Preview를 사용했다면 **설치 관리자를 덮어 실행하기 전 릴리스 상단의 AI 데이터 백업 안내를 먼저 읽으세요.** preview.10은 AI 저장 위치를 `%LOCALAPPDATA%\TableCloth.Data\ManagedAi`로 옮겼습니다. 이전 Setup이 삭제한 데이터는 자동 이전으로 복구하지 못합니다. 새 PC 첫 평가에는 해당하지 않습니다.

2026-10-28 기준 최신 정식 버전은 [v1.21.1](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.21.1)입니다. 정식 설치를 원하면 아래 WinGet 명령을 사용할 수 있으나, 이 명령은 **preview.10 고정 설치가 아닙니다**. 정식 버전에는 이 가이드의 최신 빠른 시작 UI나 Preview 기능이 다를 수 있습니다.

```powershell
winget install --exact --id TableClothProject.TableCloth
```

`/releases/latest`는 정식 Retail을 가리키며 Preview를 포함하지 않습니다. Preview에서 정식으로 돌아가려면 앱의 업데이트 채널을 **안정**으로 바꾼 뒤 정식 설치 관리자를 직접 실행합니다. 채널 변경만으로 자동 다운그레이드하지 않습니다. [채널 안내](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/docs/RELEASE_CHANNELS.md)

## 3. 실행과 공유 범위 설정

Portable 배치 블록을 실행한 **같은 PowerShell 창**에서 시작합니다. 새 창이라면 탐색기에서 방금 배치한 폴더의 `TableCloth.exe`를 실행하세요.

```powershell
Start-Process -FilePath $evalExe
```

`Spork.exe`를 호스트에서 따로 실행하거나 `TableCloth.exe spork`로 시작하지 마세요. Spork는 설치 단계를 실행하는 도구이며, 이 가이드에서는 TableCloth가 만든 **Sandbox 내부**에서만 사용합니다. 단독 Spork 실행 자체가 격리 환경을 만들지는 않습니다. [진입점 설명](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/DEVELOPMENT.md#프로젝트-구조)

첫 실행의 라이선스와 사용 주의 안내를 읽고 동의할 수 있는 경우에 진행합니다. 빠른 시작의 **옵션...**에서 다음과 같이 첫 확인용 구성을 잡습니다.

1. **인증서**: `공동인증서(NPKI) 폴더를 샌드박스와 공유`를 끕니다. 기본값은 켜짐이며, 이번 첫 확인에는 인증서가 필요하지 않습니다.
2. **사용자 폴더**: 별도 업무 폴더를 추가하지 않습니다.
3. **데이터 디렉터리**: 로컬 고정 디스크의 새 평가 전용 폴더를 지정합니다. 기본 경로는 문서 폴더 아래 `TableCloth\Data`이며, 문서 폴더가 OneDrive·네트워크로 이동된 PC라면 다른 로컬 폴더를 선택합니다. 아래 명령이 출력하는 경로를 사용할 수 있습니다.
4. **장치 공유**: 첫 확인에는 마이크·웹캠·프린터를 켜지 않습니다. GPU 가속은 기본 꺼짐으로 둡니다.

```powershell
Join-Path $env:LOCALAPPDATA 'TableCloth-Evaluation\Data'
```

일반 실행은 이 Data 폴더를 **읽기·쓰기**로 공유합니다. 앱은 호스트 설치 폴더의 복사본을 세션 `App` 폴더에 준비하며, preview.10의 실제 구성 코드는 이 세션 폴더도 **읽기·쓰기**로 매핑합니다. 코드 주석 일부의 읽기 전용 설명과 달라 실제 대입 값을 기준으로 적었습니다. Sandbox를 닫아도 호스트에 쓰인 Data와 선택 공유 폴더의 변경 사항은 남습니다. [빠른 시작 구성](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/TableCloth.App/ViewModels/QuickStartPageViewModel.cs), [실제 App 매핑](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/TableCloth.App/Components/Implementations/SandboxBuilder.cs#L247-L251)

빠른 시작의 입력칸을 **비워 둔 채 `시작`**을 누릅니다. 데이터 디렉터리 생성 확인이 나오면 방금 선택한 평가 경로인지 확인하고 만듭니다. Windows Sandbox가 열리고 Spork의 사이트 목록이 표시될 때까지 기다립니다. 빈 입력은 기관을 미리 선택하지 않은 Sandbox를 시작합니다.

## 4. 첫 결과 확인: 정부24 설치와 사이트 열기

1. **Windows Sandbox 창 안**의 Spork에서 `정부24`를 검색합니다.
2. 표시한 기관 이름과 대표 주소가 정부24·`https://www.gov.kr/`인지 확인하고 선택합니다. 주의 안내가 있으면 읽습니다.
3. `선택한 사이트 설치 진행` 창의 각 단계를 확인합니다. 설치 프로그램이 약관 동의·권한 확인을 요구하면 **게스트 안에서** 내용을 확인하고 진행합니다.
4. 성공 시 진행 창이 자동으로 닫히고 사이트가 열리는 흐름입니다. 단계에 `설치 실패`가 있으면 결과와 오류를 기록하고 성공으로 체크하지 않습니다. 카탈로그의 설치 완료 배지도 확인합니다.
5. 게스트 브라우저의 주소와 사이트 표시를 확인합니다. 여기서 멈춰도 첫 결과는 확인할 수 있습니다. 실제 로그인과 업무 처리는 기관의 지원 정책·인증 방식으로 별도 검증합니다.

preview.10이 참조한 카탈로그 커밋 `30a8ad90c3557db849ba866c9d3905e5645d286b`에는 `Gov24` 서비스와 `AnySign`, `TouchEnKey32` 패키지가 등록되어 있습니다. 실행 중 내려받는 원격 카탈로그는 이후 바뀔 수 있으므로, 실제 화면의 카탈로그 버전·패키지와 결과를 기록합니다. 카탈로그 등록은 해당 기관의 모든 업무나 현재 설치 URL 정상 동작을 보장하지 않습니다. [참조 카탈로그](https://github.com/yourtablecloth/TableClothCatalog/blob/30a8ad90c3557db849ba866c9d3905e5645d286b/docs/Catalog.xml)

격리 초기화는 공유 폴더와 구분해 확인합니다. **게스트 안의 PowerShell**에서 아래 명령으로 게스트 바탕 화면에 표시 파일을 하나 만듭니다. 호스트 PowerShell에서 실행하지 마세요.

```powershell
$guestMarker = Join-Path ([Environment]::GetFolderPath('Desktop')) 'tablecloth-session-check.txt'
New-Item -ItemType File -Path $guestMarker | Out-Null
Test-Path -LiteralPath $guestMarker
```

마지막 결과가 `True`인지 확인합니다. Sandbox를 닫고 종료 안내에 응답한 뒤, 식탁보에서 다시 빈 Sandbox를 시작합니다. 새 게스트의 바탕 화면에 표시 파일이 없는지 확인하고 정부24 설치 상태도 다시 확인합니다. 공유 Data의 즐겨찾기·사용 기록은 남을 수 있으므로 설치 배지만으로 게스트 초기화를 판정하지 않습니다. 중요한 파일을 게스트 바탕 화면에만 두지 마세요.

### 결과 기록 양식

```text
일시 / Windows 에디션·빌드 / CPU:
다운로드 태그 / ZIP 파일명 / SHA-256:
TableCloth 파일 버전 / 서명 상태:
Spork·카탈로그 버전:
Sandbox 단독 실행: 성공 / 실패
Spork 카탈로그 표시: 성공 / 실패
정부24 패키지 단계: 각 패키지명과 성공 / 실패
게스트 브라우저 주소·첫 화면: 확인 / 실패
새 Sandbox의 게스트 바탕 화면 표시 파일 소멸: 확인 / 미확인
새 Sandbox에서 설치 상태: 확인 / 미확인
공유 Data 잔존 여부:
실패 단계·오류 메시지:
```

## 5. 문제가 생겼을 때

| 증상 | 확인할 항목 |
| --- | --- |
| Windows Sandbox가 없음·시작 불가 | Windows 11 지원 에디션, BIOS 가상화, 기능 `Enabled`, 재부팅, 조직 정책을 확인합니다. 먼저 Sandbox 단독 실행을 확인합니다. |
| 식탁보가 이미 실행 중인 Sandbox를 경고 | 기존 Sandbox 창을 종료한 뒤 다시 시작합니다. |
| Data 폴더 오류 | `옵션... > 데이터 디렉터리`에서 로컬 고정 디스크의 쓰기 가능한 평가 폴더를 지정합니다. |
| Sandbox는 열리지만 Spork가 안 뜸 | 호스트 `%LOCALAPPDATA%\TableCloth.Data\ApplicationLog.jsonl`과 평가 Data의 `tablecloth-boot.log` 존재 여부를 확인합니다. 실패가 게스트 부팅 전인지 앱 준비 중인지 구분합니다. |
| 설치 실패·사이트는 열림 | 설치 실패를 그대로 기록합니다. 카탈로그·패키지 URL·네트워크·기관별 호환성을 확인하고 사이트 표시만으로 성공 처리하지 않습니다. |
| 관리형 네트워크 HTTPS 실패 | 관리자에게 프록시·TLS 검사·허용 도메인 정책을 확인합니다. Portable은 ZScaler 루트 인증서 전파 경로를 지원하지 않습니다. 설치형 옵션의 적용 범위와 실제 조직 환경 검증은 별도입니다. |
| 공유 폴더 실행에서 호스트 중지 오류 | 반복 실행을 멈추고 Windows·보안 제품·오류 시각을 기록합니다. 공식 Safe Mode 비교 절차를 따릅니다. 드라이버를 임의로 제거하지 않습니다. |

[Safe Mode 가이드](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/docs/SAFE_MODE.ko.md)의 빠른 시작 링크 **샌드박스를 시작하는데 문제가 있으신가요?**는 폴더·인증서·클립보드·장치 공유를 제외한 진단용 실행을 제공합니다. 일반 실행의 기관 입력과 옵션을 전달하지 않으며, 게스트는 최신 정식 Spork를 다운로드하므로 호스트 Preview와 버전이 다를 수 있습니다. 기능 제한을 확인하고 별도 결과로 기록합니다.

공식 문의는 [GitHub Issues](https://github.com/yourtablecloth/TableCloth/issues)입니다. 오류 메시지, Windows/CPU/앱 버전, 실패 단계와 재현 절차를 남깁니다. 인증서 파일·개인키·로그인 정보와 민감한 업무 화면은 공개 이슈에 넣지 마세요.

## 6. 기업 평가의 다음 단계

첫 확인을 통과하면 실제 대상 사이트·인증수단·보안 에이전트·프록시·출력 장치 조합을 별도로 시험합니다. 공유 폴더와 로그 보존 범위, 업데이트·복귀 절차, 카탈로그 유지 책임도 결정해야 합니다. 프로젝트는 AGPL-3.0과 별도 상용 라이선스를 안내하며, 배포·변경·운영 방식에 맞는 조건을 확인합니다. [라이선스](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/README.md#라이선스)

이 가이드의 첫 실행에는 식탁보 AI 로그인이나 스킬 설치가 필요하지 않습니다. AI는 Preview의 별도 기능이며, 일반적인 사이트 설치 경로의 검증과 구분합니다.

## 소스와 재현 범위

- [고정한 Preview 릴리스](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.22.0-preview.10)
- [태그 README](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/README.md)
- [빠른 시작 라우팅](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/TableCloth.App/Pages/QuickStartPage.axaml.cs)
- [빠른 시작 구성](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/TableCloth.App/ViewModels/QuickStartPageViewModel.cs)
- [호스트 경로 정의](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/TableCloth.App/Components/Implementations/SharedLocations.cs)
- [설치 결과 처리](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/Spork.App/ViewModels/MainWindowViewModel.cs)
- [한국어 UI 문구](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/src/TableCloth.Core/Resources/UIStringResources.ko.resx)
- [Retail·Preview 채널](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/docs/RELEASE_CHANNELS.md)
- [Microsoft Windows Sandbox 설치](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)

문서와 실행 화면이 다르면, 이 가이드가 고정한 태그와 실제 다운로드 버전부터 비교하세요. 저장소 HTML 설명서의 일부 UI·Windows 지원 문구는 현재 README·태그 소스와 다르므로 이 가이드는 후자를 기준으로 작성했습니다.
