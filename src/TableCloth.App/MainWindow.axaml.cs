using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using TableCloth.ViewModels;

namespace TableCloth;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public MainWindow(
        MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    public MainWindowViewModel ViewModel
        => (MainWindowViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // 이슈 #296: WPF Interaction.Triggers(Loaded) → 코드비하인드 Loaded 훅.
        if (ViewModel.MainWindowLoadedCommand.CanExecute(null))
            ViewModel.MainWindowLoadedCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        // 이슈 #296: WPF Interaction.Triggers(Closed) → OnClosed 오버라이드.
        if (DataContext is MainWindowViewModel vm && vm.MainWindowClosedCommand.CanExecute(null))
            vm.MainWindowClosedCommand.Execute(null);

        base.OnClosed(e);
    }
}
