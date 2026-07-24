using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using System.Windows.Input;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();

    public AboutWindow(
        AboutWindowViewModel aboutWindowViewModel)
    {
        InitializeComponent();
        DataContext = aboutWindowViewModel;
        Loaded += OnLoaded;
    }

    public AboutWindowViewModel ViewModel
        => (AboutWindowViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.AboutWindowLoadedCommand.CanExecute(ViewModel))
            ViewModel.AboutWindowLoadedCommand.Execute(ViewModel);
    }

    private void OkayButton_Click(object? sender, RoutedEventArgs e)
        => Close();

    // 이슈 #296: '더 보기(⋯)' 드롭다운의 항목들. 플라이아웃(팝업) 내부 바인딩의 DataContext 전파 이슈를 피하기 위해
    // 코드비하인드에서 VM 커맨드를 직접 실행한다(WPF 의 CommandParameter=DataContext 와 동일하게 VM 을 인자로 전달).
    private void MenuUserManual_Click(object? sender, RoutedEventArgs e) => Exec(ViewModel.OpenUserManualCommand);
    private void MenuSystemInfo_Click(object? sender, RoutedEventArgs e) => Exec(ViewModel.ShowSystemInfoCommand);
    private void MenuCheckUpdate_Click(object? sender, RoutedEventArgs e) => Exec(ViewModel.CheckUpdatedVersionCommand);
    private void MenuDiscord_Click(object? sender, RoutedEventArgs e) => Exec(ViewModel.OpenDiscordCommand);
    private void MenuPrivacy_Click(object? sender, RoutedEventArgs e) => Exec(ViewModel.OpenPrivacyPolicyCommand);

    private void Exec(ICommand command)
    {
        if (command.CanExecute(ViewModel))
            command.Execute(ViewModel);
    }

    private void SponsorBanner_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://yourtablecloth.app/#sponsor",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
