using System;
using System.Windows.Controls;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public sealed class NavigationService
    {
        private readonly AppUserInterface _appUserInterface;

        private Frame? _frame = default;

        public NavigationService(
            AppUserInterface appUserInterface)
        {
            _appUserInterface = appUserInterface;
        }

        public void Initialize(Frame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        public bool NavigateTo<TViewModel>(object? extraData)
            where TViewModel : class
        {
            if (_frame == default)
                throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

            var requestedViewModelType = typeof(TViewModel);
            var viewType = default(Type);
            var viewModelData = default(object);

            if (requestedViewModelType.Equals(typeof(CatalogPageViewModel)))
            {
                viewType = typeof(CatalogPage);
                viewModelData = _appUserInterface.CreateViewModel<CatalogPageViewModel>(extraData);
            }

            if (requestedViewModelType.Equals(typeof(DetailPageViewModel)))
            {
                viewType = typeof(DetailPage);
                viewModelData = _appUserInterface.CreateViewModel<DetailPageViewModel>(extraData);
            }

            if (viewType == default)
                return false;

            var view = _appUserInterface.CreatePage(viewType);
            view.DataContext = viewModelData;
            return _frame.Navigate(view);
        }

        public bool GoBack()
        {
            if (_frame == default)
                throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

            if (!_frame.CanGoBack)
                return false;

            _frame.GoBack();
            return true;
        }

        public bool GoForward()
        {
            if (_frame == default)
                throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

            if (!_frame.CanGoForward)
                return false;

            _frame.GoForward();
            return true;
        }

        public void Refresh()
        {
            if (_frame == default)
                throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

            _frame.Refresh();
        }
    }
}
