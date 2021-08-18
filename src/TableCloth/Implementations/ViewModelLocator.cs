using Microsoft.Extensions.DependencyInjection;
using TableCloth.ViewModels;

namespace TableCloth.Implementations
{
    public class ViewModelLocator
    {
        public MainWindowViewModel MainWindowViewModel
            => Program.ServiceProvider.GetService<MainWindowViewModel>();

        public CertSelectWindowViewModel CertSelectWindowViewModel
            => Program.ServiceProvider.GetService<CertSelectWindowViewModel>();
    }
}
