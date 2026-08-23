using Avalonia;

namespace TableCloth;

/// <summary>
/// 이슈 #296: TableCloth 진입점(TableCloth.exe 기본 verb)이 공유하는 Avalonia <see cref="AppBuilder"/> 구성.
/// </summary>
public static class TableClothAvaloniaApp
{
    public static AppBuilder Configure(AppBuilder builder)
        => builder
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
