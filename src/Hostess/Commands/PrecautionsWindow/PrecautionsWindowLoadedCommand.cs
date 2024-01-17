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
            IResourceCacheManager resourceCacheManager,
            ICommandLineArguments commandLineArguments)
        {
            _resourceCacheManager = resourceCacheManager;
            _commandLineArguments = commandLineArguments;
        }

        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly ICommandLineArguments _commandLineArguments;

        public override void Execute(PrecautionsWindowViewModel viewModel)
        {
            var catalog = _resourceCacheManager.CatalogDocument;
            var parsedArgs = _commandLineArguments.Current;
            var targets = parsedArgs.SelectedServices;

            var buffer = new StringBuilder();

            foreach (var eachItem in catalog.Services.Where(x => targets.Contains(x.Id)))
            {
                buffer.AppendLine($"[{eachItem.DisplayName} {UIStringResources.Hostess_Warning_Title}]");
                buffer.AppendLine();
                buffer.AppendLine(eachItem.CompatibilityNotes);
                buffer.AppendLine();
            }

            viewModel.CautionContent = buffer.ToString();
        }
    }
}
