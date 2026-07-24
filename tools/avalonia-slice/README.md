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

**결론:** Avalonia + Native AOT UI 스택이 이 프로젝트에서 동작함을 실증. 컴파일 바인딩 + CommunityToolkit.Mvvm
소스젠이 AOT에서 경고 0으로 통과하므로, 실 앱의 (M1에서 중립화된) ViewModel을 그대로 바인딩할 수 있다.
다음 단계(M2b)는 AboutWindow 레이아웃을 이 패턴으로 포팅, M3는 App 프로젝트 제자리 전환.
