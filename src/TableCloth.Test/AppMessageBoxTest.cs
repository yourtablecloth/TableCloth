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
        var mock = _testHost.Services.GetRequiredService<Mock<IMessageBoxService>>();

        // when
        appMessageBox.DisplayInfo("Test");

        // then
        mock.Verify(x => x.Show(
            IsAny<Window>(),
            IsNotNull<string>(),
            UIStringResources.TitleText_Info,
            IsAny<MessageBoxButton>(),
            MessageBoxImage.Information,
            MessageBoxResult.OK,
            IsAny<MessageBoxOptions>())
        );
    }
}
