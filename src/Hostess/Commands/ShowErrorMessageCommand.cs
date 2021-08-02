using System;
using System.Windows;
using System.Windows.Input;
using TableCloth.Resources;

namespace Hostess.Commands
{
    internal sealed class ShowErrorMessageCommand : ICommand
    {
#pragma warning disable CS0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object parameter)
            => true;

        public void Execute(object parameter) =>
            _ = MessageBox.Show(
                StringResources.HostessError_PackageInstallFailure(parameter as string), StringResources.AppName,
                MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
    }
}
