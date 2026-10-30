# 식탁보 Quick Start Guide

2026년 10월 30일 기준으로 정식 [식탁보 v1.22.0](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.22.0)의 시작 절차를 안내합니다. Windows에서는 식탁보가 Windows Sandbox를 실행하고 그 안에서 Spork가 사이트 이용 환경을 준비합니다. Apple Silicon Mac에서는 macSandbox로 무설치 `.wsb` 설정 파일을 열어 Windows 가상머신 안에서 Spork를 실행할 수 있습니다.

이 문서는 OS와 하드웨어 조건, 설치, 실행, 첫 결과 확인을 다룹니다. 소스 빌드나 개발용 SDK 설치, 식탁보 AI 설정 없이 진행할 수 있습니다. [English guide](QUICK_START.md)

실행 환경부터 살펴보겠습니다. Windows 사용자는 다음 두 절을 진행하고 Mac 사용자는 [macOS 설치와 .wsb 실행](#macos-설치와-wsb-실행)으로 이동합니다. 두 경로 모두 [첫 결과 확인과 세션 종료](#첫-결과-확인과-세션-종료)로 이어집니다.

## 실행 환경 선택

Windows 설치형은 **Windows 11 Pro, Education 또는 Enterprise**, x64 또는 ARM64 프로세서와 인터넷 연결을 기준으로 안내합니다. Windows Home은 Windows Sandbox를 제공하지 않습니다. Microsoft는 Windows 10의 Sandbox도 설명하지만 이 가이드는 식탁보의 Windows 11 지원 기준을 따릅니다. 지원 에디션은 [Microsoft의 Windows Sandbox 안내](https://learn.microsoft.com/ko-kr/windows/security/application-security/application-isolation/windows-sandbox/)에서 확인할 수 있습니다.

이어서 **설정 > 시스템 > 정보**에서 에디션과 시스템 종류를 확인합니다. **작업 관리자 > 성능 > CPU**에서는 가상화 상태를 확인할 수 있습니다. 아래 하드웨어 조건은 [Microsoft의 Sandbox 설치 문서](https://learn.microsoft.com/ko-kr/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)를 따릅니다.

| 항목 | 최소 조건 | 추가 여유와 선택 기준 |
| --- | --- | --- |
| 아키텍처 | x64 또는 Windows 11 22H2 이상인 ARM64 | 호스트 아키텍처에 맞는 식탁보 설치 관리자 선택 |
| 가상화 | BIOS/UEFI에서 활성화 | Windows 자체가 VM이면 상위 호스트의 중첩 가상화 지원도 필요 |
| 메모리 | RAM 4 GB | Microsoft 권장 사양은 8 GB |
| 프로세서 | CPU 코어 2개 | Microsoft 권장 사양은 하이퍼스레딩을 지원하는 코어 4개 |
| 저장 공간 | Windows Sandbox용 여유 공간 1 GB | 식탁보, 다운로드와 사이트 프로그램용 추가 공간 확보, SSD 사용 권장 |

또한 Windows 기능을 켤 때 관리자 승인이 필요합니다. GitHub, [식탁보 카탈로그](https://catalog.yourtablecloth.app/)와 선택한 사이트의 다운로드 서버에도 접속할 수 있어야 합니다. 조직 정책이 해당 기능이나 접속을 제한할 수 있습니다. Mac의 하드웨어와 이미지 준비 조건은 macOS 절에서 설명합니다.

## Windows Sandbox 활성화와 단독 실행

식탁보를 설치하기 전에 Windows Sandbox를 준비하겠습니다. 단독 실행으로 가상화 환경을 먼저 확인하면 이후 식탁보의 준비 단계에서 발생한 문제를 구분할 수 있습니다. 절차는 [Microsoft의 설치 안내](https://learn.microsoft.com/ko-kr/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)를 따릅니다.

1. 가상화가 꺼져 있으면 PC의 BIOS/UEFI에서 활성화한 뒤 Windows를 다시 시작합니다.
2. 시작 메뉴에서 **Windows 기능 켜기/끄기**를 검색합니다. **Windows 샌드박스**를 선택하고 **확인**을 누릅니다. 다시 시작 안내가 나오면 PC를 다시 시작합니다.
3. 시작 메뉴에서 **Windows Sandbox** 또는 **Windows 샌드박스**를 실행합니다. 별도의 Windows 바탕 화면이 나타날 때까지 기다립니다.
4. 테스트용 샌드박스 창을 닫고 내용 삭제를 확인한 뒤 식탁보 설치로 진행합니다.

명령줄로 2번 단계를 진행하려면 **관리자 권한 PowerShell**에서 다음 명령을 실행합니다. 다시 시작 안내가 나오면 PC를 다시 시작합니다.

```powershell
Enable-WindowsOptionalFeature -Online -FeatureName "Containers-DisposableClientVM" -All
```

다만 기능 항목이 없거나 단독 실행에 실패하면 에디션, 가상화 설정과 정책 조건부터 확인합니다. 식탁보를 설치해도 Windows Home에 Sandbox 지원을 추가하지는 않습니다.

## Windows 설치와 실행

설치 파일은 공식 [GitHub Releases](https://github.com/yourtablecloth/TableCloth/releases/latest) 또는 WinGet 패키지 `TableClothProject.TableCloth`로 받습니다. WinGet을 사용할 수 있으면 터미널에서 다음 명령으로 설치합니다.

```powershell
winget install --exact --id TableClothProject.TableCloth
```

WinGet이 없거나 최신 버전을 찾지 못하면 GitHub Releases에서 설치 관리자를 내려받아 실행합니다. v1.22.0에서는 Intel/AMD x64 PC용 `TableCloth_1.22.0.0_Release_x64.exe` 또는 ARM64 PC용 `TableCloth_1.22.0.0_Release_arm64.exe`를 선택합니다. 호스트에는 **TableCloth**를 설치하며 Spork는 샌드박스 안에서 실행합니다.

1. 시작 메뉴에서 **TableCloth**를 실행합니다. 라이선스를 읽고 **동의합니다**를 선택합니다. 사용 시 주의 사항이 나타나면 내용을 읽고 동의합니다.
2. 첫 확인에서 인증서를 사용하지 않으면 **옵션... > 인증서**에서 **공동인증서(NPKI) 폴더를 샌드박스와 공유**를 해제하고 **닫기**를 누릅니다. 식탁보는 설정을 자동으로 저장합니다. 일반 실행은 기존 NPKI 폴더가 있으면 기본적으로 공유합니다.
3. **빠른 시작**에서 입력란을 비운 채 **시작**을 누릅니다. 데이터 폴더 생성 질문이 나오면 생성을 승인합니다. 기본 위치는 호스트의 Windows 문서 폴더 아래 `TableCloth\Data`이며 실제 경로는 **옵션... > 데이터 디렉터리 > 현재 경로**에서 확인할 수 있습니다.
4. Windows Sandbox가 열리고 Spork의 **웹 사이트** 카탈로그가 나타날 때까지 기다립니다. 이후 첫 결과 확인 절차로 진행합니다.

일반 실행에서는 데이터 폴더를 쓰기 가능한 상태로 공유합니다. 해당 폴더에 저장한 파일은 샌드박스를 닫아도 호스트에 남습니다. 실행과 폴더 처리 방식은 [빠른 시작 구현](../src/TableCloth.App/ViewModels/QuickStartPageViewModel.cs)에서 확인할 수 있습니다.

설치형 대신 무설치 경로를 선택할 수도 있습니다. Windows Sandbox를 활성화한 뒤 공식 [`no-install-spork.wsb`](https://github.com/yourtablecloth/TableCloth/releases/latest/download/no-install-spork.wsb)를 내려받아 두 번 누릅니다. 게스트가 SporkBootstrap과 Spork를 다운로드하므로 호스트에 식탁보를 설치하지 않아도 됩니다. 이 설정은 호스트 폴더를 공유하지 않아 호스트의 인증서 파일과 일반 실행의 데이터 폴더를 사용할 수 없습니다. 동작은 [무설치 설정 파일](../tools/no-install/no-install-spork.wsb)에 정의했습니다.

## macOS 설치와 .wsb 실행

Mac의 실행 경로를 정리하겠습니다. **Apple Silicon M1 이상**에서 [macSandbox for Windows](https://github.com/yourtablecloth/macSandbox)를 사용합니다. 현재 [공식 README](https://github.com/yourtablecloth/macSandbox/blob/main/README.md)는 **macOS Tahoe 26 이상**을 검증한 환경으로 안내하며 초기 기본 이미지 생성에 약 **24 GB의 여유 공간**을 제시합니다. Intel Mac은 지원하지 않습니다. macOS 14와 15는 빌드 대상에 포함하지만 HVF 동작은 검증하지 않았습니다.

1. [macSandbox 공식 릴리스](https://github.com/yourtablecloth/macSandbox/releases/latest)에서 `.dmg`를 내려받습니다. 파일을 열고 **macSandbox for Windows**를 **응용 프로그램**으로 옮깁니다. macOS가 첫 실행을 차단하면 공식 배포처를 확인한 뒤 **시스템 설정 > 개인정보 보호 및 보안 > 확인 없이 열기**로 해당 앱을 허용할 수 있습니다. [Apple 안내](https://support.apple.com/ko-kr/102445)에서 절차를 제공합니다.
2. [Microsoft 공식 페이지](https://www.microsoft.com/software-download/windows11arm64)에서 **Windows 11 ARM64 ISO**를 받습니다. macSandbox는 Windows나 Windows 사용 권한을 포함하지 않으며 ISO를 자동으로 다운로드하지 않습니다. 가상머신 사용에 적용할 수 있는 Windows 라이선스를 준비합니다.
3. macSandbox를 실행합니다. **Choose ISO…**에서 받은 ISO를 선택하고 **Edition to install**에서 **Windows 11 Pro**를 선택합니다. **Build Baseline**을 누른 뒤 라이선스 확인 항목을 읽고 동의하여 빌드를 시작합니다. 앱이 드라이버를 다운로드하고 Windows, 계정과 원격 데스크톱 연결을 준비합니다. [macSandbox 도움말](https://yourtablecloth.app/macSandbox/help/)은 첫 빌드에 20분에서 40분을 안내합니다.
4. 기본 이미지 생성 후 빈 Windows 세션이 열리면 **Sandbox > Stop Sandbox**로 종료합니다. 이후 실행에서도 기본 이미지를 사용하므로 이미지는 유지합니다.
5. 식탁보의 공식 [`no-install-spork.wsb`](https://github.com/yourtablecloth/TableCloth/releases/latest/download/no-install-spork.wsb)를 내려받습니다. Finder에서 **macSandbox for Windows**로 엽니다. 두 번 눌러도 해당 앱으로 열리지 않으면 **다음으로 열기**에서 앱을 선택합니다. 실행 중인 샌드박스 세션이 없는 상태에서 파일을 엽니다.
6. Windows 게스트, 다운로드 준비 창과 SporkBootstrap을 거쳐 Spork의 **웹 사이트** 카탈로그가 나타날 때까지 기다립니다. 이후 첫 결과 확인 절차로 진행합니다.

이 경로는 macSandbox의 Windows 게스트에서 Spork를 실행합니다. Mac에 Windows용 식탁보 호스트 앱을 설치하거나 게스트 안에서 Windows Sandbox를 활성화하는 절차는 없습니다. `.wsb`의 네트워크와 로그온 명령은 macSandbox의 [호환성 문서](https://github.com/yourtablecloth/macSandbox/blob/main/WSB-SUPPORT.md)에서 지원하는 기능에 해당합니다. 이 가이드는 문서상 연동 경로를 안내하며 Mac에서 전체 절차를 실행 검증한 기록을 뜻하지는 않습니다.

기본 무설치 설정은 Mac의 폴더나 인증서 파일을 공유하지 않습니다. macSandbox는 Windows Sandbox와 격리 및 장치 동작이 다르며 공유 폴더의 `ReadOnly` 강제와 `ProtectedClient`를 지원하지 않습니다. 이후 `.wsb`를 직접 수정할 때는 해당 차이를 기준으로 공유 범위를 판단할 수 있습니다.

## 첫 결과 확인과 세션 종료

두 실행 경로의 첫 결과를 확인하겠습니다. [Spork 화면](../src/Spork.App/MainWindow.axaml)은 사이트 카드를 표시하고 [사이트 선택 처리](../src/Spork.App/ViewModels/MainWindowViewModel.cs)는 카드를 누르면 설치를 시작합니다.

1. Spork의 **웹 사이트** 탭에 카탈로그 목록이 나타나는지 확인합니다. 검색창에서 이용할 기관이나 사이트를 찾습니다.
2. 사이트 카드 하나를 누릅니다. **Windows Sandbox 환경이 아닐 수 있습니다**라는 안내가 나오면 준비한 Windows 게스트 안에서 Spork를 실행 중인지 확인한 뒤 **확인**을 선택합니다. 이후 호환성 안내가 나오면 내용을 읽고 필요한 프로그램 설치를 기다립니다. 카드 선택 후 별도의 시작 버튼은 누르지 않습니다.
3. 설치 창의 **설치 완료** 또는 카드의 녹색 설치 완료 표시와 게스트 브라우저의 공식 사이트 주소를 함께 확인합니다. 설치에 성공하면 설치 창은 자동으로 닫힙니다. 브라우저가 열렸다는 사실만으로 설치 성공을 판단하지 않습니다.
4. 첫 확인은 공개 첫 페이지까지 진행합니다. 로그인, 인증서 사용, 결제와 실제 금융 및 행정 업무 완료는 이번 확인 범위에 포함하지 않습니다.
5. 보존할 결과가 있으면 게스트 밖에 저장한 뒤 세션을 종료합니다. Windows에서는 Windows Sandbox 창을 닫고 삭제를 확인합니다. macOS에서는 **Sandbox > Stop Sandbox**를 선택합니다.

이로써 카탈로그 표시, 선택한 사이트의 프로그램 준비와 게스트 안의 페이지 열기를 확인할 수 있습니다. 아래 이미지는 Spork 화면을 식별할 때 참고할 수 있습니다.

![Windows Sandbox 안에서 실행한 Spork](images/Spork-Windows-Sandbox.png)

세션을 종료하면 게스트 내부의 변경 사항을 폐기합니다. Windows 호스트의 쓰기 가능한 데이터 폴더는 남지만 무설치 경로에는 해당 공유 폴더가 없습니다. macOS의 기본 이미지는 직접 삭제하기 전까지 유지하며 세션의 변경 사항만 폐기합니다. 수명 주기는 [Windows Sandbox 안내](https://learn.microsoft.com/ko-kr/windows/security/application-security/application-isolation/windows-sandbox/)와 [macSandbox 세션 관리](https://yourtablecloth.app/macSandbox/help/)에서 설명합니다.

## 실패 단계에 따른 점검

실행이 멈춘 단계별로 점검 대상을 짚어보겠습니다. 기반 실행 환경의 문제는 [Windows Sandbox FAQ](https://learn.microsoft.com/ko-kr/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-faq)와 [macSandbox 도움말](https://yourtablecloth.app/macSandbox/help/)에서 확인할 수 있습니다.

- **Windows Sandbox 항목이 없거나 실행 실패**: Windows 에디션, BIOS/UEFI 가상화와 관리자 승인을 확인합니다. Windows 자체가 VM이면 중첩 가상화도 확인합니다. 식탁보 실행 전의 단독 테스트에서도 같은 문제가 발생하는지 구분합니다.
- **식탁보 데이터 폴더 경로 거부**: **옵션... > 데이터 디렉터리 > 폴더 선택...**에서 쓰기 권한이 있는 로컬 고정 디스크의 폴더를 선택한 뒤 **시작**을 다시 누릅니다. 일반 실행의 데이터 폴더로 네트워크나 이동식 디스크 경로를 지정할 수 없습니다.
- **.wsb 실행 후 Spork 미표시**: 게스트에서 GitHub에 접속할 수 있는지 확인합니다. 무설치 준비 창에 오류가 나오면 창을 닫기 전에 메시지를 기록합니다.
- **빈 카탈로그 또는 사이트 프로그램 설치 실패**: 게스트의 카탈로그 및 설치 서버 접속 여부를 확인하고 사이트 이름과 설치 오류를 기록합니다. 브라우저가 열려도 프로그램 설치 성공 여부를 따로 확인합니다.
- **Mac에서 .wsb를 열어도 새 세션 미시작**: 기존 세션을 종료한 뒤 파일을 macSandbox for Windows로 다시 엽니다. 기본 이미지 생성 실패는 macSandbox 준비 단계의 문제로 구분합니다.
- **Windows 일반 실행 중 호스트 중지 오류**: 빠른 시작의 **샌드박스를 시작하는데 문제가 있으신가요?** 링크에서 호스트 폴더를 공유하지 않는 Safe Mode를 비교할 수 있습니다. 제한 사항과 비교 절차는 [Safe Mode 가이드](SAFE_MODE.ko.md)에서 설명합니다.

식탁보 문제를 [GitHub Issues](https://github.com/yourtablecloth/TableCloth/issues)에 제보할 때는 OS 버전과 에디션, CPU 아키텍처, 식탁보 또는 Spork 버전, 선택한 실행 경로와 실패 단계를 포함합니다. 오류 메시지에 개인정보가 있으면 제거한 뒤 공유합니다.

## 첫 실행 점검 항목

공식 [릴리스 다운로드](https://github.com/yourtablecloth/TableCloth/releases/latest)와 앞선 절차를 기준으로 첫 실행 결과를 기록할 수 있습니다.

1. 지원 OS, 아키텍처, 가상화, 저장 공간과 네트워크 조건을 확인합니다.
2. Windows Sandbox 단독 실행 또는 macSandbox 기본 이미지 생성을 완료합니다.
3. Windows에 식탁보를 설치하거나 준비한 실행 환경에서 공식 무설치 `.wsb`를 엽니다.
4. Spork 카탈로그, 선택한 사이트의 설치 상태와 게스트 브라우저의 공식 페이지를 확인합니다.
5. 필요한 결과를 보존하고 임시 세션을 종료합니다.

## 첫 실행 확인의 범위

여기까지 정리하면 식탁보는 임시 Windows 게스트 안에 사이트 이용 환경을 준비합니다. 앞선 점검으로 선택한 환경과 사이트에서 준비와 페이지 열기가 동작하는지 확인할 수 있습니다. 사이트별 인증과 실제 업무 처리의 호환성은 별도로 확인할 대상으로 남습니다.

이후 호스트 폴더와 인증서 옵션을 사용하려면 Windows 설치형을 선택할 수 있습니다. 해당 공유 없이 게스트에서 시작하려면 무설치 `.wsb` 경로를 사용할 수 있습니다. 기반 실행 환경의 호환성과 사이트별 설치 및 인증 문제를 구분해 기록하면 [식탁보 지원 채널](../README.md#사이트-정보-수정과-문제-제보)에서 조사 범위를 좁힐 수 있습니다.
