using System;
using System.Text;
using System.Windows;
using TableCloth.Components;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Dialogs;

public partial class InputPasswordWindow : Window
{
    public InputPasswordWindow(
        X509CertPairScanner certPairScanner,
        AppMessageBox appMessageBox)
    {
        _certPairScanner = certPairScanner;
        _appMessageBox = appMessageBox;

        InitializeComponent();
    }

    private readonly X509CertPairScanner _certPairScanner;
    private readonly AppMessageBox _appMessageBox;

    public string? PfxFilePath { get; set; }

    public X509CertPair? ValidatedCertPair { get; private set; }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var lines = new StringBuilder();
        lines.AppendLine(string.Format((string)CertInformation.Tag, PfxFilePath));
        CertInformation.Text = lines.ToString();
        PasswordInput.Focus();
    }

    private void OkayButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (PfxFilePath == null)
                throw new InvalidOperationException(StringResources.Error_Cannot_Find_PfxFile);

            var certPair = _certPairScanner.CreateX509Cert(PfxFilePath, PasswordInput.SecurePassword);

            if (certPair != null)
                ValidatedCertPair = certPair;

            DialogResult = true;
        }
        catch (Exception ex)
        {
            _appMessageBox.DisplayError(ex, false);
            PasswordInput.Focus();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
