using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
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
        this.DataContext = aboutWindowViewModel;
    }

    private void OkayButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
