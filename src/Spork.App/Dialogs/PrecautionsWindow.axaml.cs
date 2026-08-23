using Avalonia.Controls;
using Avalonia.Interactivity;
using Spork.ViewModels;
using TableCloth.Events;

namespace Spork.Dialogs
{
    public partial class PrecautionsWindow : Window
    {
        public PrecautionsWindow() => InitializeComponent();

        public PrecautionsWindow(
            PrecautionsWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += ViewModel_CloseRequested;
            Loaded += OnLoaded;
        }

        public PrecautionsWindowViewModel ViewModel
            => (PrecautionsWindowViewModel)DataContext!;

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // 이슈 #296: WPF Interaction.Triggers(Loaded) → 코드비하인드 Loaded 훅으로 대체.
            if (ViewModel.PrecautionsWindowLoadedCommand.CanExecute(null))
                ViewModel.PrecautionsWindowLoadedCommand.Execute(null);
        }

        private void ViewModel_CloseRequested(object? sender, DialogRequestEventArgs e)
            => Close(e.DialogResult);
    }
}
