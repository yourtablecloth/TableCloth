using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
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
