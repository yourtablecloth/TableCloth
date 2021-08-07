using Microsoft.Extensions.DependencyInjection;

namespace TableCloth.Implementations.WPF
{
    public class ViewModelLocator
    {
        public MainWindowViewModel MainWindowViewModel
            => Program.ServiceProvider.GetService<MainWindowViewModel>();

        public CertSelectWindowViewModel CertSelectWindowViewModel
            => Program.ServiceProvider.GetService<CertSelectWindowViewModel>();
    }
}
