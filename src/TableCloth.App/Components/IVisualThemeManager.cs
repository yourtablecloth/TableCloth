using Avalonia.Controls;

namespace TableCloth.Components;

public interface IVisualThemeManager
{
    // 이슈 #296: Avalonia FluentTheme + RequestedThemeVariant="Default" 로 OS 라이트/다크 자동 추종. 계약만 유지.
    void ApplyAutoThemeChange(Window targetWindow);
}
