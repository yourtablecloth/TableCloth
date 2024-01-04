namespace TableCloth.Test;

public class AppMessageBoxTest
{
    [Fact]
    public void Test_DisplayInfo()
    {
        // Given
        const string message = "Hello, World!";
        const MessageBoxButton button = MessageBoxButton.OK;

        var autoMocker = new AutoMocker();
        autoMocker.Use<IMessageBoxService>(x => x.Show(
            IsAny<Window>(), message, IsAny<string>(), button,
            MessageBoxImage.Information, MessageBoxResult.OK, default) == IsAny<MessageBoxResult>()
        );
        var target = autoMocker.CreateInstance<AppMessageBox>();

        // When
        target.DisplayInfo(message, button);

        // Then
        autoMocker.VerifyAll();
    }
}
