using System;

namespace Hostess.Commands
{
    public abstract class ViewModelCommandBase<TViewModel> : CommandBase
        where TViewModel : class
    {
        protected ViewModelCommandBase()
        {
        }

        public override void Execute(object parameter)
        {
            if (!(parameter is TViewModel viewModel))
                throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

            Execute(viewModel);
        }

        public abstract void Execute(TViewModel viewModel);
    }
}
