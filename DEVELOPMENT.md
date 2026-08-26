# TableCloth 개발 환경과 프로젝트 구조

TableCloth는 Windows 11과 .NET 10을 기준으로 개발합니다. v1.21.0부터 TableCloth와 Spork UI는 Avalonia를 사용하며 RID를 지정한 Release 게시에서는 Native AOT를 활성화합니다. 현재 정식 버전은 v1.21.1입니다.

브랜치와 버전 운영은 [브랜치와 버전 관리 정책](docs/BRANCHING.md)을 따릅니다. 다음 Minor 버전은 `develop`에서 `X.Y.0-preview.N`으로 검증합니다. 최신 정식 버전의 긴급 패치는 `main`에서 `X.Y.Z`로 게시한 뒤 다음 개발 브랜치로 순방향 전파합니다. 채널 계약과 게시 절차는 [릴리스 채널](docs/RELEASE_CHANNELS.md)과 [릴리스 실행 절차](docs/RELEASING.md)에 기록했습니다.

> [!WARNING]
> Windows 10은 지원 대상에 포함하지 않습니다. 문서와 카탈로그는 다른 운영체제에서도 편집할 수 있지만 애플리케이션 빌드와 실행 검증은 Windows 11에서 진행합니다.

## 개발 도구

- Visual Studio 2026
- .NET 10 SDK
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

Visual Studio에서는 `TableCloth.slnx`를 열어 빌드합니다. Visual Studio Code를 사용한다면 C# Dev Kit 확장 `ms-dotnettools.csdevkit`과 위의 Visual Studio C++ 도구를 함께 준비합니다.

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
  TableCloth.Test/         호스트 및 공용 로직 단위 테스트
  Spork.Test/              Spork 로직 단위 테스트
```

호스트 TableCloth는 실행 파일과 부속 DLL을 세션 staging의 `App` 폴더로 복사하고 `Images.zip`을 `App\images`에 풉니다. Windows Sandbox는 이 staging 폴더를 데스크톱의 `App`으로 매핑한 뒤 `TableCloth.exe spork`를 실행합니다. Spork는 로컬 이미지와 카탈로그 스냅샷을 먼저 사용할 수 있어 게스트 네트워크가 불안정한 경우에도 기본 카탈로그 흐름을 유지합니다.

## 빌드와 Native AOT 게시

개발 빌드는 AnyCPU framework-dependent 출력으로 만듭니다.

```powershell
dotnet build TableCloth.slnx
```

단위 테스트는 다음 두 프로젝트를 실행합니다.

```powershell
dotnet test src/TableCloth.Test/TableCloth.Test.csproj
dotnet test src/Spork.Test/Spork.Test.csproj
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
