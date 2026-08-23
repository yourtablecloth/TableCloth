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

    private void OpenWebsiteButton_Click(object? sender, RoutedEventArgs e)
        => Exec(ViewModel.OpenWebsiteCommand);

    private void MoreButton_Click(object? sender, RoutedEventArgs e)
        => MoreActionsPanel.IsVisible = !MoreActionsPanel.IsVisible;

    private void MenuUserManual_Click(object? sender, RoutedEventArgs e) => ExecMoreAction(ViewModel.OpenUserManualCommand);
    private void MenuSystemInfo_Click(object? sender, RoutedEventArgs e) => ExecMoreAction(ViewModel.ShowSystemInfoCommand);
    private void MenuCheckUpdate_Click(object? sender, RoutedEventArgs e) => ExecMoreAction(ViewModel.CheckUpdatedVersionCommand);
    private void MenuDiscord_Click(object? sender, RoutedEventArgs e) => ExecMoreAction(ViewModel.OpenDiscordCommand);
    private void MenuPrivacy_Click(object? sender, RoutedEventArgs e) => ExecMoreAction(ViewModel.OpenPrivacyPolicyCommand);

    private void ExecMoreAction(ICommand command)
    {
        MoreActionsPanel.IsVisible = false;
        Exec(command);
    }

    private void Exec(ICommand command)
    {
        if (command.CanExecute(null))
            command.Execute(null);
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
