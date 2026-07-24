# WPF → Avalonia + Native AOT 마이그레이션 계획 (이슈 #296)

> 상태: **계획 수립 + AOT 호환성 실측 완료** (2026-07-24)
> 대상 이슈: [#296](https://github.com/yourtablecloth/TableCloth/issues/296) — Avalonia + Native AOT 기반 UI 전환
> 선행 참고: [TableClothVNext](https://github.com/yourtablecloth/TableClothVNext) (Avalonia 재작성 시도 아카이브)

## 1. 목표와 성공 기준

WPF를 Avalonia로 교체하고 최종적으로 Native AOT로 게시하여 배포 바이너리 크기를 획기적으로
줄이고 콜드 스타트를 개선한다.

**기준선 (v1.20.6, 압축 single-file self-contained):**

| 산출물 | 크기 |
| ------ | ---- |
| `TableCloth.exe` (win-x64 포터블) | ~90 MB |
| `Spork.exe` (win-x64 포터블) | ~88 MB |
| Velopack 설치 패키지 (TableCloth, x64) | ~106 MB |

**목표 (검증 대상, PoC로 확정):**

| 단계 | 접근 | 예상 크기(앱당) | 비고 |
| ---- | ---- | -------------- | ---- |
| 현행 | WPF + self-contained + 압축 | ~90 MB | 전체 런타임 + WPF 동봉 |
| Stage 1 | Avalonia + trimmed self-contained | ~30–45 MB | WPF 제거 + 트리밍이 크기 감소의 대부분 |
| Stage 2 | Avalonia + Native AOT | ~20–35 MB | .NET 런타임 완전 제거, JIT 없음, 즉시 시작 |

> 실측 근거: 이 문서의 §6.1 AOT 프로브에서 System.Management + Velopack + Sentry + System.Text.Json을
> 참조하는 **콘솔** 앱을 Native AOT로 게시한 결과 단일 네이티브 exe = **9.7 MB**. UI(Avalonia + Skia
> 네이티브)가 더해지면 위 범위가 현실적이다. "획기적 크기 감소"의 대부분은 **Stage 1(WPF 제거 + 트리밍)**
> 에서 달성되고, Stage 2(AOT)는 여기에 **런타임 비의존 + 콜드 스타트 개선 + 소폭 추가 감소**를 얹는다.

## 2. 결정 사항 (2026-07-24)

| 항목 | 결정 | 근거 |
| ---- | ---- | ---- |
| UI 프레임워크 | **Avalonia** | 공식 Native AOT 지원, 성숙한 XAML/테마/IME/접근성. XAML 방언이 WPF와 유사해 이관 부담이 상대적으로 낮음. MewUI는 1인·프롬프트 기반 개발 + "소형 툴" 지향 + NanoVG 커스텀 렌더링이라 한국어 IME·13개 다이얼로그·카탈로그 그리드를 요구하는 프로덕션 앱엔 리스크 과다. |
| 마이그레이션 전략 | **제자리 점진 이관** | `TableCloth.Core` + 비즈니스 로직 + ViewModel(CommunityToolkit.Mvvm)을 최대한 재사용하고 View 계층만 재작성. v1.20.x의 성숙도(재시도 분류, 키워드 검색, GPU 토글, 유휴 로그아웃, 인증서 처리 등) 보존. |
| AOT 도입 시점 | **2단계 분리** | Stage 1(Avalonia 전환, 프레임워크 의존/트리밍) 완료 후 Stage 2(AOT)로 진입. 각 단계가 독립적으로 릴리스 가능하고 리스크가 분리됨. |
| vNext 활용 | **참조만** | vNext는 처음부터 재작성한 부분 구현체(~74 cs)로 현행 기능 대부분이 누락. 코드를 그대로 흡수하지 않고 **테마/스타일(17 axaml)·`ScenarioRouter`·`Lemon.Hosting` 통합 방식**을 참조 자산으로 사용. |
| 테마 | **Light/Dark 2종으로 단순화** | 현행 4테마(Light/Dark/ColourfulLight/ColourfulDark)를 Avalonia `ThemeVariant`(Light/Dark) 2종으로 축소. "Colourful" 변형은 폐기. Avalonia `FluentTheme` 내장 Light/Dark를 그대로 활용 → 이관 공수 최소화. |
| 릴리스 정책 | **완료 전까지 내부 테스트만** | 전 과정 완료 전에는 사용자 대상 릴리스를 하지 않는다. Stage 1도 별도 배포하지 않고 내부 테스트로만 검증. |

## 3. 현행 구조와 이관 이음새(seam)

식탁보는 verb 기반 단일 바이너리다. 진입점 `TableCloth.exe` 하나가 호스트/`spork` 에이전트 두 역할을 수행하고,
UI는 두 개의 WPF 클래스 라이브러리로 분리되어 있다.

```
src/
  TableCloth/       ← 진입점 exe (WPF, WinExe). verb 디스패치 + 부트스트랩(Velopack/EULA/파일연결)
  TableCloth.App/   ← 호스트 UI 모듈 (UseWPF 라이브러리). UseTableCloth()
  Spork.App/        ← 샌드박스 에이전트 UI 모듈 (UseWPF 라이브러리). UseSpork()
  TableCloth.Core/  ← 공유 인프라 (netstandard2.0). 프레임워크 중립 — 변경 없음
  Spork/            ← Spork 단독 exe (WPF, WinExe)
  Spork.Sandbox/    ← 샌드박스 부팅 초기화 (UI 무관)
  Spork.Bootstrapper/ ← Express 부트스트래퍼 (이미 PublishAot=true!)
```

**UI 표면적 실측:** WPF XAML 29개(창/페이지/다이얼로그 ~13개 + 테마 사전 4+4 + App.xaml 2), ViewModel 23개,
IValueConverter 13개, 커스텀 컨트롤 0개(`RichTextBoxHelper` 첨부 속성만 존재).

이미 존재하는 **깨끗한 추상화 이음새** — 점진 이관의 교체 지점:

| 인터페이스 | 현행 WPF 구현 | Avalonia 재구현 방향 |
| ---------- | ------------- | -------------------- |
| `IMessageBoxService` / `IAppMessageBox` | `MessageBoxService`(System.Windows.MessageBox) | MessageBox.Avalonia 또는 자체 다이얼로그 |
| `IAppUserInterface` | `AppUserInterface`(DI로 Window 생성 + Owner) | Avalonia `Window` 팩토리로 재구현 |
| `INavigationService` | `NavigationService`(WPF `Frame`.Navigate) | `ContentControl` 콘텐츠 스왑으로 재구현(인터페이스 유지) |
| `IApplicationService` | `ApplicationService`(`Application.Dispatcher`) | `Avalonia.Threading.Dispatcher.UIThread` |
| `IVisualThemeManager` | `VisualThemeManager`(HwndSource + WM_SETTINGCHANGE) | `PlatformSettings.ColorValuesChanged` |

**직접 교체가 필요한 WPF 종속 지점(추상화 밖):**
`MessageBoxService`(System.Windows.MessageBox), `ApplicationService`/`DispatchInvoke`(Dispatcher),
`VisualThemeManager` + `LicenseWindow`/`DisclaimerWindow`(HwndSource/WndProc), `NavigationService`(Frame),
`WindowChrome` 기반 `MainWindowStyle`(커스텀 캡션 버튼), `RichTextBoxHelper`(FlowDocument).

**그대로 재사용(프레임워크 중립):** `TableCloth.Core` 전체, 모든 Components/Services(위 UI 종속 5종 제외),
ViewModel 23개(CommunityToolkit.Mvvm `[ObservableProperty]`/`[RelayCommand]` + AsyncAwaitBestPractices),
카탈로그 파싱(수동 `XmlReader`), resx 문자열 리소스.

## 4. 패키지 매핑

| 현행 (WPF) | 대체 (Avalonia) | 비고 |
| ---------- | --------------- | ---- |
| `<UseWPF>true</UseWPF>` | `Avalonia` + `Avalonia.Desktop` + `Avalonia.Themes.Fluent` | 진입점은 `Avalonia.Desktop`, 라이브러리는 `Avalonia` |
| `Microsoft.Xaml.Behaviors.Wpf` | `Xaml.Behaviors.Avalonia` | `i:Interaction.Triggers`/`EventTrigger`/`InvokeCommandAction` 개념 동일, `xmlns` 변경 |
| `XamlRadialProgressBar` | Avalonia 내장 없음 → FluentAvalonia `ProgressRing` 또는 vNext `ProgressRingStyles.axaml` 이식 | SplashScreen 스피너 1곳 |
| `System.Windows.MessageBox` | `MessageBox.Avalonia` | vNext에서 사용한 선례 |
| `CommunityToolkit.Mvvm` | **변경 없음** | 소스젠 방식이 AOT/Avalonia 양쪽 호환 |
| `AsyncAwaitBestPractices.MVVM` | **변경 없음** | 프레임워크 중립 |
| (호스팅 통합 없음) | `Lemon.Hosting.AvaloniauiDesktop` | `Host.CreateApplicationBuilder` + `AddAvaloniauiDesktopApplication<App>()`. vNext 선례 |
| `BitmapImage`(System.Windows.Media) | `Avalonia.Media.Imaging.Bitmap` | `ServiceLogoConverter`, `ResourceCacheManager` |
| `Frame`/`Page` | `UserControl` + `ContentControl` 스왑 | `INavigationService` 재구현 |
| `RichTextBox`/FlowDocument | `SelectableTextBlock` + `Inlines`(Run/하이퍼링크) | Spork About/Precautions 2곳 |
| `WindowChrome` 커스텀 크롬 | Avalonia `Window.ExtendClientAreaToDecorationsHint` + 커스텀 타이틀바 | 8개 테마 사전 공통 |

## 5. Stage 1 — WPF → Avalonia 전환 (프레임워크 의존 + 트리밍)

목표: 기능 패리티를 유지한 채 WPF를 완전히 제거하고, trimmed self-contained로 게시한다. 이 단계만으로도
바이너리가 ~30–45 MB로 축소된다. AOT는 아직 켜지 않는다(리스크 분리).

### 5.1 프로젝트/TFM 변경

- `TableCloth.App` / `Spork.App`: `<UseWPF>` 제거, Avalonia 패키지 추가. `App.xaml` → `App.axaml`.
- TFM은 **`net10.0-windows10.0.18362.0` 유지** — 레지스트리/`user32` P/Invoke/`[SupportedOSPlatform]` 기존 코드
  보존이 목적. (vNext는 `net9.0`을 썼으나 이 리포는 Windows 전용이므로 `-windows` 유지가 이관 부담이 낮음.)
- `AvaloniaUseCompiledBindingsByDefault=true` 설정(Stage 2 AOT 대비 미리 컴파일 바인딩 강제).

### 5.2 XAML 방언 변환 규칙(요약)

| WPF | Avalonia |
| --- | -------- |
| `Visibility="{Binding X, Converter=...}"` | `IsVisible="{Binding X}"` (bool 직결, 컨버터 다수 제거 가능) |
| `x:Name` + `ElementName` 바인딩 | `#Name` 또는 컴파일 바인딩 |
| `{StaticResource}`/`{DynamicResource}` | `{StaticResource}`/`{DynamicResource}` (테마 스왑엔 Dynamic 유지) |
| `Style TargetType` + `Setter` | Avalonia `Style Selector` 문법 |
| `<Trigger>`/`<DataTrigger>` | `Style` selector + `:pseudoclass`, 또는 바인딩 |
| `pack://application:,,,/Asm;component/...` | `avares://Asm/...` |
| `Grid.Row`/`ColumnDefinition` | 동일 |

> `BooleanToVisibilityConverter`류는 Avalonia `IsVisible`(bool)로 상당수 제거 가능 → 컨버터 13개 중 절반 이상 소거 전망.

### 5.3 컨버터 이식 (13개)

`IValueConverter` 시그니처는 Avalonia도 동일(`Convert`/`ConvertBack`). 네임스페이스만 `System.Windows.Data` →
`Avalonia.Data.Converters`, `DependencyProperty.UnsetValue` → `AvaloniaProperty.UnsetValue`(또는
`BindingOperations.DoNothing`). Bitmap 반환 컨버터(`ServiceLogoConverter`)는 `Avalonia.Media.Imaging.Bitmap`로 교체.

### 5.4 테마 시스템 재설계 — Light/Dark 2종으로 단순화 (**결정됨**)

현행: 4테마(Light/Dark/ColourfulLight/ColourfulDark) × 2앱 = **8개 사전, ~35k XAML 라인**. 스톡 컨트롤을 전면
리스타일 + `WindowChrome`. `ThemesController`가 `MergedDictionaries`를 런타임 교체, `VisualThemeManager`가
OS 라이트/다크를 HWND 훅으로 감지. Spork의 4개는 TableCloth와 사실상 중복.

**결정: 4테마 → Light/Dark 2종으로 축소.** "Colourful" 변형은 폐기한다. 이로써 최대 이관 항목이 크게 줄어든다.

재설계:
1. **Avalonia `FluentTheme` 채택** → 스톡 컨트롤 리스타일 ~35k 라인의 대부분을 폐기(Avalonia가 Fluent Light/Dark를 내장 제공).
2. **`ThemeVariant.Light` / `ThemeVariant.Dark` 2종만** 사용. `ThemeTypes` 열거형(4값)과 `ColourfulLight`/`ColourfulDark`
   사전은 제거. `ThemesController`의 `MergedDictionaries` 수동 교체 로직도 불필요(`RequestedThemeVariant`로 전환).
3. **브러시 키는 얇은 리소스 사전 2개(Light/Dark)로 유지** — 기존 View가 참조하는 키(`ContainerBackground`,
   `ControlDefaultForeground`, `InfoBannerBackground` 등 ~109개)를 `ThemeDictionaries`(ThemeVariant별)로 재정의해
   View 바인딩 수정을 최소화. (메모리 [[wpf_theme_brushes_in_theme_dicts]]의 "테마 사전에 정의" 원칙 유지: App.axaml
   직접 정의 금지.)
4. **두 앱의 테마 사전을 공유 리소스로 단일화**(중복 제거).
5. `WindowChrome` 커스텀 캡션 버튼 → Avalonia `ExtendClientAreaToDecorationsHint` + 커스텀 타이틀바 UserControl.
6. `IVisualThemeManager` 재구현 → `PlatformSettings.ColorValuesChanged` + `GetColorValues().ThemeVariant`로 OS Light/Dark
   추종. HwndSource/WndProc 완전 제거. (Colourful 자동 선택 로직도 함께 제거.)

> 회귀 주의: 현재 `VisualThemeManager`는 OS가 Light/Dark일 때 각각 `ColourfulLight`/`ColourfulDark`를 선택한다.
> 단순화 후에는 OS Light→Light, OS Dark→Dark로 매핑되므로, 사용자에게 보이던 강조색 계열 룩은 사라진다(의도된 변경).

### 5.5 네비게이션 (TableCloth 전용)

WPF `Frame`/`Page` → Avalonia에 `Frame` 없음. `MainWindow`의 `ContentControl` 콘텐츠를 페이지 `UserControl`로 스왑.
`INavigationService` 인터페이스는 유지하고 구현만 교체(`Navigate`/`GoBack`/`CanGoBack`). Pages(Catalog/Detail/QuickStart)는
`Page` → `UserControl`로 변환. Spork는 이미 `Visibility` 바인딩으로 인-윈도우 전환하므로 `IsVisible`로 치환만 하면 됨.

### 5.6 창/다이얼로그 이식 순서 (의존성 낮은 것부터)

1. `IdleLogoutWarningWindow`(14줄) — 최소 창, 워밍업.
2. `InputPasswordWindow`, `DisclaimerWindow`, `AhnLabSafeTxGuideWindow`, `PowerSchemeGuideWindow` — 단순 다이얼로그.
3. `AboutWindow`(양 앱), `PrecautionsWindow`, `SiteReportWindow` — `RichTextBoxHelper` 대체 포함.
4. `OptionsWindow`, `CertSelectWindow`, `InstallStepsWindow` — 탭/리스트 바인딩.
5. `SplashScreen`(XamlRadialProgressBar 대체), `LicenseWindow`(HWND 훅 제거).
6. `CatalogPage`/`DetailPage`/`QuickStartPage`(TableCloth), `MainWindow`(양 앱, 439줄 Spork가 최대) — 마지막.

### 5.7 WPF 특정 API 대체 표

| 항목 | 위치 | 대체 |
| ---- | ---- | ---- |
| `Application.Dispatcher`/`DispatchInvoke` | `ApplicationService`(양 앱) | `Dispatcher.UIThread.Invoke/Post` |
| `DispatcherTimer` | `SessionIdleMonitor` | `Avalonia.Threading.DispatcherTimer` |
| `Clipboard.SetText` | `DetailPageViewModel:377` | `TopLevel.Clipboard.SetTextAsync` |
| `VisualTreeHelper`/`FindChildControl` | `Extensions`, `CatalogPage` | Avalonia `LogicalTreeHelper`/`GetVisualDescendants` |
| HWND 훅(WM_SETTINGCHANGE) | `VisualThemeManager`, `LicenseWindow`, `DisclaimerWindow` | `PlatformSettings.ColorValuesChanged` |
| `SystemParametersInfo`/`HIGHCONTRAST`/`GetLastInputInfo` | `SandboxBuilder`, `SessionIdleMonitor` | **유지**(순수 P/Invoke, Avalonia 무관) |

### 5.8 리소스/이미지

- resx 문자열: **변경 없음**(`TableCloth.Core`). XAML의 `{x:Static res:...}` → Avalonia도 `x:Static` 지원.
- `SandboxIcon.png`/`Signature.jpg`: `Resource` → `AvaloniaResource`, `avares://` URI로 참조.
- Images.zip(~850 사이트 로고): 런타임 zip 해제 로직은 그대로. `BitmapImage` → `Avalonia.Media.Imaging.Bitmap`.

### 5.9 호스팅 통합

vNext 선례대로 `Lemon.Hosting.AvaloniauiDesktop`로 `Microsoft.Extensions.Hosting`과 Avalonia 수명주기를 통합:
`builder.Services.AddAvaloniauiDesktopApplication<App>(BuildAvaloniaApp)` → `app.RunAvaloniauiApplication(args)`.
기존 verb 디스패치(`Program.cs`)와 DI 합성(`UseTableCloth()`/`UseSpork()`)은 유지. `IServiceProvider`를
`Application.Properties`에 stash하던 방식(컨버터의 DI 접근용)은 Avalonia에서도 동작하나, 가능하면 컴파일 바인딩 +
`x:CompileBindings`로 대체.

### 5.10 Stage 1 검증

- 기능 패리티 체크리스트(창별 스모크): 실행/샌드박스 생성/카탈로그/인증서/설치 스텝/테마 스왑/유휴 로그아웃.
- 단위 테스트(`TableCloth.Test`/`Spork.Test`)는 UI 비의존이 대부분 → 유지. ViewModel 테스트가 있으면 그대로.
- 크기 실측 후 §1 표 갱신.

## 6. Stage 2 — Native AOT 적용

### 6.1 AOT 호환성 프로브 실측 결과 (2026-07-24)

`System.Management` + `Velopack 0.0.1298` + `Sentry 6.0.0` + `System.Text.Json`을 참조하는 콘솔 앱을
`net10.0-windows` / `PublishAot=true` / `win-x64`로 실제 게시·실행하여 확인. **산출 네이티브 exe = 9.7 MB.**
(재현: `tools/aot-probe/`)

| 의존성 | ILC 분석 | Native AOT 런타임 | 판정 |
| ------ | -------- | ----------------- | ---- |
| **System.Management (WMI)** | `IL3052: COM interop is not supported with full AOT` + `IL2067`/`IL2077`. ILC: "will always throw" | `TypeInitializationException` | ❌ **하드 blocker → 대체 필수** |
| System.Text.Json (리플렉션) | `IL2026` + `IL3050` | `InvalidOperationException: Reflection-based serialization has been disabled` | ⚠️ **소스젠 전환 필수** |
| System.Text.Json (소스젠) | 경고 없음 | ✅ 정상 직렬화/왕복 | ✅ 이 경로로 이관 |
| **Velopack 0.0.1298** | 경고 없음 | 정상(런타임 예외는 `VelopackApp.Build()` 미호출 탓 — 앱 `Program.cs:73`은 이미 호출) | ✅ **AOT 호환** |
| **Sentry 6.0.0** | 경고 없음 | ✅ `SentrySdk.Init completed` | ✅ **AOT 호환** |
| CPUID 대체(§6.2 A) | 경고 없음 | ✅ `hypervisor-present bit (ECX[31]) = True` | ✅ WMI 하이퍼바이저 감지 대체 확정 |

> 결론: **하드 blocker는 System.Management 하나뿐**. JSON은 기계적 소스젠 이관, Velopack/Sentry는 무변경.
> 카탈로그 XML 파싱은 이미 수동 `XmlReader`, 부트스트랩 인자도 수동 파싱이라 AOT 안전.

### 6.2 의존성별 대응

**(A) System.Management (WMI) 제거** — 코드 리뷰 결과(2026-07-24) WMI 실사용은 **단 한 곳**뿐. 리스크가 매우 낮다.

WMI를 참조하는 파일은 3개(`AppStartup.cs`, `Win32DiskDrive.cs`, `Win32DiskPartition.cs)`이나, 소비처 추적 결과:

| 파일 | WMI 쿼리 | 소비처 | 대응 |
| ---- | -------- | ------ | ---- |
| `Internals/Win32DiskDrive.cs` | `Win32_DiskDrive`, `MSFT_PhysicalDisk`, `Win32_OperatingSystem`(디스크 지문) | **참조 0건 (죽은 코드)** | **파일 삭제** |
| `Internals/Win32DiskPartition.cs` | `Win32_DiskDrive`↔`Win32_DiskPartition` 연관(파티션) | **참조 0건 (죽은 코드)** | **파일 삭제** |
| `AppStartup.cs:131` | `Win32_ComputerSystem.HyperVisorPresent`(하이퍼바이저 감지) | 시작 요건 검사(릴리스에서 없으면 치명적 오류로 차단) | **CPUID로 대체** |

- **디스크 지문 클래스(`Win32DiskDrive`/`Win32DiskPartition`)는 완전한 죽은 코드**다. `src` 전체(테스트·XAML 포함)에서
  `GetPhysicalDisks`/`Win32DiskDrive`/`Win32DiskPartition`/`SerialNumber` 참조가 0건. "Phase 1.2 라이브러리 이전"
  커밋(90d8a96) 때 딸려온 뒤 배선된 적이 없다. → **두 파일을 삭제**하면 `Win32_DiskDrive`/`MSFT_PhysicalDisk`/
  `Win32_OperatingSystem` WMI 의존이 통째로 사라진다. 대체 구현 불필요.
- **하이퍼바이저 감지**(`Win32_ComputerSystem.HyperVisorPresent`)는 실사용 요건 검사다. **CPUID leaf 1, ECX 비트 31
  (hypervisor-present 비트)** 로 대체 — `System.Runtime.Intrinsics.X86.X86Base.CpuId(1, 0)`. AOT 프로브로 검증 완료:
  IL 경고 0개, 런타임에서 WMI와 동일한 값(`True`) 반환(§6.1, `tools/aot-probe/` 참조).

  ```csharp
  // AppStartup.cs — Win32_ComputerSystem.HyperVisorPresent 대체 (AOT-safe, 순수 인트린식)
  static bool? IsHypervisorPresent()
  {
      if (!X86Base.IsSupported)  // ARM64 등: 판별 불가 → 보수적으로 null(차단하지 않음)
          return null;
      var (_, _, ecx, _) = X86Base.CpuId(1, 0);
      return (ecx & (1 << 31)) != 0;
  }
  ```

  ARM64(win-arm64도 타깃 RID)에서는 `X86Base.IsSupported == false`이므로 `null`을 반환해 **차단하지 않고** 최종 게이트
  (`WindowsSandbox.exe` 존재/실행)에 위임한다 — 기존 `IsSandboxCpuLikelyThrottled()`의 "확신할 때만 경고" 패턴과 일치.

- 관련 감지 로직 2곳은 **이미 WMI 미사용**이라 무영향: `Helpers.IsSandboxCpuLikelyThrottled()`(전원 관리 API),
  `SandboxBootstrap.IsRunningInSandbox()`(`UserName == WDAGUtilityAccount` + 경로).
- 위 두 대응(삭제 + CPUID) 후 `System.Management` PackageReference를 제거하고 AOT publish에서 IL3052가 사라지는지 확인한다.

**(B) System.Text.Json 소스젠 이관** — `[JsonSerializable]` 컨텍스트 도입. 대상(실측):
`InstallRecordStore`, `UserDataStore`, `PreferencesManager`, `SporkAnswersSessionPolicyProvider`,
`AppUpdateManager`, `SponsorInfo`/`ContributorInfo`(Core), `SandboxBuilder`, `QuickStartPageViewModel`,
`MicrosoftEdgeInstaller`, `IdleGuardApplication` 등. 각 `JsonSerializer.Serialize/Deserialize` 호출을
`JsonTypeInfo`/`JsonSerializerContext` 오버로드로 교체. (`Spork.Bootstrapper.BootstrapOptions`는 이미 소스젠 컨텍스트 보유 — 선례.)

**(C) COM interop 점검** — `ShortcutCreator`(IShellLink), 인증서 선택(CryptUI 등)이 레거시 COM이면
`[GeneratedComInterface]`로 전환. `[ComImport]`/`Marshal.GetActiveObject` 계열은 AOT에서 제한.

**(D) System.CommandLine 2.0.1** — ✅ **AOT-clean 검증 완료**(2026-07-24). 실제 사용 패턴(`RootCommand` + `Option<T>` +
`Parse` + `GetValue`)을 AOT 프로브로 게시·실행한 결과 IL 경고 0, 파싱 정상. 코드 변경 불필요.

### 6.3 진입점 csproj 변경

`TableCloth.csproj` / `Spork.csproj`(진입점 exe)에:
- `PublishAot=true`(publish 시). 기존 `PublishSingleFile`/`IncludeNativeLibrariesForSelfExtract`/
  `EnableCompressionInSingleFile`/`PublishReadyToRun`은 **AOT와 상호배타 → 제거**. (AOT가 단일 네이티브 exe를 직접 생성)
- 단, Avalonia는 Skia/HarfBuzz **네이티브 지원 DLL을 별도 파일로 동봉**한다 → 산출물이 완전한 단일 파일은 아님.
  Velopack 패키징은 다중 파일을 정상 처리하므로 문제 없음.
- 모든 의존 프로젝트에 `IsAotCompatible=true`, XAML에 컴파일 바인딩(`x:CompileBindings`).
- `Directory.Build.Props`의 `FixWpfReferences` 타깃(WPF 워크어라운드)은 WPF 제거 후 삭제.

### 6.4 트리밍/AOT 경고 제거 절차

`dotnet publish -p:PublishAot=true`의 IL2xxx/IL3xxx 경고를 0으로 만들 때까지 반복. 리플렉션 잔존부는
소스젠/`DynamicallyAccessedMembers` 주석/트리머 루트(`TrimmerRootDescriptor`)로 해소. resx `ResourceManager`는
생성된 typed accessor 경로가 트리밍 안전.

### 6.5 단일 바이너리 / verb 디스패치

verb 디스패치(`Program.cs` 수동 인자 파싱)는 AOT 무관 — 유지. AOT는 이미 네이티브 단일 exe이므로 통합 바이너리
전략과 정합적. 샌드박스는 호스트 `TableCloth.exe`를 마운트해 `spork` verb로 실행하는 방식 유지.

### 6.6 릴리스 파이프라인 영향

- `build.cs`: publish 명령을 `-p:SelfContained=true` → `-p:PublishAot=true`로 전환. **`Spork.Bootstrapper`가
  이미 `build.cs:308`에서 `PublishAot=true`로 게시 중** → AOT 툴체인이 CI에서 이미 검증됨(선례).
- CI 러너에 **MSVC C++ 툴체인** 필요(Native AOT 링커). GitHub Actions `windows-latest`는 VS Build Tools 포함.
  로컬 실측 시 `vswhere.exe`가 PATH에 있어야 링크 성공(Developer Prompt 또는 PATH에 VS Installer 추가).
- 코드 서명(Certum SimplySign, [[code_signing_approach]]): AOT 네이티브 exe에도 동일 적용. winget은 Setup.exe만 참조 — 무영향.
- Velopack: AOT 산출물(exe + Skia/HarfBuzz DLL)을 `vpk pack`으로 패키징. 무변경.

## 7. 리스크와 완화

| 리스크 | 영향 | 완화 |
| ------ | ---- | ---- |
| 테마 사전 8개(~35k 라인) 재작성 | Stage 1 최대 공수 | FluentTheme 베이스 채택으로 대부분 폐기, 브러시 키만 얇게 유지 |
| WMI 디스크 지문 대체 | 머신 식별 로직 변경 | 사용처 확인 후 소스젠 COM/`DeviceIoControl`/MachineGuid 중 최소 침습 선택 |
| `RichTextBox`/FlowDocument 부재 | Spork About/Precautions 렌더 | `SelectableTextBlock` + Inlines로 2곳 재작성 |
| 한국어 IME/접근성 회귀 | 사용성 | Avalonia는 IME 성숙 — 초기 슬라이스에서 우선 검증 |
| System.CommandLine AOT | 인자 파싱 | PoC 검증, 필요 시 수동 파싱으로 대체(부트스트래퍼 선례) |
| Avalonia 서드파티 컨트롤 AOT | 빌드 실패 | 최소 의존(FluentAvalonia/Behaviors만), 각 PoC 검증 |
| 큰 변경의 회귀 | 전반 | 브랜치 격리 + WPF 병행 유지(§9) + 창별 패리티 체크 |

## 8. 마일스톤 / 순서

- **M0 (완료):** 계획 수립 + AOT 프로브 실측(§6.1). blocker 확정(System.Management 단일).
- **M1:** UI 이음새 선(先)디커플링 — `IApplicationService`/`IMessageBoxService`/`INavigationService`/`IVisualThemeManager`를
  WPF 타입 비의존으로 정리(아직 WPF 유지). ViewModel 12개의 `System.Windows` 참조 제거.
- **M2:** Avalonia 스캐폴딩 — `Lemon.Hosting` 통합, `App.axaml`, 공유 테마(FluentTheme + 브러시 키), 수직 슬라이스
  1창(예: `AboutWindow`) 완전 이관 + IME/테마 스왑 검증.
- **M3:** 다이얼로그·페이지 순차 이관(§5.6), WPF 제거. **Stage 1 내부 테스트 빌드**(trimmed self-contained, 크기 실측). 사용자 릴리스 없음.
- **M4:** JSON 소스젠 이관(§6.2 B) + WMI 대체(§6.2 A: 디스크 클래스 삭제 + CPUID) + COM 점검(§6.2 C).
- **M5:** 진입점 `PublishAot=true`, IL 경고 0화, `build.cs`/CI 전환. **Stage 2 내부 테스트 빌드**(Native AOT).
- **M6:** 전 과정 안정화 확인 후 **최초 사용자 릴리스**. (릴리스 정책: 완료 전까지 내부 테스트만 — §2)

### 진행 현황 (2026-07-24, 브랜치 `feature/avalonia-aot`)

비-UI AOT 사전 작업(S 시리즈)을 M1 이전에 선행 완료. 각 단계는 개별 커밋이며 WPF 상태에서 빌드 0경고 +
TableCloth.Test 66 / Spork.Test 42 통과로 검증됨.

- ✅ **S1 (WMI 제거):** 죽은 코드(Win32DiskDrive/Partition) 삭제 + 하이퍼바이저 감지 CPUID 대체 + `System.Management` 제거.
- ✅ **S2a (PreferenceSettings JSON):** 소스 생성 컨텍스트로 이관.
- ✅ **S2b (Spork 계약 타입 JSON):** SporkAnswers/SporkUserData/InstallRecord 소스젠 이관. → 전체 JsonSerializer 호출이 리플렉션 비의존.
- ✅ **S3 (COM interop):** ShortcutCreator 2곳을 `Shell.Application`+`dynamic` → `IShellLinkW`/`IPersistFile`(GeneratedComInterface)로 대체.
- ✅ **S4 (System.CommandLine):** AOT-clean 검증(변경 불필요).

→ 남은 하드 AOT blocker(비-UI) 없음. Velopack/Sentry/카탈로그 XML 포함 모두 AOT 안전 확인.

**M1 (UI 이음새 디커플링) — 완료.** ViewModel을 "깔끔히 추상화 가능한" WPF 결합에서 분리(WPF 유지 상태, 빌드 0경고 + 테스트 66+42):

- ✅ **M1a (메시지 박스):** Core에 `AppMessageBoxButton/Result/Image` 중립 열거형 추가, `IAppMessageBox`(양 앱) 중립화(WPF 매핑은 구현층). QuickStartVM의 직접 `IMessageBoxService`/`IApplicationService` 의존 제거.
- ✅ **M1b (테마 매니저):** `IVisualThemeManager.ApplyAutoThemeChange()` 매개변수 없는 오버로드 추가 → Spork MainVM에서 `Application.Current`/WPF Window 결합 제거.
- ✅ **M1c (네비게이션):** `INavigationService`의 WPF `Frame` 메서드를 구현 private로 강등 → 공개 계약 완전 중립.
- **M3로 이관(본질적 뷰 계층 타입):** `CollectionViewSource`/`ICollectionView`(CatalogPageVM·Spork MainVM의 검색/필터), DetailPageVM의 `Clipboard`·`ImageSource`. Avalonia 대체(컬렉션 뷰·`Bitmap`)가 프레임워크 종속적이라 view 포팅과 함께 재구성.

**M2 (Avalonia 스캐폴딩 + 수직 슬라이스) — 완료.** "프로브 먼저" 방식으로 실 앱 전환(M3) 전 스택을 `tools/avalonia-slice`에서 실증:

- ✅ **M2a (Avalonia+AOT 실증):** Avalonia 11.3.18 + FluentTheme + 컴파일 바인딩 + CommunityToolkit.Mvvm 소스젠 +
  한글 IME + 런타임 테마 스왑. **AOT publish IL 경고 0, 부팅 성공, footprint ~34MB**(exe 17.9MB + Skia/ANGLE/HarfBuzz) — WPF ~90MB 대비 60%+↓.
- ✅ **M2b (AboutWindow 포팅):** `Visibility+Converter`→`IsVisible`, `RichTextBox/FlowDocument`→`SelectableTextBlock`,
  `ItemsControl`+`WrapPanel`+`DataTemplate`(x:DataType) 실증. AOT IL 경고 0, 부팅 성공.
- ✅ **M2c (Host+DI 통합):** `Host.CreateApplicationBuilder` + Lemon.Hosting(1.1.1) + DI로 App/VM 생성(`[ActivatorUtilitiesConstructor]`) +
  서비스 주입. **AOT IL 경고 0, 부팅 성공** — DI 활성화가 AOT에서 동작(vNext 미검증 조합 확정).

**M3 확정 이관 항목**(슬라이스가 도출): 원격 아바타 이미지 async 로딩, `Hyperlink` 인라인, `CollectionViewSource`·
`ImageSource`·`Clipboard`(M1 이월), Lemon.Hosting 신 API(`AddAppBuilder`/`RunAvaloniaAppAsync`).

→ 다음은 **M3(App 프로젝트 제자리 전환: UseWPF 제거 + Avalonia 도입 + View 이관 + WPF 제거)**.

#### M3 착수 순서 (콜드 스타트용 — 이 순서로 재개)

전제: 브랜치 `feature/avalonia-aot`(main 대비 15커밋). 검증된 참조 구현은 `tools/avalonia-slice`(Program/App/테마/
AboutWindow 포팅·Host+DI 패턴 그대로 사용). 실 VM은 이미 UI 중립(M1).

1. **테마 시스템 먼저(§5.4):** 공유 Avalonia 테마 리소스(FluentTheme + Light/Dark `ThemeDictionaries`에 기존 브러시 키
   ~109개 재현). 이게 있어야 이후 뷰들이 바인딩됨.
2. **App 프로젝트 전환(§5.1, 프로젝트 단위 빅뱅):** 먼저 **Spork.App**부터 — `UseWPF` 제거 → Avalonia 패키지 +
   `App.axaml` + `Program`/Host 배선(slice의 M2c 패턴, 단 Lemon.Hosting **신 API** `AddAppBuilder`/`RunAvaloniaAppAsync`).
   Spork가 더 자기완결적이라 선행에 적합.
3. **뷰 이관(§5.6 순서):** 가장 단순한 다이얼로그(예: Spork `IdleLogoutWarningWindow`)부터 → 다이얼로그 → 페이지 →
   MainWindow. 각 창마다 slice에서 검증한 이디엄(`IsVisible`, `SelectableTextBlock`, `ItemsControl`/`WrapPanel`) 적용.
4. **M1/M2 이월 처리:** 원격 이미지 async 로더(아바타), `Hyperlink` 인라인, `CollectionViewSource`→Avalonia 컬렉션 뷰/수동 필터,
   `ImageSource`→`Bitmap`, `Clipboard`→`TopLevel.Clipboard`.
5. **TableCloth.App 전환** (Spork 검증 후 동일 패턴) → **WPF 완전 제거** + `Directory.Build.Props`의 `FixWpfReferences` 삭제.
6. **Stage 1 내부 테스트 빌드**: trimmed self-contained 게시 + 크기 실측(§1 표 갱신). 사용자 릴리스 없음(§2).

리스크: 프로젝트가 WPF/Avalonia 공존 불가 → App 프로젝트별 전환은 되돌리기 어려운 빅뱅. 반드시 브랜치에서, 창 단위 커밋 +
기능 패리티 체크(§5.10)로 진행.

#### M3 실행 사양 — 분석 완료 · 확정 설계 (2026-07-24)

Spork.App 전체 표면(뷰 8 + VM + 컴포넌트/인터페이스 + 진입점)을 정독하고, AOT/Avalonia 미확인 요소를 실측으로 해소한 뒤
확정한 설계. **핵심 기술 unknown 2건이 해소되어 최대 리스크가 제거됨:**

1. **동기 모달 메시지박스 가능(핵심).** `Avalonia.Threading.Dispatcher.PushFrame(DispatcherFrame)`가 11.3.18에 public으로
   존재(HandMirror 실측). WPF `MessageBox.Show`의 **동기 블로킹 시맨틱을 중첩 디스패처 펌프로 재현** → `IAppMessageBox`/
   `IMessageBoxService`를 async로 바꾸는 대규모 리플 불필요(VM 호출부 다수 무변경). 의존성 추가 없이 커스텀
   `MessageBoxWindow`(AXAML) 자작(S3의 자작 COM interop와 동일 노선).
2. **RichText가 평문 문자열.** `PrecautionsWindowVM.CautionContent`·`AboutWindowVM.LicenseDescription`은 FlowDocument XAML이
   아니라 평문 `string`. → `RichTextBox`+`RichTextBoxHelper.DocumentXaml` → `SelectableTextBlock Text=`로 **무손실 대체**
   (자동 하이퍼링크 감지만 상실 — 후속 선택 개선). `RichTextBoxHelper`/`Controls/`는 삭제.

**확정 결정(양 앱 공통 적용):**
- **테마:** WPF 4테마(각 ~4.5k줄, aero2 컨트롤 템플릿 덤프) 폐기 → `FluentTheme` + `ThemeVariant.Default`(OS 자동 추종).
  뷰가 참조하는 브러시 키만 `ResourceDictionary.ThemeDictionaries`(Light/Dark)로 얇게 재현. Colourful 값은 accent(`ControlPrimary*`)에만.
  `HwndSource`/레지스트리/WndProc 수동 테마 감지(`VisualThemeManager`) → Avalonia 자동 추종으로 대체(코드 대폭 축소).
  `MainWindowStyle`/Expander/FocusVisual 등 WPF 스타일 참조는 FluentTheme가 대체 → 뷰에서 제거.
- **다이얼로그 결과:** WPF `Window.DialogResult` 세터 없음 → VM의 `CloseRequested(DialogRequestEventArgs)` → `Close(e.DialogResult)`,
  호출측은 `await ShowDialog<bool?>(owner)`. 동기 `ShowDialog()==true`(AhnLab, 백그라운드 스텝 스레드)는
  `Dispatcher.UIThread.Invoke`로 마셜 후 PushFrame 동기 대기.
- **CueBanner 워터마크(VisualBrush 해킹):** Avalonia `TextBox.Watermark` 네이티브 속성으로 대체 → `CueBannerBrush` 키 삭제.
- **컬렉션 뷰/그룹화(M1 이월):** `System.Windows.Data.CollectionViewSource`/`PropertyGroupDescription`(Spork MainVM 사이트 카탈로그
  검색·필터·카테고리 그룹) → **VM측 계산 그룹 컬렉션**(`ObservableCollection<그룹>` 재구성)으로 대체. 의존성 추가 없음.
  뷰는 그룹 `ItemsControl`(헤더+내부 `WrapPanel`), 카드 클릭은 코드비하인드 훅 제거하고 `Command`+`CommandParameter={Binding}`로.
- **템플릿+트리거 컨트롤:** 즐겨찾기 별(ToggleButton)/설치 배지(Button hover-morph)의 `ControlTemplate.Triggers` →
  Avalonia `Style` + 의사클래스(`:checked`/`:pointerover`/`:pressed`). 배지 hover-morph는 1차 단순화(정적 체크 배지+툴팁), 후속 개선.
- **런타임/호스트:** M2c 실증 패턴(`Host.CreateApplicationBuilder`+`Lemon.Hosting`+DI, `[ActivatorUtilitiesConstructor]`)을 실 앱에 적용.
  1차는 저위험 위해 M2c 검증 API 유지(CS0618 억제) → 신 API(`AddAppBuilder`/`RunAvaloniaAppAsync`) 이관은 green 확보 후 시도.
  `Application.Properties[IServiceProvider]`(Init/GetServiceProvider) → Avalonia에 없음 → App 인스턴스 정적 홀더로 대체.
- **ServiceLogoConverter:** WPF `BitmapImage`(원격 URI 자동 async) → Avalonia `Bitmap`/`IImage`. 로컬 파일은 즉시,
  원격 아바타/로고 async 로딩은 이월(1차 로컬 우선, 원격 실패시 null).

**진행 방식:** 프로젝트 단위 빅뱅(중간 빌드 체크 불가)이므로 **앱 단위로 green 검증 후 커밋**. Spork.App(자기완결적) 먼저 →
TableCloth.App(네비게이션 Frame/Page·CollectionViewSource·ImageSource·Clipboard·XamlRadialProgressBar 추가 난점) → WPF 완전 제거 →
Stage 1 내부 테스트 빌드. green 미달 시 미커밋 상태로 정확히 보고(브랜치라 되돌리기 자명).

#### M3 완료 (2026-07-24) — WPF 전면 제거 + Stage 1 실측

**M3 전 단계 완료.** 두 앱을 Avalonia 로 이관하고 WPF 를 솔루션에서 완전히 제거. 각 앱 단위로 빌드 0경고/0오류 +
TableCloth.Test 66 / Spork.Test 42 통과를 확인하며 개별 커밋.

- ✅ **Spork.App 전환**(커밋 `e771597`): App.axaml/Host(Lemon.Hosting), FluentTheme+Colors.axaml, 뷰 8종, 자작 동기
  MessageBoxWindow(Dispatcher.PushFrame), CollectionViewSource→VM 그룹, IdleGuard/SessionIdleMonitor 이관.
- ✅ **TableCloth.App 전환**(커밋 `4e50fc9`): 스플래시→메인창 라이프사이클, Frame/Page→ContentControl+UserControl 네비게이션,
  라이선스 게이트를 App 로 이관, 6 다이얼로그+3 페이지+MainWindow+Splash+License, 캐리오버 전부 처리
  (ImageSource→IImage, Clipboard→TopLevel.Clipboard, OpenFile/FolderDialog→StorageProvider, XamlRadialProgressBar→ProgressBar,
  RichTextBox→SelectableTextBlock, PasswordBox→TextBox).
- ✅ **WPF 완전 제거**(커밋 `c50c9ce`): 진입점 MessageBox→Win32(user32) P/Invoke, `UseWPF` 전부 off, `FixWpfReferences` 폐기,
  CPM 에서 Behaviors.Wpf/XamlRadialProgressBar 제거. 솔루션 `using System.Windows` 0건.
- **M3 확정 이관 항목 처리 현황:** 원격 아바타 이미지 async 로딩 = **이월(플레이스홀더)**; Hyperlink 인라인 = 링크형 Button 로 대체;
  CollectionViewSource/ImageSource/Clipboard = 완료; Lemon.Hosting = 1차 obsolete API 유지(신 API 이관은 후속).

**Stage 1 내부 테스트 빌드 실측(2026-07-24, `dotnet publish -c Release -r win-x64`, trimmed self-contained single-file):**

| 항목 | 크기 |
| ---- | ---- |
| WPF 기준선(단일 파일) | ~90 MB |
| **Avalonia Stage 1 `TableCloth.exe`(트리밍+R2R+압축)** | **~36 MB (37,939,482 bytes) — 약 60% 감소** |

- 트림 경고 IL 10건(카탈로그 정렬 `EnumDisplayOrderAttribute` 리플렉션, 일부 리플렉션 바인딩 등) — 런타임 트림 안전성은
  내부 테스트에서 검증 후 확정(`[DynamicDependency]`/트리머 루트 or `x:CompileBindings` 정리). **사용자 릴리스 없음(§2).**
- 다음(M4/M5): JSON 소스젠·WMI·COM 점검은 S 시리즈에서 선행 완료됨 → **M5 Native AOT(Stage 2)** 로 추가 축소 예정
  (슬라이스 실측 ~34MB AOT). 진입점 `PublishAot=true` + IL 경고 0화 + Lemon.Hosting 신 API 이관.

## 9. 롤백 전략

- 전 작업을 `feature/avalonia-aot`(가칭) 브랜치에서 진행. main은 WPF v1.20.x 유지.
- **완료 전까지 사용자 릴리스 없음(§2).** Stage 1/Stage 2는 내부 테스트 빌드로만 검증하고, 문제 시 브랜치 되돌림으로 롤백.
- Stage 1 완료 시점까지 WPF 코드를 삭제하지 않고 병행 유지(디렉터리/브랜치 분리)해 비교·복귀 여지 확보.

## 10. 열린 질문

- ~~WMI 디스크 지문의 실제 소비처와 요구 정밀도?~~ → **해결(§6.2 A):** 디스크 지문은 죽은 코드(참조 0) → 삭제. 하이퍼바이저 감지만 CPUID로 대체.
- ~~4테마를 accent로 접을지 4변형 유지할지?~~ → **결정:** Light/Dark 2종으로 단순화(§5.4).
- ~~Stage 1을 사용자에게 배포할지?~~ → **결정:** 완료 전까지 릴리스 없음, 내부 테스트만(§2, §9).
- (잔여) Spork/TableCloth 테마 사전 단일화의 배치 위치(`TableCloth.Core` 인접 vs 신규 공유 테마 프로젝트)?

## 부록 — 참조

- [Avalonia Native AOT 문서](https://docs.avaloniaui.net/docs/deployment/native-aot)
- [TableClothVNext](https://github.com/yourtablecloth/TableClothVNext) (Avalonia + `Lemon.Hosting` + 커스텀 Fluent 테마 선례)
- 관련 문서: [DEVELOPMENT.md](../DEVELOPMENT.md), [UI_OVERHAUL_TODO.md](UI_OVERHAUL_TODO.md), [RELEASING.md](RELEASING.md)
