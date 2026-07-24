using Avalonia.Controls;

namespace Spork.Components.Implementations
{
    // 이슈 #296: WPF 시절의 수동 테마 감지(HwndSource/레지스트리/WndProc + Colourful 테마 스왑)는 폐기.
    // Avalonia FluentTheme + App.RequestedThemeVariant="Default" 가 OS 라이트/다크를 자동 추종하므로
    // 본 서비스는 계약 유지를 위한 no-op 이다. (향후 강제 테마 오버라이드가 필요하면 여기에 구현.)
    public sealed class VisualThemeManager : IVisualThemeManager
    {
        public void ApplyAutoThemeChange(Window targetWindow) { }

        public void ApplyAutoThemeChange() { }
    }
}
