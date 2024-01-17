using TableCloth.Components;

namespace TableCloth.Test;

public class AppMessageBoxTest
{
    private readonly IHost _testHost = TableClothApp.CreateHostBuilder(
        servicesBuilderOverride: services => services
            .ProvideApplication()
            .ReplaceWithMock<IMessageBoxService>()
        ).Build();

    [Fact]
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
}
