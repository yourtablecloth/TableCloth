# TableCloth 개발 환경과 프로젝트 구조

TableCloth의 `develop` 브랜치는 Windows 11과 .NET 11 RC1 SDK를 기준으로 v1.22.0 Preview를 개발합니다. v1.21.0부터 TableCloth와 Spork UI는 Avalonia를 사용하며 RID를 지정한 Release 게시에서는 Native AOT를 활성화합니다. 현재 정식 버전은 v1.21.1입니다.

브랜치와 버전 운영은 [브랜치와 버전 관리 정책](docs/BRANCHING.md)을 따릅니다. 다음 Minor 버전은 `develop`에서 `X.Y.0-preview.N`으로 검증합니다. 최신 정식 버전의 긴급 패치는 `main`에서 `X.Y.Z`로 게시한 뒤 다음 개발 브랜치로 순방향 전파합니다. 채널 계약과 게시 절차는 [릴리스 채널](docs/RELEASE_CHANNELS.md)과 [릴리스 실행 절차](docs/RELEASING.md)에 기록했습니다.

> [!WARNING]
> Windows 10은 지원 대상에 포함하지 않습니다. 문서와 카탈로그는 다른 운영체제에서도 편집할 수 있지만 애플리케이션 빌드와 실행 검증은 Windows 11에서 진행합니다.

## 개발 도구

- Visual Studio 2026 Insiders 또는 C# Dev Kit
- .NET 11 RC1 SDK `11.0.100-rc.1.26425.128` (`global.json`에 고정)
- Desktop development with C++ 워크로드
- Windows 11 SDK
- Git과 GitHub CLI
- Velopack CLI

Native AOT 링크에는 MSVC C++ 도구가 필요합니다. ARM64를 로컬에서 게시하려면 ARM64용 MSVC 빌드 도구도 설치합니다. 공식 릴리스는 GitHub Actions의 x64 `windows-latest` 러너와 ARM64 `windows-11-arm` 러너에서 각 아키텍처를 네이티브로 게시합니다.

## 저장소 준비

서브모듈을 포함하여 저장소를 복제합니다.

```powershell
git clone --recurse-submodules https://github.com/yourtablecloth/TableCloth.git
Set-Location TableCloth
```

이미 저장소를 복제했다면 카탈로그 서브모듈을 초기화합니다.

```powershell
git submodule update --init --depth 1 --recursive
```

빌드는 `external/TableClothCatalog`의 카탈로그와 이미지를 사용하여 `Images.zip`을 만듭니다. 서브모듈이 비어 있으면 카탈로그 자산 생성 단계가 실패합니다.

Visual Studio에서는 `TableCloth.slnx`를 열어 빌드합니다. Visual Studio Code에서는 C# Dev Kit 확장 `ms-dotnettools.csdevkit`으로 Debug 구성을 실행할 수 있습니다. 이 디버깅 과정에는 Native AOT 게시용 Visual Studio C++ 도구가 필요하지 않습니다.

Dev Kit이 `dotnet TableCloth.dll`로 앱을 시작하면 실행 프로세스는 `dotnet.exe`입니다. 앱은 `dotnet.exe`의 설치 폴더 대신 `TableCloth.dll`이 있는 Debug 출력 폴더에서 `TableCloth.exe`와 `Images.zip`을 찾습니다.

Native AOT의 `bin\Release\...\native`는 중간 산출물 폴더이므로 게시 결과를 실행할 때에는 `dotnet publish`가 만든 폴더를 사용합니다.

## 프로젝트 구조

TableCloth 설치판은 `TableCloth.exe`를 두 모드로 사용합니다. 인자 없이 실행하면 호스트 런처가 시작되며 `TableCloth.exe spork`는 Windows Sandbox 안의 Spork 게스트를 시작합니다. 같은 릴리스가 제공하는 `Spork.exe`는 무설치 Express와 단독 배포에 사용하는 별도 진입점입니다.

```text
src/
  TableCloth/              TableCloth.exe 진입점과 verb 디스패치
  TableCloth.App/          호스트 런처 UI와 Windows Sandbox 구성
  Spork.App/               게스트 카탈로그와 설치 UI
  Spork.Sandbox/           Sandbox 전용 DNS, 인증서, 부팅 초기화
  Spork/                   단독 Spork.exe 진입점
  Spork.Bootstrapper/      무설치 Express용 소형 다운로드 런처
  TableCloth.Theme/        TableCloth와 Spork가 공유하는 Avalonia 테마
  TableCloth.Core/         카탈로그 모델, 리소스와 공용 인프라
  TableCloth.Cli/          TableClothCli.exe 인증서 및 Catalog 조회 도구
  TableCloth.ManagedAi.Core/       AI 대화 계약, URL 검증과 모델 선택
  TableCloth.ManagedAi.OpenAi/     Codex 설치, 인증, 모델 조회와 실행
  TableCloth.ManagedAi.Windows/    Windows 프로세스 격리와 전용 프로필
  TableCloth.ManagedAi.Poc/        AI Preview용 진단 콘솔 호스트
  TableCloth.ManagedAi.Test/       AI 런타임과 공급자 단위 테스트
  TableCloth.Test/         호스트 및 공용 로직 단위 테스트
  Spork.Test/              Spork 로직 단위 테스트
```

