# Unified Binary 진척 관리 (TableCloth + Spork 단일 바이너리 / Verb 기반 CLI)

> 작업 브랜치: `feature/unified-binary-verbs`
> 시작일: 2026-05-12

## 배경과 목표

현재 구조에서는 Spork가 .NET Framework 4.8 기반의 별도 WPF 앱으로 빌드되어 `Spork.zip`으로 묶인 뒤, TableCloth가 샌드박스 진입 시점에 매번 해당 zip을 staging 영역에 풀어 마운트한다. 이 방식의 문제는:

- 두 앱이 사실상 동일한 의존성(`Microsoft.Extensions.*`, `Microsoft.Xaml.Behaviors.Wpf`, `AsyncAwaitBestPractices.MVVM`, `Sentry`, `System.CommandLine`, WPF 런타임 등)을 각자 가지고 있어 중복 배포.
- 샌드박스 진입마다 zip 추출 비용 발생.
- Spork는 net48에 묶여 있어 향후 .NET 진영 표준 라이브러리/API에서 점점 멀어짐.
- 별도 .exe / 별도 빌드 파이프라인 / 별도 코드 서명 / 별도 버전 관리.

**목표**: 두 앱을 단일 .exe 바이너리로 통합하고, 내부 구현은 모듈식 클래스 라이브러리로 분리한다. 진입점은 `System.CommandLine` 기반 verb 분기로 동작 모드를 결정하며, 각 모듈은 `builder.UseTableCloth()` / `builder.UseSpork()` 형태의 확장 메서드로 DI 컨테이너에 합성된다.

```text
TableCloth.exe                    → 기본 TableCloth 모드 (호스트 런처)
TableCloth.exe spork <args>       → Spork 모드 (샌드박스 내부 에이전트)
TableCloth.exe <future-verb>      → 후속 확장 모듈 (필요 시)
```

향후 Spork를 단독 출시해야 하는 경우, `Spork.App` 라이브러리를 그대로 재사용하는 얇은 `Spork.exe` 진입점을 별도로 만들면 됨.

## 아키텍처 변경 요약

| 영역 | 현재 | 변경 후 |
|------|------|---------|
| Spork TFM | `net48` | `net10.0-windows10.0.18362.0` |
| 배포 단위 | `TableCloth.exe` + `Spork.zip` (별도 게시) | `TableCloth.exe` 단일 (verb 분기) |
| 런타임 의존 | net48: OS 내장 / net10: 사용자 설치 | self-contained 단일 파일 (.NET 10 desktop runtime 내장) |
| Spork 배포 흐름 | post-build에서 zip 생성 → TableCloth 콘텐츠로 복사 → 샌드박스 staging에서 추출 | TableCloth 빌드 출력에 이미 Spork 코드 포함, 별도 패키징 없음 |
| 모듈 구조 | `TableCloth`, `Spork`, `TableCloth.Core` | `TableCloth`(진입점), `TableCloth.App`, `Spork.App`, `TableCloth.Core` |
| CLI 분기 | 두 개의 별개 .exe / 각자 CLI 옵션 | 단일 .exe + verb 기반 sub-command |
| DI 합성 | 각 앱이 자체적으로 ServiceCollection 구성 | `UseTableCloth()` / `UseSpork()` 확장 메서드로 모듈식 등록 |
| WPF Application | 앱당 1개 | verb별 `IHostedService`가 자기 모듈의 `Application` 인스턴스 생성·실행 |
| 샌드박스 호출 | `Spork.exe <args>` | `TableCloth.exe spork <args>` |

## 의존성 / 호환성 사전 점검

이 리팩토링이 성립하려면 다음 항목들이 .NET 10에서 동작하거나 대체 가능해야 한다.

