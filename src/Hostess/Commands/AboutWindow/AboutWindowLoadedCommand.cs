using Hostess.ViewModels;
using TableCloth.Resources;

namespace Hostess.Commands.AboutWindow
{
    public sealed class AboutWindowLoadedCommand : ViewModelCommandBase<AboutWindowViewModel>
    {
        public override void Execute(AboutWindowViewModel viewModel)
            => viewModel.AppVersion = StringResources.Get_AppVersion();
    }
}