호스트 TableCloth는 실행 파일과 부속 DLL을 세션 staging의 `App` 폴더로 복사하고 `Images.zip`을 `App\images`에 풉니다. Windows Sandbox는 이 staging 폴더를 데스크톱의 `App`으로 매핑한 뒤 `TableCloth.exe spork`를 실행합니다. Spork는 로컬 이미지와 카탈로그 스냅샷을 먼저 사용할 수 있어 게스트 네트워크가 불안정한 경우에도 기본 카탈로그 흐름을 유지합니다.

### 식탁보 AI Preview

사용자 화면에서는 AI 기능을 `식탁보 AI (Preview)`로 표시합니다. `TableCloth.App/ManagedAi`가 채팅 UI와 Catalog 연결을 담당하며 Managed AI 프로젝트 세 개가 플랫폼 중립 계약, OpenAI 통합과 Windows 격리를 나누어 구현합니다. `TableCloth.ManagedAi.Poc` 이름은 최초 설계 단계에서 만든 진단 프로젝트 경로와 명령 호환성을 유지합니다. 제품에 노출하는 기능 단계는 Preview입니다.

전용 스킬은 `%LOCALAPPDATA%\TableCloth\ManagedAi\profiles\openai-codex\codex-home\skills`에 저장합니다. Codex 런타임은 `runtimes\openai-codex` 아래에 설치하므로 두 수명 주기가 겹치지 않습니다. 공급자는 모델 요청 직전에 native `skills/list`를 읽고 전용 프로필 밖의 검색 결과를 `tablecloth-skills.config.toml`에서 비활성화합니다. 일반 설정 창의 `AI 스킬` 탭과 대화 창에서 목록 조회, 폴더 가져오기, 개별 사용 설정, 제거, 저장 위치 열기를 제공합니다. 대화 창의 수치는 실제 스킬 호출 횟수가 아니라 다음 대화에 제공할 활성 스킬 수입니다. 런타임이 없는 동안에도 전용 폴더 목록과 사용 설정을 관리할 수 있으며 상세 설명은 런타임을 설치한 뒤 확인합니다. 현재 Preview 정책은 shell, hook, subagent와 스킬의 MCP 의존성 자동 설치를 끄므로 지침 중심 스킬을 지원 범위로 봅니다.

`TableClothCli.exe`는 TableCloth 게시 출력에 함께 포함하는 호스트 전용 읽기 도구입니다. 첫 스킬 동기화 시 앱이 내장 `tablecloth-certificate-expiry`를 전용 스킬 폴더에 설치합니다. 사용자가 인증서 만료 질문을 보내고 로컬 조회 결과의 OpenAI 전송에 동의하면 앱이 CLI를 고정 인수로 실행합니다. Codex 프로필의 shell 차단 설정은 유지합니다. CLI는 `signCert.der`의 공개 만료 정보만 읽고 `signPri.key`는 존재 여부만 확인합니다. 기본 JSON에는 이름과 경로를 넣지 않으며, 로컬에서 `--details`를 지정한 경우에만 두 항목을 출력합니다. `catalog services`는 기존 `TableCloth.Data\CatalogCache.xml`을 오프라인으로 읽습니다. Catalog에는 인증서와 서비스의 대응 관계가 없습니다.

내장 `tablecloth-windows-sandbox` 스킬은 호스트의 `wsb.exe` 실행 별칭을 사용합니다. 앱은 요청을 분석한 뒤 `list`, 기본 설정의 `start`, `stop`, `connect`, `ip`만 고정 인수로 실행하고 결과를 대화에 전달합니다. 대상 ID가 없으면 실행 중인 Sandbox가 하나일 때만 자동 선택하며 종료 전에는 확인 창을 표시합니다. `exec`, 폴더 공유와 임의 구성은 지원하지 않습니다. 이 기능은 [Microsoft의 Windows Sandbox CLI](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-cli)를 설치한 환경에서만 동작합니다. 기존 식탁보 웹사이트 열기 흐름은 별도로 생성한 `.wsb` 구성을 계속 사용합니다.

다음 명령으로 로컬 조회 출력을 확인할 수 있습니다. `--root`와 `--file`은 명시적으로 지정한 테스트 또는 사용자 경로에만 적용합니다.

```powershell
TableClothCli.exe certificates expiring --within-days 30 --with-catalog-summary
TableClothCli.exe certificates expiring --within-days 30 --details
TableClothCli.exe catalog services --query 은행 --limit 20
```

