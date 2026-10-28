# TableCloth Quick Start Guide

이 가이드는 처음 사용하는 Windows 엔지니어가 TableCloth를 설치하고, 격리된 브라우저 실행과 세션 폐기를 직접 확인하는 절차입니다. 기본 실습에는 ChatGPT 로그인이나 공동인증서가 필요하지 않습니다.

- 작성: 남정현
- 기준: 2026-10-28, **v1.22.0-preview.10**
- 소스 커밋: [`85c48a99ff75ab971ed913f7db18a29c2ada6eea`](https://github.com/yourtablecloth/TableCloth/commit/85c48a99ff75ab971ed913f7db18a29c2ada6eea)
- [고정 버전 다운로드와 릴리스 노트](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.22.0-preview.10)
- [프로젝트](https://github.com/yourtablecloth/TableCloth) · [문제 제보](https://github.com/yourtablecloth/TableCloth/issues)

> 이 버전은 develop 계열의 **프리뷰**입니다. 현재 정식판은 [v1.21.1](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.21.1)이며, 프리뷰의 기능과 지원 범위는 정식 출시 전에 달라질 수 있습니다. 이 문서는 공개 소스와 릴리스 설명을 기준으로 작성했으며 모든 PC·기관 사이트의 동작을 보장하지 않습니다.

## 1. 설치 전 준비

### 지원 환경 확인

필수 조건은 **Windows 11 Pro, Education 또는 Enterprise**, x64 또는 ARM64 CPU, Windows Sandbox와 인터넷 연결입니다. Windows Home은 지원 대상이 아닙니다. 회사 정책에서 가상화·Windows Sandbox·다운로드를 막은 PC라면 해당 정책에 맞게 관리자의 환경 준비가 먼저 필요합니다.

1. **설정 > 시스템 > 정보**에서 Windows 에디션과 시스템 종류를 확인합니다. Intel·AMD 기반 64비트 PC는 x64, Windows on ARM 장치는 ARM64 파일을 선택합니다. 아래 PowerShell 명령으로도 아키텍처를 확인할 수 있습니다.

   ```powershell
   [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
   ```

2. **작업 관리자 > 성능 > CPU**에서 **가상화: 사용**을 확인합니다. 꺼져 있으면 PC 제조사 안내에 따라 UEFI/BIOS에서 가상화를 켭니다. 가상 머신 안에서 실습하려면 호스트가 중첩 가상화를 지원하고 허용해야 합니다.
3. Windows Sandbox의 최소 요구 사항은 RAM 4GB, 여유 디스크 1GB, CPU 코어 2개입니다. Microsoft는 RAM 8GB와 SSD를 권장합니다. 보안 프로그램 설치와 다운로드에는 추가 공간이 필요합니다.

출처: [TableCloth 지원 환경](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/README.md#지원-환경), [Microsoft Windows Sandbox 설치 안내](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install).

### Windows Sandbox 켜기

1. 시작 메뉴에서 **Windows 기능 켜기/끄기**를 검색해 엽니다.
2. **Windows 샌드박스**를 선택하고 확인을 누릅니다.
3. 재시작 안내가 나오면 작업을 저장한 뒤 PC를 재시작합니다.
4. 시작 메뉴에서 **Windows Sandbox**를 실행합니다. 별도 Windows 바탕 화면이 열리면 기반 환경 준비가 끝났습니다. 창을 닫아 테스트 세션을 종료합니다.

명령으로 설정하려면 **관리자 권한 PowerShell**에서 다음을 실행하고 재시작 요청을 따릅니다.

```powershell
Enable-WindowsOptionalFeature -FeatureName "Containers-DisposableClientVM" -All -Online
```

기능 상태도 관리자 PowerShell에서 확인할 수 있습니다.

```powershell
Get-WindowsOptionalFeature -Online -FeatureName "Containers-DisposableClientVM"
```

예상 상태는 `Enabled`입니다. 기능을 켠 뒤 재시작을 완료했는데 Windows Sandbox 자체가 열리지 않으면 TableCloth 설치보다 Windows 가상화 환경을 먼저 점검합니다.

## 2. TableCloth 설치

### 이전 프리뷰 사용자는 먼저 AI 데이터를 보존

새 설치라면 다음 다운로드 단계로 진행합니다. 이전 Preview를 사용했다면 **Setup을 실행하기 전에** TableCloth를 종료하고, `%LOCALAPPDATA%\TableCloth\ManagedAi` 폴더가 있을 경우 설치 폴더 밖의 개인 보관 위치에 백업합니다. 전체 Setup은 이전 설치 루트를 교체하므로 이 폴더의 로그인·스킬·런타임 데이터를 잃을 수 있습니다. 백업에는 인증정보가 포함될 수 있습니다.

Preview 10의 새 AI 데이터 위치는 `%LOCALAPPDATA%\TableCloth.Data\ManagedAi`입니다. 남아 있는 이전 저장소는 앱이 조건을 확인해 이전하지만, 양쪽에 데이터가 있으면 덮어쓰지 않습니다. 이미 삭제한 프로필을 복구하지는 못합니다. 자세한 조건은 [릴리스 노트](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.22.0-preview.10)를 확인합니다.

### 고정 버전 다운로드

자신의 아키텍처에 맞는 **TableCloth** 파일 하나를 선택합니다. Spork 파일은 게스트용이므로 처음 설치할 호스트 앱 대신 선택하지 않습니다.

| PC 아키텍처 | 설치 관리자 | 포터블 ZIP |
| --- | --- | --- |
| x64 | [x64 설치](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_x64.exe) | [x64 포터블](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_x64_Portable.zip) |
| ARM64 | [ARM64 설치](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_arm64.exe) | [ARM64 포터블](https://github.com/yourtablecloth/TableCloth/releases/download/v1.22.0-preview.10/TableCloth-Preview_1.22.0.0_Release_arm64_Portable.zip) |

이 실습에서는 재현성을 위해 위 태그를 직접 사용합니다. `winget install --exact --id TableClothProject.TableCloth`와 GitHub의 `releases/latest`는 정식 배포 경로이므로 이 프리뷰를 설치하는 명령·링크로 사용하지 않습니다.

### 서명 확인 후 실행

다운로드한 EXE의 **속성 > 디지털 서명**에서 서명을 선택하고 서명 정보를 확인합니다. PowerShell에서는 다운로드 폴더로 이동한 뒤 아래처럼 확인할 수 있습니다. ARM64는 파일 이름의 `x64`를 `arm64`로 바꿉니다.

```powershell
Get-AuthenticodeSignature -LiteralPath '.\TableCloth-Preview_1.22.0.0_Release_x64.exe' |
    Format-List Status, StatusMessage, SignerCertificate
```

예상 `Status`는 `Valid`입니다. 서명이 없거나 유효하지 않으면 실행하지 말고 공식 태그에서 다시 다운로드합니다. 조직 PC에서 검증에 실패하면 인증서 신뢰·네트워크 정책을 점검합니다. 보안 경고를 일괄 해제하는 방식으로 진행하지 않습니다.

- **설치 관리자:** 검증한 EXE를 실행하고 설치 안내를 따른 뒤 TableCloth를 엽니다.
- **포터블:** ZIP을 쓰기 가능한 별도 폴더에 모두 풀고, 안의 `TableCloth.exe` 서명을 확인한 뒤 실행합니다. ZIP 안에서 바로 실행하지 않습니다.

실행되면 정보 화면의 `1.22.0` 버전과 프리뷰 채널을 확인합니다. 파일 버전만으로 `preview.10`을 구별할 수 없으므로 사용한 다운로드 태그도 함께 기록합니다. 같은 Preview 계열에서도 파일 버전 `1.22.0.0`을 공유할 수 있습니다.

출처: [Preview 10 배포 자산](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.22.0-preview.10), [릴리스 채널 정책](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/docs/RELEASE_CHANNELS.md).

## 3. 실행과 첫 결과 확인

### A. 로그인 없이 브라우저 격리 확인

1. TableCloth의 **빠른 시작** 통합 입력란에 `https://example.com`을 입력합니다.
2. **시작**을 누릅니다. 이 주소는 기준일에 서비스 카탈로그에 없는 공개 웹 주소이므로 폴더와 인증서를 공유하지 않는 브라우저 전용 Windows Sandbox에서 열립니다.
3. Windows Sandbox 창 안의 브라우저에서 **Example Domain** 페이지를 확인합니다. 호스트 브라우저 창과 혼동하지 않도록 Sandbox 창 제목을 함께 확인합니다.

**첫 성공 기준:** 호스트에서 TableCloth를 실행했고, 별도 Windows Sandbox 안의 브라우저에 지정한 공개 페이지가 나타납니다. 이 결과는 브라우저 실행과 네트워크 연결을 확인하며, 모든 기관 사이트의 보안 프로그램 호환성을 입증하지는 않습니다.

> 카탈로그는 앱과 별도로 갱신됩니다. 나중에 이 주소가 카탈로그에 등록되어 서비스 선택창이 나타나면 현재 카탈로그에서 미등록인 공개 웹 주소로 동일 절차를 확인합니다. 로컬·사설 IP, 로그인 정보가 포함된 URL, 웹 이외의 스킴은 빠른 시작 입력 대상으로 사용할 수 없습니다.

### B. 세션을 닫으면 게스트 파일이 사라지는지 확인

1. 방금 열린 **Windows Sandbox 안에서** `Win+R`을 누르고 `powershell`을 실행합니다.
2. 아래 두 줄을 실행합니다. 이 경로는 게스트의 바탕 화면이며 호스트 폴더가 아닙니다.

   ```powershell
   Set-Content -LiteralPath "$env:USERPROFILE\Desktop\TableCloth-first-result.txt" -Value 'guest-only'
   Test-Path -LiteralPath "$env:USERPROFILE\Desktop\TableCloth-first-result.txt"
   ```

   예상 결과는 `True`입니다.

3. **Windows Sandbox 창 전체를 닫아** 세션을 종료합니다. 브라우저만 닫거나 게스트 Windows만 다시 시작하는 절차와 구분합니다.
4. TableCloth에서 같은 주소로 새 Sandbox를 시작합니다. 새 게스트의 PowerShell에서 다음을 실행합니다.

   ```powershell
   Test-Path -LiteralPath "$env:USERPROFILE\Desktop\TableCloth-first-result.txt"
   ```

   예상 결과는 `False`입니다. 앞선 게스트의 파일이 새 세션에 남지 않았음을 확인한 것입니다.

**실습 완료 기록:** 사용 태그, Windows 에디션·빌드, x64/ARM64, Sandbox 안의 페이지 제목, 종료 전 `True`와 새 세션의 `False`를 기록합니다. 실제 실행 결과를 기록하며 예상값을 측정값으로 적지 않습니다.

### C. 실제 기관 사이트의 보안 프로그램 설치 확인

1. 테스트 Sandbox를 닫고 TableCloth에서 **카탈로그에서 사이트 선택**을 엽니다.
2. **국세청 홈택스**를 찾아 선택하고 실행합니다. 현재 카탈로그의 해당 서비스 주소는 `https://hometax.go.kr/`입니다. 빠른 시작에서 기관명을 입력해 제안을 선택한 후 **시작**을 눌러도 됩니다.
3. Sandbox 안에서 **Spork**가 선택한 서비스의 보안 프로그램을 설치하는 과정을 확인합니다. 외부 다운로드와 설치에 시간이 걸릴 수 있으며 설치 프로그램이 추가 동작을 요구할 수 있습니다.
4. Sandbox 브라우저에서 홈택스 페이지가 열리는지 확인합니다. 설치 실패가 있으면 성공으로 간주하지 말고 실패한 패키지와 메시지를 기록합니다.

**시나리오 성공 기준:** Spork의 설치 결과와 Sandbox 안의 대상 사이트 표시를 확인합니다. 첫 실습에서는 로그인·인증서 복사·실제 신고를 수행하지 않아도 됩니다. 실제 인증 업무는 해당 사이트가 지원하는 인증 수단과 필요한 공유 범위를 확인한 뒤 별도로 시험합니다.

기관 카탈로그 실행은 위 A의 브라우저 전용 경로와 공유 정책이 다릅니다. 공동인증서와 데이터 폴더·장치 공유가 필요한 경우 해당 범위만 허용합니다. 쓰기 가능한 호스트 공유 폴더에 저장한 결과는 Sandbox를 종료해도 호스트에 남으므로, 중요 파일을 공유한 상태에서 폐기 테스트를 하지 않습니다.

출처: [빠른 시작과 Spork 동작](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/README.md#프로젝트-개요), [실시간 서비스 카탈로그](https://github.com/yourtablecloth/TableClothCatalog/blob/main/docs/Catalog.xml).

## 4. 자주 막히는 지점

| 증상 | 확인과 다음 조치 |
| --- | --- |
| Windows Sandbox 항목이 없음 | Windows 에디션, CPU 가상화와 관리자 정책을 확인합니다. Home에서 비공식 활성화 스크립트로 진행하지 않습니다. |
| Sandbox 자체가 열리지 않음 | PC 재시작을 완료하고 Windows Sandbox를 시작 메뉴에서 단독 실행합니다. 이 단계의 오류와 TableCloth 오류를 구분합니다. |
| 앱이 시작되지 않음 | x64/ARM64 파일 선택, 압축 전체 해제, 실행 파일 서명과 조직의 실행 정책을 확인합니다. |
| 다운로드·페이지 열기 실패 | 인터넷, DNS, GitHub 접근과 사내 프록시·TLS 검사 정책을 확인합니다. 인증서 검증을 끄지 않습니다. |
| 기관 사이트의 패키지 설치 실패 | 기관명, 실패한 패키지, 오류 시각·메시지를 기록합니다. 카탈로그와 외부 설치 파일 변경에 따른 실패는 [카탈로그 이슈](https://github.com/yourtablecloth/TableClothCatalog/issues)에 제보합니다. |
| 폴더 공유 실행 중 PC 중지 오류 | 빠른 시작의 **샌드박스를 시작하는데 문제가 있으신가요?** 링크에서 Safe Mode로 비교합니다. 아래 제한을 먼저 확인합니다. |
| 재설치 후 AI 로그인·스킬이 사라짐 | 이전 설치 루트의 AI 저장소가 Setup 전에 삭제되었는지 확인합니다. 백업을 우선 확인하며 자동 이전이 삭제 데이터를 복구한다고 가정하지 않습니다. |

### Safe Mode의 범위

Safe Mode는 호스트 폴더, 클립보드와 장치 공유를 제외하고 네트워크를 유지합니다. 기관은 Sandbox 안의 Spork에서 선택합니다. 호스트 빠른 시작의 입력값·기관 선택을 전달하지 않으며, 호스트 공동인증서와 ZScaler 루트 인증서도 사용할 수 없습니다. 게스트 Spork는 최신 **정식** 배포본을 내려받으므로 호스트 프리뷰와 기능 차이가 날 수 있습니다. 자세한 비교 절차와 로그는 [Safe Mode 진단 가이드](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/docs/SAFE_MODE.ko.md)를 따릅니다.

일반 문의에는 태그, Windows 빌드, 아키텍처, 일반 실행/Safe Mode 여부와 오류 메시지를 포함합니다. 개인정보·인증서·인증정보가 들어간 로그나 화면은 공개 이슈에 그대로 올리지 않습니다. 빠른 시작의 `#debug`로 로컬 진단 경로를 확인할 수 있습니다.

## 5. 선택 실습: 식탁보 AI와 스킬

이 절은 기본 설치·첫 결과 확인의 필수 단계가 아닙니다.

1. 빠른 시작에서 **식탁보 AI (베타)**를 엽니다. 앱이 관리하는 전용 Codex 런타임 설치와 ChatGPT 계정 연결 안내를 따릅니다. 메시지 전송은 연결한 OpenAI 구독 사용량을 소비합니다.
2. **옵션 > AI 스킬 > 스킬 마켓플레이스 열기**에서 스킬의 출처·라이선스·요구 조건을 확인한 뒤 필요한 항목을 설치하고 활성화합니다. 새 프로필에는 스킬을 자동 설치하지 않습니다.
3. 스크립트 스킬이 필요하면 마켓플레이스의 **실행 환경** 탭에서 전용 Python·Node.js의 **실행 환경 설치/복구**와 **설치 및 환경 검사**를 진행합니다.

민감 개인정보, 인증서 파일과 스크린샷을 AI 입력으로 제공하지 않습니다. 인증서 만료 조회 스킬은 별도 설치와 사용자 동의 후 건수만 전달합니다. 스킬의 설치·활성 상태나 AI 답변에 나온 스킬 이름만으로 실제 스크립트 실행을 판정하지 않습니다. 실행 확인은 [공식 재현 도구와 증거 절차](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/tools/managed-skill-smoke/README.ko.md)를 사용합니다.

출처: [식탁보 AI 안내](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/README.md#식탁보-ai-베타), [스킬 마켓플레이스와 실행 경계](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/docs/SKILL_MARKETPLACE.ko.md).

## 6. 정식판 복귀와 조직 도입

정식판으로 돌아가려면 먼저 **옵션 > 미리 보기 > 업데이트 채널**에서 **안정 (권장)**을 선택하고, 그다음 [v1.21.1 정식 설치 관리자](https://github.com/yourtablecloth/TableCloth/releases/tag/v1.21.1)를 직접 실행합니다. 채널 변경만으로 버전이 자동으로 낮아지지 않습니다. [정식판 복귀 가이드](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/docs/TROUBLESHOOTING_UPDATE_CHANNEL.md)를 참고합니다.

조직 도입 전에는 실제 업무 사이트·인증 수단·프린터·공유 폴더·보안 에이전트·프록시를 포함한 파일럿을 진행합니다. Preview 10의 ARM64 CI 통과를 ARM64 실기기의 전체 GUI·AI 스킬 검증으로 확대하지 않습니다. 실제 기업 ZScaler 환경 전체도 검증 완료 범위가 아닙니다.

라이선스는 [AGPL-3.0](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/LICENSE-AGPL)과 [별도 상용 라이선스](https://github.com/yourtablecloth/TableCloth/blob/v1.22.0-preview.10/LICENSE-COMMERCIAL) 중 적용 조건을 확인합니다. 기업 지원과 운영 요건을 논의하려면 상용 제품 [ThinBox](https://cloudbro.ai/projects/thinbox/)를 참고할 수 있습니다. macOS에서의 관련 프로젝트는 [macSandbox](https://github.com/yourtablecloth/macSandbox)이며, TableCloth의 Windows용 설치 절차를 그대로 적용하는 대상은 아닙니다.