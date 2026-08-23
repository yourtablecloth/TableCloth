using Avalonia.Controls;

namespace TableCloth.Components.Implementations;

// 이슈 #296: WPF 수동 테마 감지(HwndSource/레지스트리/WndProc + Colourful 스왑) 폐기. Avalonia FluentTheme +
// RequestedThemeVariant="Default" 가 OS 라이트/다크를 자동 추종하므로 no-op(계약 유지).
public sealed class VisualThemeManager : IVisualThemeManager
{
    public void ApplyAutoThemeChange(Window targetWindow) { }
}
