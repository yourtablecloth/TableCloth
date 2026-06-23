# TableCloth 개발 환경 요구사항

TableCloth는 Windows 11 이상에서 개발하도록 최적화되어 있습니다. 프로젝트 빌드 및 실행을 포함한 전체 애플리케이션 개발에는 Windows 환경이 필요합니다. 다른 운영체제에서는 문서 편집 및 카탈로그 관리와 같은 특정 작업으로 개발이 제한됩니다.

> [!WARNING]
> Windows 10은 2025년 12월부로 지원이 중단된 OS이며 보안 업데이트가 더 이상 제공되지 않습니다. 개발이나 빌드가 가능하더라도 보안상 권장하지 않습니다.

## Windows 개발

애플리케이션 빌드 및 실행을 포함한 전체 개발을 위해서는 Windows 11 이상 환경이 필요합니다.

### Windows 단계

1. 저장소를 클론합니다(서브모듈 포함):

    ```powershell
    git clone --recurse-submodules https://github.com/yourtablecloth/TableCloth.git
    ```

    이미 클론한 경우에는 다음 명령으로 서브모듈을 초기화합니다:

    ```powershell
    git submodule update --init --depth 1 --recursive
    ```

    > 식탁보의 빌드는 [TableClothCatalog](https://github.com/yourtablecloth/TableClothCatalog) 리포지터리를 `external/TableClothCatalog` 경로에 서브모듈로 가져와 사용합니다. 이 서브모듈로부터 빌드 시 `Images.zip`을 생성하므로, 초기화하지 않으면 빌드가 실패합니다.

2. Visual Studio 2026 이상을 설치합니다.
3. Visual Studio에서 솔루션 파일 `TableCloth.slnx`를 엽니다.
4. 프로젝트를 빌드하고 실행합니다:
    - 디버깅 없이 실행하려면 `Ctrl + F5`를 누릅니다.

## Visual Studio Code

> C# Dev Kit은 유효한 Visual Studio 라이선스가 필요할 수 있습니다.

1. 저장소를 클론합니다(서브모듈 포함):

    ```powershell
    git clone --recurse-submodules https://github.com/yourtablecloth/TableCloth.git
    ```

2. C# Dev Kit 확장을 설치합니다:
    - 확장 ID: `ms-dotnettools.csdevkit`
3. 프로젝트를 빌드하고 실행합니다:
    - `Ctrl + F5`를 누릅니다.

## Windows 이외 OS 개발

macOS 또는 Linux에서는 문서 편집과 카탈로그 관리 같은 작업으로 개발이 제한됩니다. 전체 애플리케이션 빌드 및 실행은 지원되지 않습니다.

## 프로젝트 구조

식탁보는 verb 기반 단일 바이너리로 통합되어 있습니다. 진입점 `TableCloth.exe` 하나가 호스트 런처와 샌드박스 내 에이전트 두 역할을 모두 수행합니다.

```text
src/
  TableCloth/        ← 진입점 .exe (얇은 셸: 부트스트랩 + verb 디스패치)
                       기본: TableCloth 호스트 모드
                       `TableCloth.exe spork [args]`: Spork 샌드박스 에이전트 모드
  TableCloth.App/    ← TableCloth 호스트 런처 모듈 (UI/서비스 라이브러리)
                       UseTableCloth() 확장 메서드로 DI 합성
  Spork.App/         ← Spork 샌드박스 에이전트 모듈 (UI/서비스 라이브러리)
                       UseSpork() 확장 메서드로 DI 합성
  TableCloth.Core/   ← 공유 인프라 (netstandard2.0): Helpers, Resources, Models, Events
  Spork/             ← Spork 단독 배포/재사용 아티팩트용 얇은 진입점
                       (Velopack 으로 별도 패키징되어 TableCloth 와 같은 릴리스에 함께 게시)
  TableCloth.Test/   ← TableCloth 측 단위 테스트 (MSTest)
  Spork.Test/        ← Spork 측 단위 테스트 (MSTest)
```

### 진입점 디스패치

```csharp
// TableCloth/Program.cs
if (args[0] == "spork") return RunSpork(args[1..]);
return RunTableCloth(args);
```

샌드박스는 호스트 TableCloth 설치 폴더를 staging의 `Desktop\App`으로 마운트해 `TableCloth.exe spork ...`를 호출합니다(`SandboxBuilder.cs`). 별도 `Spork.zip` 파이프라인은 폐기되었습니다.

### 빌드 / 게시

| 시나리오 | 명령 | 산출물 |
|---------|------|--------|
| 개발 빌드 | `dotnet build` 또는 VS F5 | AnyCPU framework-dependent, 호스트 .NET 10 SDK 사용 |
| 샌드박스 테스트 (개발) | `dotnet build` 후 호스트에서 실행 | `SandboxBuilder`가 자동으로 호스트 `%ProgramFiles%\dotnet` 마운트 + `DOTNET_ROOT` 설정해 샌드박스에 런타임 공급 |
| 배포 게시 | `dotnet publish src/TableCloth -c Release -r win-x64 -p:SelfContained=true -o publish/win-x64` | single-file self-contained `TableCloth.exe` (~93MB) — `TableCloth.csproj`의 조건부 PropertyGroup이 PublishSingleFile/PublishReadyToRun/IncludeNativeLibrariesForSelfExtract/EnableCompressionInSingleFile/PublishTrimmed=false를 자동 활성화 |
| Velopack 패키징 | `vpk pack -packId TableCloth -mainExe TableCloth.exe -packDir publish/win-x64 ...` | 설치/업데이트 패키지 |
| Spork 단독 게시 | `dotnet publish src/Spork -c Release -r win-x64 -p:SelfContained=true -o publish/spork/win-x64` | single-file self-contained `Spork.exe` (~92MB) — `Spork.csproj`의 조건부 PropertyGroup이 TableCloth 와 동일하게 자동 활성화 |
| Spork Velopack 패키징 | `vpk pack -packId Spork -mainExe Spork.exe -packDir publish/spork/win-x64 ...` | TableCloth 와 같은 릴리스에 함께 게시되는 별도 설치/업데이트 패키지 |

로컬에서 위 흐름(TableCloth + Spork 모두)을 한 번에 돌리려면 [`build.cmd`](./build.cmd)를 사용합니다.

### 단위 테스트

```powershell
dotnet test src/TableCloth.Test/TableCloth.Test.csproj
dotnet test src/Spork.Test/Spork.Test.csproj
```

### 아키텍처 결정 배경

본 통합 구조와 단계별 마이그레이션의 상세 내역은 [docs/UNIFIED_BINARY_TODO.md](./docs/UNIFIED_BINARY_TODO.md)에 기록되어 있습니다.

## Known Issues

### Windows Sandbox 첫 부팅 시 inner 데스크톱이 작게 렌더링됨

`.wsb` 로 sandbox 가 시작될 때, sandbox 내부 데스크톱 해상도가 호스트 측 클라이언트 영역과 즉시 동기화되지 않아 화면 일부에만 작게 렌더링되는 경우가 있습니다. `WindowsSandboxClient.exe`(또는 24H2+ 의 `WindowsSandbox.exe`) 자체의 OS 버그로 식탁보 / 포카락 측에서 발생시키는 문제가 아니며 공식 fix 는 없습니다.

**수동 우회 (사용자 액션)**:

- sandbox 윈도우를 잡고 크기를 살짝 조절하면 즉시 정상화됩니다.
- 또는 윈도우를 최대화(Maximize) 했다가 다시 원래 크기로 복원(Restore) 합니다.
- 또는 타이틀 바를 더블 클릭(=최대화/복원 토글)합니다.

**자동 우회 시도 이력**:

호스트 측에서 sandbox 메인 윈도우를 찾아 `ShowWindow(SW_MAXIMIZE)` → `SW_RESTORE` 를 자동으로 발화시키는 `SandboxRenderNudger` 보정 로직을 한 차례 도입했지만, sandbox 측의 첫 페인트 타이밍 / 윈도우 핸들 등장 순서 / UWP wrapper 유무 등 변수가 많아 실제 환경에서 신뢰성 있게 동작하지 않는 것이 확인되어 제거했습니다(commit history 참고). 향후 재시도 시 다음 접근들을 고려해볼 수 있습니다:

- `WindowsSandboxClient` 가 만드는 자식 윈도우(렌더 surface) 까지 깊이 탐색해 그쪽에 redraw 신호 전달
- `UIAutomation` 으로 sandbox 윈도우 트리 확인 후 적절한 시점 식별
- Windows Sandbox 자체에 fix 가 반영되기를 기다림 (Microsoft Feedback Hub 보고 권장)
