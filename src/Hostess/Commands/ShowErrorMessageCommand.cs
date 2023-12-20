using System.Windows;
using TableCloth.Resources;

namespace Hostess.Commands
{
    internal sealed class ShowErrorMessageCommand : CommandBase
    {
        public override void Execute(object parameter)
            => MessageBox.Show(
                StringResources.HostessError_PackageInstallFailure(parameter as string), StringResources.AppName,
                MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
    }
}
