using System;
using System.Linq;
using System.Windows;

namespace TableCloth.Components;

public sealed class ApplicationService(
    Application? application,
    IVisualThemeManager visualThemeManager) : IApplicationService
{
    public object? DispatchInvoke(Delegate @delegate, object?[] arguments)
    {
        var dispatcher = application!.Dispatcher!;
        return dispatcher.Invoke(@delegate, arguments);
    }

    // https://stackoverflow.com/questions/2038879/refer-to-active-window-in-wpf
    public Window? GetActiveWindow()
        => DispatchInvoke(
            (Application _application) => _application.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive),
            new object?[] { application! }) as Window;

    public Window? GetMainWindow()
        => DispatchInvoke(
            (Application _application) => _application!.MainWindow!,
            new object?[] { application! }) as Window;

    public void ApplyCosmeticChange(Window? targetWindow)
        => DispatchInvoke(
            (Window _targetWindow, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_targetWindow),
            new object?[] { targetWindow, visualThemeManager });

    public void ApplyCosmeticChangeToMainWindow()
        => DispatchInvoke(
            (Application _application, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow),
            new object?[] { application, visualThemeManager });

    public void Shutdown(int exitCode = default)
        => DispatchInvoke(
            (Application _application, int _exitCode) => _application.Shutdown(_exitCode),
            new object?[] { application, exitCode });
}
