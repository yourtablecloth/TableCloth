using System;
using System.Linq;
using System.Windows;

namespace TableCloth.Components.Implementations;

public sealed class ApplicationService(IVisualThemeManager visualThemeManager) : IApplicationService
{
    // Application은 DI로 받지 않고 WPF 표준 정적 참조를 사용한다. DI에서 Application을 받으면
    // TableClothApplication 팩토리가 다시 호출되면서 순환 의존이 발생하여
    // "Cannot create more than one System.Windows.Application instance"가 던져질 수 있다.
    // (TableCloth 경로는 Application_Startup이 SYNC라 현재 즉시 폭발하진 않지만, 일관성을 위해
    //  Spork.App과 같은 방식으로 정렬.)
    private static Application Application
        => System.Windows.Application.Current
           ?? throw new InvalidOperationException("Application.Current is not yet available.");

    public object? DispatchInvoke(Delegate @delegate, object?[] arguments)
    {
        var dispatcher = Application.Dispatcher!;
        return dispatcher.Invoke(@delegate, arguments);
    }

    // https://stackoverflow.com/questions/2038879/refer-to-active-window-in-wpf
    public Window? GetActiveWindow()
        => DispatchInvoke(
            (Application _application) => _application.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive),
            [Application]) as Window;

    public Window? GetMainWindow()
        => DispatchInvoke(
            (Application _application) => _application!.MainWindow!,
            [Application]) as Window;

    public void ApplyCosmeticChange(Window? targetWindow)
        => DispatchInvoke(
            (Window _targetWindow, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_targetWindow),
            [targetWindow, visualThemeManager]);

    public void ApplyCosmeticChangeToMainWindow()
        => DispatchInvoke(
            (Application _application, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow),
            [Application, visualThemeManager]);

    public void Shutdown(int exitCode = default)
        => DispatchInvoke(
            (Application _application, int _exitCode) => _application.Shutdown(_exitCode),
            [Application, exitCode]);
}
