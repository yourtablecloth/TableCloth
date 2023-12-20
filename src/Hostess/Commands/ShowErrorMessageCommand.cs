using System;
using System.Windows;
using System.Windows.Input;
using TableCloth.Resources;

namespace Hostess.Commands
{
    internal sealed class ShowErrorMessageCommand : ICommand
    {
        private EventHandler _canExecuteChanged;

        public event EventHandler CanExecuteChanged
        {
            add => _canExecuteChanged += value;
            remove => _canExecuteChanged -= value;
        }

        public bool CanExecute(object parameter)
            => true;

        public void Execute(object parameter) =>
            MessageBox.Show(
                StringResources.HostessError_PackageInstallFailure(parameter as string), StringResources.AppName,
                MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
    }
}
