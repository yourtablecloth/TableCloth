using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;
using TableCloth.Contracts;

namespace TableCloth.Components
{
    public sealed class AppUserInterface
    {
        public AppUserInterface(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider serviceProvider;

        public TWindow CreateWindow<TWindow>(Action<TWindow>? modifier = default)
            where TWindow : Window
        {
            var windowInstance = (TWindow)CreateWindow(typeof(TWindow), default);
            modifier?.Invoke(windowInstance);
            return windowInstance;
        }

        public Window CreateWindow(Type windowType, Action<Window>? modifier = default)
        {
            var windowInstance = (Window)this.serviceProvider.GetRequiredService(windowType);
            modifier?.Invoke(windowInstance);
            return windowInstance;
        }

        public TPage CreatePage<TPage, TPageViewModel>(Action<TPage>? modifier = default)
            where TPage : Page
            where TPageViewModel : class, IPageExtraArgument
        {
            var pageInstance = (TPage)CreatePage(typeof(TPage), default);
            pageInstance.DataContext = this.serviceProvider.GetService<TPageViewModel>();
            modifier?.Invoke(pageInstance);
            return pageInstance;
        }

        public Page CreatePage(Type pageType, Action<Page>? modifier = default)
        {
            var pageInstance = (Page)this.serviceProvider.GetRequiredService(pageType);
            modifier?.Invoke(pageInstance);
            return pageInstance;
        }

        public TViewModel CreateViewModel<TViewModel>(object? extraArgument)
            where TViewModel : class, IPageExtraArgument
        {
            var viewModel = this.serviceProvider.GetRequiredService<TViewModel>();

            if (viewModel is IPageExtraArgument extraArg)
                extraArg.ExtraArgument = extraArgument;

            return viewModel;
        }
    }
}
