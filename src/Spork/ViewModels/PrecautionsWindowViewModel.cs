using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spork.Commands.PrecautionsWindow;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace Spork.ViewModels
{
    public partial class PrecautionsWindowViewModelForDesigner : PrecautionsWindowViewModel { }

    public partial class PrecautionsWindowViewModel : ViewModelBase
    {
        protected PrecautionsWindowViewModel() { }

        public PrecautionsWindowViewModel(
            PrecautionsWindowLoadedCommand precautionsWindowLoadedCommand,
            PrecautionsWindowCloseCommand precautionsWindowCloseCommand)
        {
            _precautionsWindowLoadedCommand = precautionsWindowLoadedCommand;
            _precautionsWindowCloseCommand = precautionsWindowCloseCommand;
        }

        [RelayCommand]
        private void PrecautionsWindowLoaded()
        {
            _precautionsWindowLoadedCommand.Execute(this);
        }

        private PrecautionsWindowLoadedCommand _precautionsWindowLoadedCommand;

        [RelayCommand]
        private void PrecautionsWindowClose()
        {
            _precautionsWindowCloseCommand.Execute(this);
        }

        private PrecautionsWindowCloseCommand _precautionsWindowCloseCommand;

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
            => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        [ObservableProperty]
        private string _cautionContent;
    }
}
