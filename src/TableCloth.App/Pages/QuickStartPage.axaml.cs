using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TableCloth.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using TableCloth.ManagedAi;
using TableCloth.Dialogs;
using TableCloth;

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

    private async void Start_Click(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanStart)
            return;

        ViewModel.IsStarting = true;
        try
        {
            ViewModel.DismissSuggestions();
            var route = ViewModel.ClassifyStart();
            switch (route.Kind)
            {
                case QuickStartRouteKind.EmptySandbox:
                    await ViewModel.LaunchSandboxCommand.ExecuteAsync(null);
                    break;
                case QuickStartRouteKind.WebAddress:
                    ViewModel.WebAddress = route.Value;
                    await ViewModel.OpenWebAddressCommand.ExecuteAsync(null);
                    break;
                case QuickStartRouteKind.CatalogSearch:
                    await OpenCatalogAsync(route.Value);
                    break;
                case QuickStartRouteKind.SpecialCommand:
                    ViewModel.ShowSpecialCommandResult(route.Value);
                    break;
                case QuickStartRouteKind.AiQuestion:
                    await OpenManagedAiAsync(route.Value);
                    break;
            }
        }
        finally
        {
            ViewModel.IsStarting = false;
        }
    }

    private void Suggestion_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: QuickStartSuggestion suggestion })
            return;

        AcceptSuggestion(suggestion);
    }

    private void AcceptSuggestion(QuickStartSuggestion suggestion)
    {
        ViewModel.AcceptSuggestion(suggestion);
        StartInput.Focus();
        StartInput.CaretIndex = StartInput.Text?.Length ?? 0;
    }

    private void StartInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel.HasSuggestions)
        {
            ViewModel.DismissSuggestions();
            e.Handled = true;
        }
        else if ((e.Key is Key.Down or Key.Up) && ViewModel.HasSuggestions)
        {
            var buttons = GetSuggestionButtons();
            if (buttons.Length > 0)
            {
                FocusSuggestion(buttons[e.Key == Key.Down ? 0 : buttons.Length - 1]);
                e.Handled = true;
            }
        }
    }

    private void Suggestion_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (e.Key == Key.Escape)
        {
            ViewModel.DismissSuggestions();
            StartInput.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && button.DataContext is QuickStartSuggestion suggestion)
        {
            AcceptSuggestion(suggestion);
            e.Handled = true;
            return;
        }

        if (e.Key is not (Key.Down or Key.Up))
            return;

        var buttons = GetSuggestionButtons();
        var index = System.Array.IndexOf(buttons, button);
        if (index < 0)
            return;
        var next = index + (e.Key == Key.Down ? 1 : -1);
        if (next >= 0 && next < buttons.Length)
            FocusSuggestion(buttons[next]);
        else if (next < 0)
            StartInput.Focus();
        e.Handled = true;
    }

    private static void FocusSuggestion(Button button)
    {
        button.Focus();
        button.BringIntoView();
    }

    private Button[] GetSuggestionButtons()
    {
        var buttons = StartSuggestions.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("start-suggestion")).ToArray();
        if (buttons.Length == 0 && ViewModel.HasSuggestions)
        {
            StartSuggestions.UpdateLayout();
            buttons = StartSuggestions.GetVisualDescendants().OfType<Button>()
                .Where(button => button.Classes.Contains("start-suggestion")).ToArray();
        }
        return buttons;
    }

    private async void ManagedAi_Click(object? sender, RoutedEventArgs e) => await OpenManagedAiAsync(null);

    internal async Task OpenManagedAiAsync(string? initialPrompt)
    {
        var services = TableClothApplication.ServiceProvider;
        if (services is null) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var window = services.GetRequiredService<ManagedAiWindow>();
        if (initialPrompt is not null) window.SetInitialPrompt(initialPrompt);
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        await window.ShowDialog(owner);
    }

    private async void OpenCatalog_Click(object? sender, RoutedEventArgs e) => await OpenCatalogAsync(null);

    internal async Task OpenCatalogAsync(string? nameQuery)
    {
        var services = TableClothApplication.ServiceProvider;
        if (services is null) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var window = services.GetRequiredService<CatalogWindow>();
        if (nameQuery is not null) window.ViewModel.SearchByName(nameQuery);
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        await window.ShowDialog(owner);
    }
}
