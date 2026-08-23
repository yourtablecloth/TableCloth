using Avalonia.Controls;
using Avalonia.Interactivity;
using Spork.ViewModels;
using System.Diagnostics;
using TableCloth.Events;

namespace Spork.Dialogs
{
    public partial class AboutWindow : Window
    {
        // Avalonia 런타임 XAML 로더/디자이너용. 런타임 인스턴스는 DI(VM 생성자)로 만든다.
        public AboutWindow() => InitializeComponent();

        public AboutWindow(AboutWindowViewModel aboutWindowViewModel)
        {
            InitializeComponent();
            DataContext = aboutWindowViewModel;
            aboutWindowViewModel.CloseRequested += AboutWindowViewModel_CloseRequested;
            Loaded += OnLoaded;
        }

        public AboutWindowViewModel ViewModel
            => (AboutWindowViewModel)DataContext!;

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // 이슈 #296: WPF Interaction.Triggers(Loaded) → 코드비하인드 Loaded 훅으로 대체.
            if (ViewModel.AboutWindowLoadedCommand.CanExecute(null))
                ViewModel.AboutWindowLoadedCommand.Execute(null);
        }

        private void AboutWindowViewModel_CloseRequested(object? sender, DialogRequestEventArgs e)
            => Close(e.DialogResult);

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
}
