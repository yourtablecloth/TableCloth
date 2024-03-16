using Spork.Commands.PrecautionsWindow;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace Spork.ViewModels
{
    public class PrecautionsWindowViewModelForDesigner : PrecautionsWindowViewModel { }

    public class PrecautionsWindowViewModel : ViewModelBase
    {
        protected PrecautionsWindowViewModel() { }

        public PrecautionsWindowViewModel(
            PrecautionsWindowLoadedCommand precautionsWindowLoadedCommand,
            PrecautionsWindowCloseCommand precautionsWindowCloseCommand)
        {
            _precautionsWindowLoadedCommand = precautionsWindowLoadedCommand;
            _precautionsWindowCloseCommand = precautionsWindowCloseCommand;
        }

        private readonly PrecautionsWindowLoadedCommand _precautionsWindowLoadedCommand;
        private readonly PrecautionsWindowCloseCommand _precautionsWindowCloseCommand;

        public PrecautionsWindowLoadedCommand PrecautionsWindowLoadedCommand
            => _precautionsWindowLoadedCommand;

        public PrecautionsWindowCloseCommand PrecautionsWindowCloseCommand
            => _precautionsWindowCloseCommand;

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
            => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        private string _cautionContent;

        public string CautionContent
        {
            get => _cautionContent;
            set => SetProperty(ref _cautionContent, value);
        }
    }
}
