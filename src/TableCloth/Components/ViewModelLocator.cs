using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public sealed class ViewModelLocator
    {
        public IHttpClientFactory HttpClientFactory
            => App.Current.Services.GetService<IHttpClientFactory>();

        public MainWindowViewModel MainWindowViewModel
            => App.Current.Services.GetService<MainWindowViewModel>();

        public CertSelectWindowViewModel CertSelectWindowViewModel
            => App.Current.Services.GetService<CertSelectWindowViewModel>();

        public AboutWindowViewModel AboutWindowViewModel
            => App.Current.Services.GetService<AboutWindowViewModel>();

        public InputPasswordWindowViewModel InputPasswordWindowViewModel
            => App.Current.Services.GetService<InputPasswordWindowViewModel>();
    }
}
