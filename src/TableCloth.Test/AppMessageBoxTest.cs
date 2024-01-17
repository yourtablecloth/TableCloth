using TableCloth.Components;

namespace TableCloth.Test;

public class AppMessageBoxTest
{
    private readonly IHost _testHost = TableClothApp.CreateHostBuilder(
        servicesBuilderOverride: services => services
            .ProvideApplication<Application>()
            .ReplaceWithMock<IMessageBoxService>()
        ).Build();

    [Fact]
    public void AppMessageBox_DisplayInfo()
    {
        // given
        var appMessageBox = _testHost.Services.GetRequiredService<IAppMessageBox>();
        var message = "Test";

        // when
        appMessageBox.DisplayInfo(message);

        // then
        var messageBoxServiceMock = _testHost.Services.GetRequiredMock<IMessageBoxService>();
        messageBoxServiceMock.Verify(x => x.Show(
            IsAny<Window>(),
            message,
            UIStringResources.TitleText_Info,
            IsAny<MessageBoxButton>(),
            MessageBoxImage.Information,
            MessageBoxResult.OK,
            IsAny<MessageBoxOptions>())
        );
    }
}
