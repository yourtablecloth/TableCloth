using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Spork.ViewModels
{
    public partial class EdgeExtensionInstallItemViewModel : InstallItemViewModel
    {
        [ObservableProperty]
        private string _edgeCrxUrl;

        [ObservableProperty]
        private string _edgeExtensionId;
    }
}
