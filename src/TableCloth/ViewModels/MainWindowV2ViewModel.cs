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
            AppRestartManager appRestartManager)
        {
            _sandboxLauncher = sandboxLauncher;
            _sandboxCleanupManager = sandboxCleanupManager;
            _appRestartManager = appRestartManager;
        }

#pragma warning disable IDE0051 // Remove unused private members
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
#pragma warning restore IDE0051 // Remove unused private members
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly SandboxLauncher _sandboxLauncher;
        private readonly SandboxCleanupManager _sandboxCleanupManager;
        private readonly AppRestartManager _appRestartManager;

        public event PropertyChangedEventHandler PropertyChanged;

        public SandboxLauncher SandboxLauncher
            => _sandboxLauncher;

        public SandboxCleanupManager SandboxCleanupManager
            => _sandboxCleanupManager;

        public AppRestartManager AppRestartManager
            => _appRestartManager;
    }
}
