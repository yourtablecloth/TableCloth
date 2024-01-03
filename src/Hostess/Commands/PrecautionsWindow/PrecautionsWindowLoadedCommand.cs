using Hostess.Components;
using Hostess.ViewModels;
using System;
using System.Linq;
using System.Text;
using TableCloth.Models;
using TableCloth.Resources;

namespace Hostess.Commands.PrecautionsWindow
{
    public sealed class PrecautionsWindowLoadedCommand : ViewModelCommandBase<PrecautionsWindowViewModel>
    {
        public PrecautionsWindowLoadedCommand(
            SharedProperties sharedProperties,
            CommandLineArguments commandLineArguments)
        {
            _sharedProperties = sharedProperties;
            _commandLineArguments = commandLineArguments;
        }

        private readonly SharedProperties _sharedProperties;
        private readonly CommandLineArguments _commandLineArguments;

        public override void Execute(PrecautionsWindowViewModel viewModel)
        {
            var catalog = _sharedProperties.GetCatalogDocument();
            var parsedArgs = _commandLineArguments.Current;
            var targets = parsedArgs.SelectedServices;

            var buffer = new StringBuilder();

            foreach (var eachItem in catalog.Services.Where(x => targets.Contains(x.Id)))
            {
                buffer.AppendLine($"[{eachItem.DisplayName} {StringResources.Hostess_Warning_Title}]");
                buffer.AppendLine(eachItem.CompatibilityNotes);
            }

            viewModel.CautionContent = buffer.ToString();
        }
    }
}
