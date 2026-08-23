using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System;
using System.Linq;

namespace TableCloth.Components.Implementations;

// 이슈 #296: WPF Application 정적 참조 → Avalonia Application/데스크톱 라이프타임. UI 스레드 마셜은 Dispatcher.UIThread.
public sealed class ApplicationService(IVisualThemeManager visualThemeManager) : IApplicationService
{
    private static IClassicDesktopStyleApplicationLifetime? Desktop
        => Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    public object? DispatchInvoke(Delegate @delegate, object?[] arguments)
        => Dispatcher.UIThread.Invoke(() => @delegate.DynamicInvoke(arguments));

    public Window? GetActiveWindow()
        => Dispatcher.UIThread.Invoke(() => Desktop?.Windows.FirstOrDefault(x => x.IsActive));

    public Window? GetMainWindow()
        => Dispatcher.UIThread.Invoke(() => Desktop?.MainWindow);

    public void ApplyCosmeticChange(Window? targetWindow)
    {
        if (targetWindow != null)
            Dispatcher.UIThread.Invoke(() => visualThemeManager.ApplyAutoThemeChange(targetWindow));
    }

    public void ApplyCosmeticChangeToMainWindow()
        => Dispatcher.UIThread.Invoke(() =>
        {
            var main = Desktop?.MainWindow;
            if (main != null)
                visualThemeManager.ApplyAutoThemeChange(main);
        });

    public void Shutdown(int exitCode = default)
        => Dispatcher.UIThread.Invoke(() => Desktop?.Shutdown(exitCode));
}
