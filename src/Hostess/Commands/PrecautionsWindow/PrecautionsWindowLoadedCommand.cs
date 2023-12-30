using Hostess.Components;
using Hostess.ViewModels;
using System;
using System.Linq;
using System.Text;
using TableCloth.Resources;

namespace Hostess.Commands.PrecautionsWindow
{
    public sealed class PrecautionsWindowLoadedCommand : ViewModelCommandBase<PrecautionsWindowViewModel>
    {
        public PrecautionsWindowLoadedCommand(
            SharedProperties sharedProperties)
        {
            _sharedProperties = sharedProperties;
        }

        private readonly SharedProperties _sharedProperties;

        public override void Execute(PrecautionsWindowViewModel viewModel)
        {
            var catalog = _sharedProperties.GetCatalogDocument();
            var targets = _sharedProperties.GetInstallSites();
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
