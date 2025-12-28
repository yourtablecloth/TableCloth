using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spork.Components;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.Resources;

namespace Spork.ViewModels
{
    public partial class PrecautionsWindowViewModelForDesigner : PrecautionsWindowViewModel { }

    public partial class PrecautionsWindowViewModel : ObservableObject
    {
        protected PrecautionsWindowViewModel() { }

        public PrecautionsWindowViewModel(
            IResourceCacheManager resourceCacheManager,
            ICommandLineArguments commandLineArguments,
            TaskFactory taskFactory)
        {
            _resourceCacheManager = resourceCacheManager;
            _commandLineArguments = commandLineArguments;
            _taskFactory = taskFactory;
        }

        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly TaskFactory _taskFactory;

        [RelayCommand]
        private void PrecautionsWindowLoaded()
        {
            var catalog = _resourceCacheManager.CatalogDocument;
            var parsedArgs = _commandLineArguments.GetCurrent();
            var targets = parsedArgs.SelectedServices;

            var buffer = new StringBuilder();

            foreach (var eachItem in catalog.Services.Where(x => targets.Contains(x.Id)))
            {
                buffer.AppendLine($"[{eachItem.DisplayName} {UIStringResources.Spork_Warning_Title}]");
                buffer.AppendLine();
                buffer.AppendLine(eachItem.CompatibilityNotes);
                buffer.AppendLine();
            }

            CautionContent = buffer.ToString();
        }

        [RelayCommand]
        private Task PrecautionsWindowClose()
        {
            return _taskFactory.StartNew(
                () => CloseRequested?.Invoke(this, new DialogRequestEventArgs(true)),
                default(CancellationToken));
        }

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        [ObservableProperty]
        private string _cautionContent;
    }
}
