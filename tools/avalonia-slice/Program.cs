using System;
using Avalonia;

namespace AvaloniaSlice;

internal static class Program
{
    // Native AOT 검증용 진입점. Avalonia 데스크톱 앱이 AOT 상태에서 부팅/렌더링되는지 실증한다.
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
