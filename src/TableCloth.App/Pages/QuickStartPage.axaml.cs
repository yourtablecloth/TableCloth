using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;
using TableCloth.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using TableCloth.ManagedAi;

namespace TableCloth.Pages;

public partial class QuickStartPage : UserControl
{
    public QuickStartPage() => InitializeComponent();

    public QuickStartPage(QuickStartPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    public QuickStartPageViewModel ViewModel
        => (QuickStartPageViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.QuickStartPageLoadedCommand.CanExecute(ViewModel))
            ViewModel.QuickStartPageLoadedCommand.Execute(ViewModel);
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

    private void ManagedAi_Click(object? sender, RoutedEventArgs e)
    {
        var services = TableClothApplication.ServiceProvider;
        if (services is null) return;
        var window = services.GetRequiredService<ManagedAiWindow>();
        if (TopLevel.GetTopLevel(this) is Window owner) window.Show(owner);
        else window.Show();
    }
}
