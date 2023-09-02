using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public sealed class ViewModelLocator
    {
        public IHttpClientFactory HttpClientFactory
            => App.Current.Services.GetService<IHttpClientFactory>();

        public MainWindowV2ViewModel MainWindowV2ViewModel
            => App.Current.Services.GetService<MainWindowV2ViewModel>();

        public CertSelectWindowViewModel CertSelectWindowViewModel
            => App.Current.Services.GetService<CertSelectWindowViewModel>();

        public AboutWindowViewModel AboutWindowViewModel
            => App.Current.Services.GetService<AboutWindowViewModel>();

        public InputPasswordWindowViewModel InputPasswordWindowViewModel
            => App.Current.Services.GetService<InputPasswordWindowViewModel>();

        public MainWindowViewModel MainWindowViewModel
            => App.Current.Services.GetService<MainWindowViewModel>();

        public DetailPageViewModel DetailPageViewModel
            => App.Current.Services.GetService<DetailPageViewModel>();
    }
}
