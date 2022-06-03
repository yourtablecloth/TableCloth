using System;
using System.Windows;
using System.Windows.Input;
using TableCloth.Resources;

namespace Hostess.Commands
{
    internal sealed class ShowErrorMessageCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
            => true;

        public void Execute(object parameter) =>
            MessageBox.Show(
                StringResources.HostessError_PackageInstallFailure(parameter as string), StringResources.AppName,
                MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
    }
}
