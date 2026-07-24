using Avalonia.Controls;
using Avalonia.Interactivity;
using TableCloth.Resources;

namespace TableCloth.Bootstrap.Dialogs;

public partial class LicenseWindow : Window
{
    public LicenseWindow()
    {
        InitializeComponent();

        // 이슈 #296: WPF HwndSource 기반 수동 테마 적용은 폐기(Avalonia 자동 추종). 리소스 문자열만 주입.
        InstructionLabel.Text = UIStringResources.License_Instruction;
        AgreeButton.Content = UIStringResources.License_AgreeButton;
        DeclineButton.Content = UIStringResources.License_DeclineButton;
        LicenseContentTextBox.Text = UIStringResources.License_Content;
    }

    public bool LicenseAccepted { get; private set; }

    private void AgreeButton_Click(object? sender, RoutedEventArgs e)
    {
        LicenseAccepted = true;
        Close(true);
    }

    private void DeclineButton_Click(object? sender, RoutedEventArgs e)
    {
        LicenseAccepted = false;
        Close(false);
    }
}