[AI Preview 구현 및 검증 보고서](docs/poc/managed-ai-runtime.md)는 현재 기능, 테스트 증거와 남은 실기기 검증을 설명합니다. [원본 PoC 설계](docs/poc/managed-ai-runtime-design.ko.md)는 구현 전 가설과 승인 조건을 기록한 자료이므로 원래 명칭을 유지합니다.

## 빌드와 Native AOT 게시

개발 빌드는 AnyCPU framework-dependent 출력으로 만듭니다.

```powershell
dotnet build TableCloth.slnx
```

전체 단위 테스트는 솔루션에서 실행합니다.

```powershell
dotnet test TableCloth.slnx
```

RID를 지정하면 진입점 프로젝트가 `PublishAot=true`를 적용합니다. x64 TableCloth와 Spork 게시 예시는 다음과 같습니다.

```powershell
dotnet publish src/TableCloth/TableCloth.csproj -c Release -r win-x64 -o publish/Release/win-x64
dotnet publish src/Spork/Spork.csproj -c Release -r win-x64 -o publish/spork/Release/win-x64
```

ARM64 게시에서는 RID와 출력 경로를 `win-arm64`로 바꿉니다. Native AOT 결과는 네이티브 실행 파일과 Skia, HarfBuzz, ANGLE 지원 DLL로 구성됩니다. WPF 시절의 `PublishSingleFile`, `PublishReadyToRun`과 압축 단일 파일 옵션은 사용하지 않습니다.

로컬에서 게시와 Velopack 패키징을 함께 실행하려면 다음 명령을 사용합니다.

```powershell
.\build.cmd
```

이 스크립트는 x64와 ARM64 TableCloth, Spork 및 SporkBootstrap 산출물을 처리합니다. 한 아키텍처의 로컬 Native AOT 도구가 없다면 CI가 만든 `PublishPayload-<arch>`를 내려받아 `--skip-build`로 패키징할 수 있습니다. SimplySign 서명과 릴리스 자산 교체는 [릴리스 실행 절차](docs/RELEASING.md)에 따릅니다.

## 테스트 경계

단위 테스트와 비 UI 검증은 호스트에서 실행합니다. 설치 관리자, TableCloth 호스트 UI, 설정 저장, 업데이트와 딥링크 같은 호스트 시나리오도 호스트 Windows에서 확인합니다.

Spork 게스트 시나리오는 Windows Sandbox 안에서만 스모크 테스트합니다. TableCloth가 생성한 Sandbox에서 다음 흐름을 확인합니다.

1. Spork가 자동으로 시작되는지 확인합니다.
2. 카탈로그와 사이트 이미지가 표시되는지 확인합니다.
3. 검색, 즐겨찾기와 탭 전환을 확인합니다.
4. 프로그램 정보 대화상자와 외부 링크를 확인합니다.
5. 필요한 경우 실제 사이트의 설치 단계를 검증합니다.

Windows Sandbox 안에서 Sandbox를 다시 설치하거나 중첩 실행하지 않습니다. Sandbox 바깥에서 확인할 호스트 시나리오를 게스트로 옮기지도 않습니다.

## 릴리스와 아키텍처 검증

Retail과 Preview CI는 x64와 ARM64에서 각각 빌드 및 테스트한 뒤 미서명 Draft와 `PublishPayload-x64`, `PublishPayload-arm64`를 만듭니다. 로컬 x64 PC는 두 아키텍처 페이로드를 실행하지 않고 패키징하고 Authenticode 서명할 수 있습니다.

릴리스 전후 검증 범위는 다음과 같습니다.

- 태그와 `Directory.Build.Props` 버전 일치
- x64와 ARM64 설치 관리자 및 Portable ZIP
- TableCloth와 Spork 실행 파일의 Authenticode 상태
- 아키텍처 및 제품별 Velopack 채널 메타데이터
- SBOM과 심볼 자산
- Retail 게시 후 WinGet Pull Request와 Discord 공지

## Windows 이외 운영체제

macOS와 Linux에서는 Markdown 문서, 카탈로그와 플랫폼 중립 라이브러리를 편집할 수 있습니다. Windows Sandbox 실행, Windows 전용 UI 검증과 Native AOT Windows 패키징은 지원하지 않습니다. Avalonia와 플랫폼 중립 테마는 이후 크로스플랫폼 지원을 위한 기반으로 유지하지만 현재 제품 지원 범위는 Windows 11입니다.

## Windows Sandbox 렌더링 문제

Windows Sandbox를 처음 시작할 때 게스트 데스크톱이 클라이언트 영역보다 작게 표시될 수 있습니다. 이 현상은 Windows Sandbox 창 크기를 조정하거나 최대화와 복원을 한 번 전환하면 해소되는 경우가 있습니다. TableCloth가 자동으로 창 크기를 조정하는 방식은 Windows 버전별 창 구조와 초기 렌더링 시점이 달라 안정적으로 적용하지 못해 사용하지 않습니다.
