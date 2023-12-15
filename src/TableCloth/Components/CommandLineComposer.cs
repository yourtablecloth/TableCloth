using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public sealed class CommandLineComposer
    {
        public CommandLineComposer(
            ILogger<CommandLineComposer> logger)
        {
            this.logger = logger;
        }

        private readonly ILogger logger;

        public string ComposeCommandLineArguments(DetailPageViewModel viewModel)
        {
            var options = new List<string>();

            if (viewModel.EnableMicrophone)
                options.Add(StringResources.TableCloth_Switch_EnableMicrophone);
            if (viewModel.EnableWebCam)
                options.Add(StringResources.TableCloth_Switch_EnableCamera);
            if (viewModel.EnablePrinters)
                options.Add(StringResources.TableCloth_Switch_EnablePrinter);
            if (viewModel.InstallEveryonesPrinter)
                options.Add(StringResources.TableCloth_Switch_InstallEveryonesPrinter);
            if (viewModel.InstallAdobeReader)
                options.Add(StringResources.TableCloth_Switch_InstallAdobeReader);
            if (viewModel.InstallHancomOfficeViewer)
                options.Add(StringResources.TableCloth_Switch_InstallHancomOfficeViewer);
            if (viewModel.InstallRaiDrive)
                options.Add(StringResources.TableCloth_Switch_InstallRaiDrive);
            if (viewModel.EnableInternetExplorerMode)
                options.Add(StringResources.TableCloth_Switch_EnableIEMode);
            if (viewModel.MapNpkiCert)
                options.Add(StringResources.Tablecloth_Switch_EnableCert);

            var firstSite = viewModel.SelectedService;

            if (firstSite != null)
                options.Add(firstSite.Id);

            return string.Join(' ', options.ToArray());
        }
    }
}
