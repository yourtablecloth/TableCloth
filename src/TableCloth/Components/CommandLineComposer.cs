using System.Collections.Generic;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public sealed class CommandLineComposer
    {
        public CommandLineComposer(
            SharedLocations sharedLocations)
        {
            this.sharedLocations = sharedLocations;
        }

        private readonly SharedLocations sharedLocations;

        public string ComposeCommandLineExpression(DetailPageViewModel viewModel)
        {
            var targetFilePath = this.sharedLocations.ExecutableFilePath;
            var args = this.ComposeCommandLineArguments(viewModel);
            return $"\"{targetFilePath}\" {args}";
        }

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
