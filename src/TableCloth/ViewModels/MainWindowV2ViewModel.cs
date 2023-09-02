using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Components;

namespace TableCloth.ViewModels
{
    public class MainWindowV2ViewModel : INotifyPropertyChanged
    {
        public MainWindowV2ViewModel(
            SandboxLauncher sandboxLauncher,
            SandboxCleanupManager sandboxCleanupManager,
            AppRestartManager appRestartManager,
            CatalogCacheManager catalogCacheManager,
            SharedLocations sharedLocations,
            ResourceResolver resourceResolver,
            AppMessageBox appMessageBox)
        {
            _sandboxLauncher = sandboxLauncher;
            _sandboxCleanupManager = sandboxCleanupManager;
            _appRestartManager = appRestartManager;
            _catalogCacheManager = catalogCacheManager;
            _sharedLocations = sharedLocations;
            _resourceResolver = resourceResolver;
            _appMessageBox = appMessageBox;
        }

#pragma warning disable IDE0051 // Remove unused private members
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
#pragma warning restore IDE0051 // Remove unused private members
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly SandboxLauncher _sandboxLauncher;
        private readonly SandboxCleanupManager _sandboxCleanupManager;
        private readonly AppRestartManager _appRestartManager;
        private readonly CatalogCacheManager _catalogCacheManager;
        private readonly SharedLocations _sharedLocations;
        private readonly ResourceResolver _resourceResolver;
        private readonly AppMessageBox _appMessageBox;

        public event PropertyChangedEventHandler PropertyChanged;

        public SandboxLauncher SandboxLauncher
            => _sandboxLauncher;

        public SandboxCleanupManager SandboxCleanupManager
            => _sandboxCleanupManager;

        public AppRestartManager AppRestartManager
            => _appRestartManager;

        public CatalogCacheManager CatalogCacheManager
            => _catalogCacheManager;

        public SharedLocations SharedLocations
            => _sharedLocations;

        public ResourceResolver ResourceResolver
            => _resourceResolver;

        public AppMessageBox AppMessageBox
            => _appMessageBox;
    }
}