- [x] **PnPeople.Security 1.1.0** — `netstandard2.0` 단일 어셈블리. .NET 10에서 그대로 사용 가능. **결론: 변경 없음.**
- [x] **Mono.HttpUtility 1.0.0.1** — `net40` 단일 어셈블리. Spork 내 사용처는 [ResourceResolver.cs:14,37](../src/Spork/Components/Implementations/ResourceResolver.cs#L14-L37)의 `ParseQueryString` 1회뿐이고, 이미 [TableCloth ResourceResolver.cs:38](../src/TableCloth/Components/Implementations/ResourceResolver.cs#L38)이 .NET BCL의 `System.Web.HttpUtility.ParseQueryString`을 동일 시그니처로 호출 중. **결론: Mono.HttpUtility PackageReference 제거 + `System.Web.HttpUtility`로 호출 교체.**
- [x] **Serilog 버전 정렬** — 의존성 트리:
  - `Serilog.Extensions.Logging 10.0.0` → `Serilog 4.2.0` (TableCloth 측 transitive)
  - `Sentry.Serilog 6.0.0` → `Serilog 2.10.0` (양쪽 공통 transitive)
  - Spork는 `Serilog 4.3.0` 명시
  - NuGet의 highest-wins으로 자연 해소되지만, 두 모듈을 한 어셈블리 그래프로 합치려면 명시 핀이 필요. **결론: CPM 도입 + Serilog 4.3.0 핀.**
- [x] **Velopack 0.0.1298 단일 파일 + self-contained 게시 호환성** — `net6.0/net8.0/net9.0/netstandard2.0/net462` 빌드 보유, net10 호환. Velopack 자체가 self-contained + single-file을 1급 시나리오로 지원. **결론: 호환성 무문제, 실제 게시물로 Phase 5에서 회귀 검증.**
- [x] **WPF 단일 파일 게시 시 알려진 함정** — `Assembly.Location` 사용처 전수 검사 결과 Spork 4건만 발견 (TableCloth/TableCloth.Core 0건):
  - [src/Spork/Components/Implementations/ResourceCacheManager.cs:93](../src/Spork/Components/Implementations/ResourceCacheManager.cs#L93) — 디렉터리 경로 → `AppContext.BaseDirectory`
  - [src/Spork/Converters/ServiceLogoConverter.cs:20](../src/Spork/Converters/ServiceLogoConverter.cs#L20) — 디렉터리 경로 → `AppContext.BaseDirectory`
  - [src/Spork/ViewModels/MainWindowViewModel.cs:168](../src/Spork/ViewModels/MainWindowViewModel.cs#L168) — `sporkExePath` 단축 아이콘 타겟 → `Environment.ProcessPath` (verb 통합 후엔 `TableCloth.exe spork` 호출 형태로 인자 포함 재구성 필요)
  - [src/Spork/Program.cs:39](../src/Spork/Program.cs#L39) — 디렉터리 경로 → `AppContext.BaseDirectory`
  - 트리밍은 `PublishTrimmed=false` 고정. **결론: 4건 교체 작업은 Phase 2(Spork.App 추출) 시점에 일괄 처리.**
- [x] **Sentry 6.0.0 단일 파일 모드 동작** — `net10.0` 전용 빌드 보유, 최신 버전 라인이라 self-extract 모드와 single-file 모드 모두 1급 지원. 단일 파일 + self-contained 환경에서의 stack frame symbol 처리도 6.x에서 안정. **결론: 호환성 무문제, Phase 5 게시물로 회귀 검증.**

## 작업 항목

### Phase 0 — 준비

- [x] 작업 브랜치 `feature/unified-binary-verbs` 생성
- [x] 본 진척 관리 문서 작성
- [x] 위 "의존성 / 호환성 사전 점검" 항목 전부 결론 도출 (2026-05-12)
- [x] CPM(Central Package Management) 도입 결정 — Phase 1 시작 시점에 `Directory.Packages.props` 추가하고 두 프로젝트의 `PackageReference`에서 `Version` 속성 제거. Serilog는 `4.3.0`으로 핀.
- [x] 각 모듈의 WPF `Application` 인스턴스 경계 설계 결정 — verb별 `IHostedService`가 `App.xaml`을 인스턴스화하고 `Application.Run(MainWindow)` 호출. 한 프로세스에서는 정확히 한 verb만 활성화되므로 `Application.Current`는 모듈당 단일 인스턴스로 유지됨.

### Phase 1 — `TableCloth.App` 라이브러리 추출

호환성 리스크가 가장 작은 단계. 진입점 분리 전에 라이브러리화부터 진행한다. 진행 방식은 (c) → (b): 먼저 빈 라이브러리 + `UseTableCloth()` 골격으로 컴파일되는 구조를 확립하고, 그 위에 영역별(Components → ViewModels → Views)로 점진 이동.

#### Phase 1.0 — CPM 도입 (2026-05-12)

- [x] `Directory.Packages.props` 작성, 메인 3개 프로젝트(TableCloth/TableCloth.Core/Spork)의 모든 PackageReference에서 Version 속성 제거
- [x] TableCloth.Test/Spork.Test는 MSTest.Sdk 충돌로 `ManagePackageVersionsCentrally=false`로 opt-out
- [x] `CentralPackageTransitivePinningEnabled`는 의도적으로 비활성화 — 활성화 시 TableCloth.Test와 Serilog MSB3277 충돌, Phase 3 통합 시점에 highest-wins으로 4.3.0 자연 수렴
- [x] 전체 솔루션 빌드 통과 (0 에러)

#### Phase 1.1 — `TableCloth.App` 골격 확립 (2026-05-12)

- [x] `src/TableCloth.App/TableCloth.App.csproj` 신규 (라이브러리, `net10.0-windows10.0.18362.0`, `UseWPF=true`, `TableCloth.Core` 참조)
- [x] `TableCloth.App.DependencyInjection.UseTableClothExtensions.UseTableCloth(this IHostApplicationBuilder)` 확장 메서드 골격 작성 (현재 no-op)
- [x] TableCloth.slnx와 TableCloth.csproj에 ProjectReference 추가
- [x] Program.cs에 `builder.UseTableCloth()` 호출 추가하여 호출 계약 확립
- [x] WPF `App` 클래스 → `TableClothApplication`으로 개명 (네임스페이스 `TableCloth.App`과 충돌 해소). 참조 3곳: App.xaml `x:Class`, App.xaml.cs, Program.cs DI 등록
- [x] 빌드 통과 (0 에러, 0 경고)

#### Phase 1.2 — 영역별 내부 구현 이전 (진행 예정)

- [ ] **Components / Services 이전** — Implementations + 인터페이스를 `TableCloth.App.Components`로 이동, `UseTableCloth()`로 등록 흡수
- [ ] **ViewModels 이전** — `TableCloth.App.ViewModels`로 이동
- [ ] **Views (Windows / Pages / Dialogs) 이전** — `TableCloth.App.Views`로 이동, WPF Resource/ResourceDictionary 정합성 유지
- [ ] **App.xaml / App.xaml.cs (TableClothApplication) 이전** — `TableCloth.App`으로 이동, 진입점은 인스턴스화만 담당
- [ ] **Resources / Converters / 헬퍼 이전**
- [ ] 진입점 `TableCloth` 프로젝트는 `Program.cs`만 남기고 부트스트랩에서 `builder.UseTableCloth().Build()` 호출
- [ ] 빌드/실행 회귀 확인 (현재 UX와 동일하게 동작해야 함)

### Phase 2 — Spork .NET 10 전환 + `Spork.App` 라이브러리 추출

- [ ] [Spork.csproj](../src/Spork/Spork.csproj) `TargetFramework`을 `net10.0-windows10.0.18362.0`으로 변경
- [ ] `LangVersion=12.0` 제거 (TableCloth와 동일하게 `preview`로 통일하거나 기본값 사용)
- [ ] net48 의존성 정리:
  - [ ] `Mono.HttpUtility` 제거 → 표준 라이브러리 또는 `System.Web.HttpUtility` 대체
  - [ ] 명시적으로 박혀 있던 `System.Collections.Immutable`, `System.Reflection.Metadata` 등 resolution-only 패키지 제거 (10.0에선 불필요)
- [ ] `src/Spork.App/Spork.App.csproj` 신규 (라이브러리)
- [ ] Spork 내부 구현 전부를 `Spork.App`으로 이전 (Services, Components, ViewModels, Views, Resources, App.xaml 등)
- [ ] `Spork.App.DependencyInjection.UseSporkExtensions.UseSpork(this HostApplicationBuilder)` 확장 메서드 작성
- [ ] 기존 `Spork` 프로젝트는 잠정적으로 `Program.cs`만 남기고 라이브러리 호출 — Phase 3에서 단일 진입점으로 흡수될 예정 (또는 향후 단독 출시용으로 보존)
- [ ] Spork 단독 실행 회귀 확인 (.NET 10 변환만 적용된 상태로 기존 흐름 유지되는지)
- [ ] `Spork.zip` 생성 PostBuild 파이프라인은 이 시점에서는 그대로 유지 (Phase 4에서 제거)

### Phase 3 — 진입점 통합 (verb-based CLI)

- [ ] [TableCloth/Program.cs](../src/TableCloth/Program.cs)를 `System.CommandLine` 루트 + sub-command 구조로 재작성
  - 루트 핸들러: `builder.UseTableCloth()`
  - `spork` 서브커맨드 핸들러: `builder.UseSpork()`
- [ ] 공통 부트스트랩 (`HostBootstrap.Run` 또는 유사) — 로깅(Serilog/Sentry), 설정 바인딩, Velopack 초기화 등은 verb 분기 전 1회 실행
- [ ] 각 verb의 `IHostedService` 구현이 자기 모듈의 `System.Windows.Application` 인스턴스를 생성·실행 (두 Application이 동시에 살지 않도록 보장)
- [ ] 기존 TableCloth CLI 옵션(예: `--select`)을 루트 명령의 옵션으로 이전
- [ ] 기존 Spork CLI 옵션을 `spork` 서브커맨드의 옵션으로 이전
- [ ] `Spork` 프로젝트 제거 또는 향후 단독 출시용 placeholder로 비활성화
- [ ] 두 verb를 통합 진입점에서 각각 실행 회귀 확인

### Phase 4 — 샌드박스 통합

- [ ] [SandboxBuilder.cs:227-253](../src/TableCloth/Components/Implementations/SandboxBuilder.cs) — `Spork.zip` 추출 단계 제거
- [ ] [SandboxBuilder.cs](../src/TableCloth/Components/Implementations/SandboxBuilder.cs)에서 호스트 설치 폴더(또는 staging 복사본)를 read-only로 마운트하도록 변경
- [ ] [SandboxBuilder.cs:220](../src/TableCloth/Components/Implementations/SandboxBuilder.cs) — Spork 호출 배치를 `TableCloth.exe spork <args>`로 변경
- [ ] StartupScript.cmd 생성 로직 (Spork 인자 전달부) 갱신
- [ ] [Spork.csproj:152-154](../src/Spork/Spork.csproj) — `Spork.zip` 생성 PostBuild target 제거
- [ ] [TableCloth.csproj:134](../src/TableCloth/TableCloth.csproj) — `Spork.zip` PreBuild 복사 제거
- [ ] `Spork.zip`을 참조하던 `<Content Include="Spork.zip">` 제거
- [ ] 샌드박스에서 verb 호출 회귀 확인 (NPKI 마운트, Catalog 흐름 포함)

### Phase 5 — 단일 파일 publish

- [ ] 진입점 `TableCloth.csproj`에 publish 속성 추가:
  - `PublishSingleFile=true`
  - `IncludeNativeLibrariesForSelfExtract=true`
  - `SelfContained=true`
  - `PublishTrimmed=false` (WPF + reflection-heavy XAML)
  - `PublishReadyToRun=true` (선택, cold start 개선)
- [ ] 코드베이스 전수 검색 — `Assembly.GetExecutingAssembly().Location`, `Assembly.GetEntryAssembly().Location`, `typeof(X).Assembly.Location` 사용처를 `AppContext.BaseDirectory` 또는 `Environment.ProcessPath`로 교체
- [ ] Sentry/Velopack의 어셈블리 location 의존 동작이 single-file 모드에서 정상인지 검증
- [ ] win-x64 / win-arm64 두 RID 모두 게시물 검증
- [ ] 첫 실행 시 자기-추출 캐시 위치 / 권한 / 사이즈 측정

### Phase 6 — Velopack 정책 결정

세 가지 옵션 중 택일하여 빌드 파이프라인 확정:

- [ ] **A. Velopack 유지 + 단일 파일 게시물 패키징**: 현재 설치/업데이트 UX 그대로 유지, 내부적으로 .exe 1개를 설치.
- [ ] **B. Velopack 제거 + 순수 portable .exe 배포**: 사용자가 .exe 1개를 다운로드해 어디서나 실행. 업데이트는 in-app 체크 + 수동 교체 또는 자체 갱신 트릭.
- [ ] **C. 하이브리드**: portable .exe를 primary로 두고, "설치" 액션 시 시작 메뉴 등록·자동 업데이트 활성화.

결정 후 GitHub Actions / 로컬 빌드 스크립트 갱신.

### Phase 7 — 정리 / 문서화

- [ ] `Spork` 프로젝트 디렉터리/이름 정리 (제거 or 단독 출시용 placeholder)
- [ ] 미사용 NuGet/리소스/스크립트 제거
- [ ] [README.md](../README.md), [DEVELOPMENT.md](../DEVELOPMENT.md) 갱신 — 빌드/실행/디버그 절차, 새 모듈 구조 설명
- [ ] 코드 서명 / CI 파이프라인 / 릴리스 노트 템플릿 갱신

### Phase 8 (옵션) — Spork 단독 출시 준비

이 단계는 필요해질 때 착수.

- [ ] `src/Spork/Program.cs`를 얇은 진입점(`builder.UseSpork().Build().Run()`)으로 부활 또는 신규 작성
- [ ] 별도 게시 프로파일 (Spork-only single-file self-contained)
- [ ] 별도 릴리스 채널/패키징

## 의사결정 로그

작업 중 결정한 사항을 여기에 누적.

- 2026-05-12 — verb 기반 단일 바이너리 + 모듈식 DI(UseXxx) 아키텍처로 확정. shim/하드링크 옵션은 불필요해져 폐기.
- 2026-05-12 — 두 앱은 self-contained 단일 파일로 게시하되 verb 분기로 하나의 .exe에 통합. 출력 폴더 머지 방식(이전 안)은 폐기.
- 2026-05-12 — Phase 0 호환성 점검 결과: PnPeople.Security/Velopack/Sentry 모두 .NET 10 호환 확인. Mono.HttpUtility는 `System.Web.HttpUtility`로 교체(사용처 1군데). Assembly.Location 4건은 Spork에 집중, Phase 2에서 일괄 교체.
- 2026-05-12 — CPM 도입 확정. Serilog는 4.3.0 핀(Spork 현행). Phase 1에서 `Directory.Packages.props` 추가.
- 2026-05-12 — WPF Application 경계: verb별 `IHostedService`가 자기 모듈의 `Application` 인스턴스를 생성/실행. 한 프로세스 한 모듈 원칙.
