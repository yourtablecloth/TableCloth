using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Spork.ViewModels
{
    [Serializable]
    public partial class OpenWebSiteItemViewModel : InstallItemViewModel
    {
        [ObservableProperty]
        private string _targetUrl;
    }
}
