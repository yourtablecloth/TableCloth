using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;
using System.Windows.Controls;

namespace TableCloth.Components
{
    public sealed class AppUserInterface
    {
        public AppUserInterface(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider serviceProvider;

        public TWindow CreateWindow<TWindow>(Action<TWindow> modifier = default)
            where TWindow : Window
        {
            var windowInstance = (TWindow)CreateWindow(typeof(TWindow), default);
            modifier?.Invoke(windowInstance);
            return windowInstance;
        }

        public Window CreateWindow(Type windowType, Action<Window> modifier = default)
        {
            var windowInstance = (Window)this.serviceProvider.GetRequiredService(windowType);
            modifier?.Invoke(windowInstance);
            return windowInstance;
        }

        public TPage CreatePage<TPage>(Action<TPage> modifier = default)
            where TPage : Page
        {
            var pageInstance = (TPage)CreatePage(typeof(TPage), default);
            modifier?.Invoke(pageInstance);
            return pageInstance;
        }

        public Page CreatePage(Type pageType, Action<Page> modifier = default)
        {
            var pageInstance = (Page)this.serviceProvider.GetRequiredService(pageType);
            modifier?.Invoke(pageInstance);
            return pageInstance;
        }
    }
}
