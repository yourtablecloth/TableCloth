using TableCloth.Components;

namespace TableCloth.Test;

public class AppMessageBoxTest
{
    public AppMessageBoxTest()
    {
        _testHost = Program.CreateHostBuilder(
            servicesBuilderOverride: services => services
                .ProvideMockupApplication()
                .ReplaceWithMock<IMessageBoxService>()
            ).Build();
    }

    private readonly IHost _testHost;

    [WpfFact]
    public void AppMessageBox_DisplayInfo()
    {
        // given
        var sut = _testHost.Services.GetRequiredService<IAppMessageBox>();
        var message = "Test";

        // when
        sut.DisplayInfo(message);

        // then
        var mock = _testHost.Services.GetRequiredMock<IMessageBoxService>();
        mock.Verify(x => x.Show(
            IsAny<Window>(),
            message,
            UIStringResources.TitleText_Info,
            MessageBoxButton.OK,
            MessageBoxImage.Information,
            MessageBoxResult.OK,
            IsAny<MessageBoxOptions>())
        );
    }

    [WpfFact]
    public void AppMessageBox_DisplayError_NonCritical()
    {
        // given
        var sut = _testHost.Services.GetRequiredService<IAppMessageBox>();
        var message = "Test Error";
        var isCritical = false;

        // when
        sut.DisplayError(message, isCritical);

        // then
        var mock = _testHost.Services.GetRequiredMock<IMessageBoxService>();
        mock.Verify(x => x.Show(
            IsAny<Window>(),
            message,
            UIStringResources.TitleText_Warning,
            MessageBoxButton.OK,
            MessageBoxImage.Warning,
            MessageBoxResult.OK,
            IsAny<MessageBoxOptions>())
        );
    }

    [WpfFact]
    public void AppMessageBox_DisplayError_Critical()
    {
        // given
        var sut = _testHost.Services.GetRequiredService<IAppMessageBox>();
        var message = "Test Error";
        var isCritical = true;

        // when
        sut.DisplayError(message, isCritical);

        // then
        var mock = _testHost.Services.GetRequiredMock<IMessageBoxService>();
        mock.Verify(x => x.Show(
            IsAny<Window>(),
            message,
            UIStringResources.TitleText_Error,
            MessageBoxButton.OK,
            MessageBoxImage.Stop,
            MessageBoxResult.OK,
            IsAny<MessageBoxOptions>())
        );
    }
}
