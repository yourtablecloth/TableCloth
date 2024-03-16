using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Spork.Components;
using Spork.Steps;
using Spork.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;

namespace Spork.Commands.MainWindow
{
    public sealed class MainWindowLoadedCommand : ViewModelCommandBase<MainWindowViewModel>, IAsyncCommand<MainWindowViewModel>
    {
        public MainWindowLoadedCommand(
            Application application,
            IResourceCacheManager resourceCacheManager,
            IAppUserInterface appUserInterface,
            IVisualThemeManager visualThemeManager,
            ICommandLineArguments commandLineArguments,
            IStepsComposer stepsComposer)
        {
            _application = application;
            _resourceCacheManager = resourceCacheManager;
            _appUserInterface = appUserInterface;
            _visualThemeManager = visualThemeManager;
            _commandLineArguments = commandLineArguments;
            _stepsComposer = stepsComposer;
        }

        private readonly Application _application;
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IAppUserInterface _appUserInterface;
        private readonly IVisualThemeManager _visualThemeManager;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IStepsComposer _stepsComposer;

        public override void Execute(MainWindowViewModel viewModel)
            => ExecuteAsync(viewModel).SafeFireAndForget();

        public async Task ExecuteAsync(MainWindowViewModel viewModel)
        {
            _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow);

            var parsedArgs = _commandLineArguments.Current;
            var catalog = _resourceCacheManager.CatalogDocument;
            var targets = parsedArgs.SelectedServices;

            viewModel.ShowDryRunNotification = parsedArgs.DryRun;

            await viewModel.NotifyWindowLoadedAsync(this, EventArgs.Empty);

            var steps = _stepsComposer.ComposeSteps();
            viewModel.InstallSteps = new ObservableCollection<StepItemViewModel>(steps);

            if (catalog.HasAnyCompatNotes(targets))
            {
                var window = _appUserInterface.CreatePrecautionsWindow();
                window.ShowDialog();
            }

            viewModel.MainWindowInstallPackagesCommand.Execute(viewModel);
        }
    }
}
