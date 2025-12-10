using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Dialogs;

public partial class LicenseWindow : Window
{
    public LicenseWindow()
    {
        InitializeComponent();
        
        // Set UI strings from resources
        InstructionLabel.Content = UIStringResources.License_Instruction;
        AgreeButton.Content = UIStringResources.License_AgreeButton;
        DeclineButton.Content = UIStringResources.License_DeclineButton;
        LicenseContentTextBox.Text = UIStringResources.License_Content;
    }

    public bool LicenseAccepted { get; private set; }

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseAccepted = true;
        DialogResult = true;
        Close();
    }

    private void DeclineButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseAccepted = false;
        DialogResult = false;
        Close();
    }
}
