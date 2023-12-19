using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Components;
using TableCloth.Contracts;

namespace TableCloth.ViewModels
{
    public class MainWindowV2ViewModel : ViewModelBase
    {
        [Obsolete("This constructor should be used only in design time context.")]
        public MainWindowV2ViewModel() { }

        public MainWindowV2ViewModel(
            NavigationService navigationService,
            SandboxCleanupManager sandboxCleanupManager,
            AppRestartManager appRestartManager,
            CommandLineParser commandLineParser,
            VisualThemeManager visualThemeManager)
        {
            _navigationService = navigationService;
            _sandboxCleanupManager = sandboxCleanupManager;
            _appRestartManager = appRestartManager;
            _commandLineParser = commandLineParser;
            _visualThemeManager = visualThemeManager;
        }

        private readonly NavigationService _navigationService;
        private readonly SandboxCleanupManager _sandboxCleanupManager;
        private readonly AppRestartManager _appRestartManager;
        private readonly CommandLineParser _commandLineParser;
        private readonly VisualThemeManager _visualThemeManager;

        public NavigationService NavigationService
            => _navigationService;

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
