using Hostess.Commands.PrecautionsWindow;
using System;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace Hostess.ViewModels
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

        public void RequestClose(object sender, bool? dialogResult)
            => CloseRequested?.Invoke(sender, new DialogRequestEventArgs(dialogResult));

        private string _cautionContent;

        public string CautionContent
        {
            get => _cautionContent;
            set => SetProperty(ref _cautionContent, value);
        }
    }
}
