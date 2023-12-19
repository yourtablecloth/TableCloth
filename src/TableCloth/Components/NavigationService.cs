using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using TableCloth.Contracts;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public sealed class NavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private Frame _frame;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public bool NavigateTo<TViewModel>(object extraData)
            where TViewModel : class
        {
            var requestedViewModelType = typeof(TViewModel);
            var viewType = default(Type);

            if (requestedViewModelType.Equals(typeof(CatalogPageViewModel)))
                viewType = typeof(CatalogPage);

            if (requestedViewModelType.Equals(typeof(DetailPageViewModel)))
                viewType = typeof(DetailPage);

            if (viewType == default)
                return false;

            var view = _serviceProvider.GetService(viewType) as Page;
            return _frame.Navigate(view, extraData);
        }

        public bool GoBack()
        {
            if (!_frame.CanGoBack)
                return false;

            _frame.GoBack();
            return true;
        }

        public bool GoForward()
        {
            if (!_frame.CanGoForward)
                return false;

            _frame.GoForward();
            return true;
        }

        public void Refresh()
        {
            _frame.Refresh();
        }
    }
}
