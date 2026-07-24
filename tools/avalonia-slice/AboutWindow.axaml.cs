using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaSlice;

public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();

    private void OkayButton_Click(object? sender, RoutedEventArgs e) => Close();
}
