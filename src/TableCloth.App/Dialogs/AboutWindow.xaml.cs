using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

/// <summary>
/// Interaction logic for AboutWindow.xaml
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow(
        AboutWindowViewModel aboutWindowViewModel)
    {
        InitializeComponent();
        DataContext = aboutWindowViewModel;
    }

    public AboutWindowViewModel ViewModel
        => (AboutWindowViewModel)DataContext;

    private void OkayButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // 보조 동작(⋯) 버튼: 부착된 ContextMenu 를 버튼 위쪽으로 펼친다. 하단 버튼이라 Top 배치로
    // 메뉴가 창 밖으로 나가지 않게 한다. PlacementTarget 을 지정해야 ContextMenu.DataContext 바인딩
    // (PlacementTarget.DataContext=VM)이 해소된다.
    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is { } contextMenu)
        {
            contextMenu.PlacementTarget = button;
            contextMenu.Placement = PlacementMode.Top;
            contextMenu.IsOpen = true;
        }
    }

    private void SponsorBanner_MouseLeftButtonUp(object sender, RoutedEventArgs e)
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
