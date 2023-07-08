using Microsoft.Extensions.DependencyInjection;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public class ViewModelLocator
    {
        public MainWindowViewModel MainWindowViewModel
            => Program.ServiceProvider.GetService<MainWindowViewModel>();

        public CertSelectWindowViewModel CertSelectWindowViewModel
            => Program.ServiceProvider.GetService<CertSelectWindowViewModel>();

        public AboutWindowViewModel AboutWindowViewModel
            => Program.ServiceProvider.GetService<AboutWindowViewModel>();

        public InputPasswordWindowViewModel InputPasswordWindowViewModel
            => Program.ServiceProvider.GetService<InputPasswordWindowViewModel>();
    }
}
