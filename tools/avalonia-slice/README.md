# Avalonia + Native AOT 수직 슬라이스 (이슈 #296 · M2)

WPF → Avalonia 전환([docs/AVALONIA_AOT_MIGRATION.md](../../docs/AVALONIA_AOT_MIGRATION.md))의 **M2 단계**에서,
실 앱을 건드리기 전에 **Avalonia UI 스택이 Native AOT에서 실제로 렌더링·동작하는지** 실증하는 독립 슬라이스다.
S 시리즈의 `tools/aot-probe`와 동일한 "프로브 먼저" 방식이며, 메인 솔루션(`TableCloth.slnx`)에 포함되지 않는다.

## 검증 대상

- **Avalonia 11.3.18** (`Avalonia.Desktop` + `Avalonia.Themes.Fluent` + `Avalonia.Fonts.Inter`)
- **FluentTheme** + 런타임 Light/Dark 테마 스왑(`Application.RequestedThemeVariant`)
- **컴파일 바인딩**(`AvaloniaUseCompiledBindingsByDefault` + `x:DataType`) — AOT 필수
- **CommunityToolkit.Mvvm 소스젠**(`[ObservableProperty]`/`[RelayCommand]`) — 실 앱 VM과 동일 패턴
- **한글 IME** 입력(TextBox 두 방향 바인딩)
- `ItemsControl` 컬렉션 바인딩

## 실행

Native AOT 링크에는 MSVC C++ 툴체인이 필요하다(`vswhere.exe`가 PATH에 있어야 함).

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;" + $env:PATH
dotnet publish -c Release -r win-x64 -p:PublishAot=true
& .\bin\Release\net10.0-windows\win-x64\publish\avalonia-slice.exe
# 개발 실행(비 AOT): dotnet run
```

## 실측 결과 (2026-07-24, .NET 10.0.302 / ILCompiler 10.0.10 / Avalonia 11.3.18)

- **AOT publish: IL 경고 0, 오류 0.** 완전히 AOT-clean.
- **AOT 앱 부팅 성공** — 시작 시 크래시 없이 창 렌더링(FluentTheme/한글/테마 스왑 동작).
- 산출 footprint:

| 파일 | 크기 | 비고 |
| ---- | ---- | ---- |
| `avalonia-slice.exe` | 17.9 MB | 트리밍된 런타임 + Avalonia 관리 코드 + 앱(AOT) |
| `libSkiaSharp.dll` | 9.2 MB | 렌더링(Skia) 네이티브 |
| `av_libglesv2.dll` | 5.3 MB | ANGLE(OpenGL ES) 네이티브 |
| `libHarfBuzzSharp.dll` | 1.8 MB | 텍스트 셰이핑 네이티브 |
| **합계** | **~34 MB** | 현재 WPF 단일 파일 ~90MB 대비 60%+ 감소 |

**결론(M2a):** Avalonia + Native AOT UI 스택이 이 프로젝트에서 동작함을 실증. 컴파일 바인딩 + CommunityToolkit.Mvvm
소스젠이 AOT에서 경고 0으로 통과하므로, 실 앱의 (M1에서 중립화된) ViewModel을 그대로 바인딩할 수 있다.

## M2b — AboutWindow 포팅 검증 (`AboutWindow.axaml` + `AboutViewModel`)

기존 WPF `AboutWindow.xaml`(가장 요소가 다양한 다이얼로그 중 하나)을 Avalonia로 포팅해 view 포팅 이디엄을 실증.
실 `AboutWindowViewModel`의 바인딩 표면(속성/커맨드 이름)을 그대로 미러링(샘플 데이터).

WPF → Avalonia 매핑 실증:

| WPF | Avalonia | 결과 |
| --- | -------- | ---- |
| `Visibility="{Binding X, Converter=BoolToVis}"` | `IsVisible="{Binding X}"` | ✅ 컨버터 제거 |
| `RichTextBox` + `RichTextBoxHelper.DocumentXaml`(FlowDocument) | `SelectableTextBlock`(ScrollViewer) | ✅ 스크롤/한글 렌더 |
| `ItemsControl` + `WrapPanel` + `DataTemplate` | 동일(`x:DataType` 컴파일 템플릿) | ✅ |
| `Ellipse`+`ImageBrush ImageSource="{Binding AvatarUrl}"`(원격 URL) | 플레이스홀더 Ellipse | ⚠️ 원격 이미지 async 로더는 **M3** |
| `i:Interaction.Triggers`(Loaded) / `Hyperlink` | 코드비하인드/링크형 Button | ✅ (M3에선 Xaml.Behaviors.Avalonia 검토) |

**M2b 실측:** Debug 빌드 0오류(컴파일 바인딩 전부 검증), **AOT publish IL 경고 0, exe 18.0MB, 부팅 성공.**

**남은 M3 이관 항목(이 슬라이스가 확정):** 원격 아바타 이미지 async 로딩, `Hyperlink` 인라인(현재 Button 대체),
그리고 (본 슬라이스 밖) `CollectionViewSource`·`ImageSource`·`Clipboard`.

## M2c (예정)

`Lemon.Hosting.AvaloniauiDesktop`(1.1.1) + `Microsoft.Extensions.Hosting` + Avalonia의 **AOT 통합** 검증
(vNext는 이 조합을 AOT로 실증한 적 없음). 이후 **M3**: App 프로젝트 제자리 전환 + WPF 제거.
