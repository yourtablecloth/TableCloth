using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaSlice;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OpenAbout_Click(object? sender, RoutedEventArgs e)
        => new AboutWindow { DataContext = new AboutViewModel() }.Show(this);
}
