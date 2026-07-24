using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth;

public partial class SplashScreen : Window
{
    public SplashScreen() => InitializeComponent();

    public SplashScreen(
        SplashScreenViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.StatusUpdate += ViewModel_StatusUpdate;
        Loaded += OnLoaded;
    }

    public SplashScreenViewModel ViewModel
        => (SplashScreenViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 이슈 #296: WPF Interaction.Triggers(Loaded) → 코드비하인드 Loaded 훅.
        if (ViewModel.SplashScreenLoadedCommand.CanExecute(null))
            ViewModel.SplashScreenLoadedCommand.Execute(null);
    }

    private void ViewModel_StatusUpdate(object? sender, StatusUpdateRequestEventArgs e)
        => ViewModel.Status = e.Status;

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 무테두리 스플래시를 드래그로 이동(WPF DragMove 대체).
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
