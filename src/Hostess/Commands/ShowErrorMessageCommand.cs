using Hostess.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TableCloth.Resources;

namespace Hostess.Commands
{
    internal sealed class ShowErrorMessageCommand : CommandBase
    {
        public override void Execute(object parameter)
        {
            var services = App.Current.Services;
            var appMessageBox = services.GetRequiredService<AppMessageBox>();
            appMessageBox.DisplayError(StringResources.HostessError_PackageInstallFailure(parameter as string), true);
        }
    }
}
