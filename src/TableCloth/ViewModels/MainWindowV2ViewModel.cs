using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Components;

namespace TableCloth.ViewModels
{
    public class MainWindowV2ViewModel : INotifyPropertyChanged
    {
        public MainWindowV2ViewModel(
            SandboxCleanupManager sandboxCleanupManager,
            AppRestartManager appRestartManager,
            CommandLineParser commandLineParser,
            VisualThemeManager visualThemeManager)
        {
            _sandboxCleanupManager = sandboxCleanupManager;
            _appRestartManager = appRestartManager;
            _commandLineParser = commandLineParser;
            _visualThemeManager = visualThemeManager;
        }

#pragma warning disable IDE0051 // Remove unused private members
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
#pragma warning restore IDE0051 // Remove unused private members
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly SandboxCleanupManager _sandboxCleanupManager;
        private readonly AppRestartManager _appRestartManager;
        private readonly CommandLineParser _commandLineParser;
        private readonly VisualThemeManager _visualThemeManager;

        public event PropertyChangedEventHandler PropertyChanged;

        public SandboxCleanupManager SandboxCleanupManager
            => _sandboxCleanupManager;

        public AppRestartManager AppRestartManager
            => _appRestartManager;

        public CommandLineParser CommandLineParser
            => _commandLineParser;

        public VisualThemeManager VisualThemeManager
            => _visualThemeManager;
    }
}
