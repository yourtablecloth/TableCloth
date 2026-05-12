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

```
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

- [ ] **PnPeople.Security 1.1.0**: TableCloth가 사용 중. .NET 10 호환성 확인. 비호환 시 대체/포크 검토.
- [ ] **Mono.HttpUtility 1.0.0.1**: Spork만 사용. .NET 10에서는 `System.Web.HttpUtility` (System.Web.HttpUtility NuGet 또는 BCL)로 대체 가능한지 확인.
- [ ] **Serilog 버전 정렬**: Spork 4.3.0 vs TableCloth가 transitive로 끌어오는 버전. 두 모듈을 한 어셈블리 그래프에 합치려면 단일 버전으로 통일 필요. CPM(`Directory.Packages.props`) 도입 검토.
- [ ] **Velopack 단일 파일 + self-contained 게시 호환성**: 현재 Velopack 워크플로가 `PublishSingleFile=true` + `IncludeNativeLibrariesForSelfExtract=true` 출력을 그대로 받아 패키징할 수 있는지 검증.
- [ ] **WPF 단일 파일 게시 시 알려진 함정**: `Assembly.Location` 사용 코드 전수 검색 → `AppContext.BaseDirectory` 또는 `Environment.ProcessPath`로 교체. 트리밍은 끄기 (`PublishTrimmed=false`).
- [ ] **Sentry / Velopack의 단일 파일 모드 동작**: 둘 다 어셈블리 위치를 probe하는 경향이 있음. 실제 게시물로 회귀 테스트.

## 작업 항목

### Phase 0 — 준비

- [x] 작업 브랜치 `feature/unified-binary-verbs` 생성
- [x] 본 진척 관리 문서 작성
- [ ] 위 "의존성 / 호환성 사전 점검" 항목 전부 결론 도출
- [ ] CPM(Central Package Management) 도입 여부 결정 — 도입한다면 `Directory.Packages.props` 추가 및 두 프로젝트의 `PackageReference` 버전 표기 제거
- [ ] 각 모듈의 WPF `Application` 인스턴스 경계에 대한 설계 노트 (verb별 `IHostedService`가 자기 App.xaml 인스턴스화)

### Phase 1 — `TableCloth.App` 라이브러리 추출

호환성 리스크가 가장 작은 단계. 진입점 분리 전에 라이브러리화부터 진행한다.

- [ ] `src/TableCloth.App/TableCloth.App.csproj` 신규 (라이브러리, `net10.0-windows10.0.18362.0`, `UseWPF=true`)
- [ ] 현재 [TableCloth](../src/TableCloth/) 내부 구현(Services, Components, ViewModels, Views, Resources 등)을 `TableCloth.App`으로 이전
- [ ] `App.xaml` / `App.xaml.cs`도 `TableCloth.App`으로 이전 (진입점이 인스턴스화하는 형태로)
- [ ] `TableCloth.App.DependencyInjection.UseTableClothExtensions.UseTableCloth(this HostApplicationBuilder)` 확장 메서드 작성 — 현재 [Program.cs](../src/TableCloth/Program.cs)의 ServiceCollection 구성을 이쪽으로 옮김
- [ ] 진입점 `TableCloth` 프로젝트는 임시로 `Program.cs`만 남기고 부트스트랩에서 `builder.UseTableCloth().Build().Run()` 호출
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
