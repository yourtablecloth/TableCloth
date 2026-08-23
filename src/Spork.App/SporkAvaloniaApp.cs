using Avalonia;

namespace Spork
{
    /// <summary>
    /// 이슈 #296: Spork 진입점(단독 Spork.exe / 통합 TableCloth.exe spork·idle-guard verb)이 공유하는
    /// Avalonia <see cref="AppBuilder"/> 구성. M2c 슬라이스에서 검증한 구성과 동일.
    /// </summary>
    public static class SporkAvaloniaApp
    {
        public static AppBuilder Configure(AppBuilder builder)
            => builder
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
