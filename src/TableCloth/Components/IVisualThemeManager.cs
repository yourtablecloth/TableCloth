using System.Windows;

namespace TableCloth.Components;

public interface IVisualThemeManager
{
    void ApplyAutoThemeChange(Window targetWindow);
}