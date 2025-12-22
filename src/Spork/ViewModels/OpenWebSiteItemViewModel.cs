using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Spork.ViewModels
{
    public partial class OpenWebSiteItemViewModel : InstallItemViewModel
    {
        [ObservableProperty]
        private string _targetUrl;
    }
}
