using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class OptionsWindowViewModelForDesigner : OptionsWindowViewModel { }

public partial class OptionsWindowViewModel : ObservableObject
{
    protected OptionsWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public OptionsWindowViewModel(
        IPreferencesManager preferencesManager,
        IAppRestartManager appRestartManager,
        TaskFactory taskFactory)
    {
        _preferencesManager = preferencesManager;
        _appRestartManager = appRestartManager;
        _taskFactory = taskFactory;
    }

    public event EventHandler? CloseRequested;

    public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [RelayCommand]
    private async Task OptionsWindowLoaded()
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        _suppressSave = true;
        try
        {
            EnableMicrophone = currentConfig.UseAudioRedirection;
            EnableWebCam = currentConfig.UseVideoRedirection;
            EnablePrinters = currentConfig.UsePrinterRedirection;
            InstallEveryonesPrinter = currentConfig.InstallEveryonesPrinter;
            InstallAdobeReader = currentConfig.InstallAdobeReader;
            InstallHancomOfficeViewer = currentConfig.InstallHancomOfficeViewer;
            InstallRaiDrive = currentConfig.InstallRaiDrive;
            EnableLogAutoCollecting = currentConfig.UseLogCollection;
        }
        finally
        {
            _suppressSave = false;
        }

        PropertyChanged += ViewModel_PropertyChanged;
    }

    [RelayCommand]
    private async Task CloseDialog()
    {
        await RequestCloseAsync(this, EventArgs.Empty);
    }

    [ObservableProperty]
    private bool _enableMicrophone;

    [ObservableProperty]
    private bool _enableWebCam;

    [ObservableProperty]
    private bool _enablePrinters;

    [ObservableProperty]
    private bool _installEveryonesPrinter;

    [ObservableProperty]
    private bool _installAdobeReader;

    [ObservableProperty]
    private bool _installHancomOfficeViewer;

    [ObservableProperty]
    private bool _installRaiDrive;

    [ObservableProperty]
    private bool _enableLogAutoCollecting;

    private bool _suppressSave;
    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
    private readonly TaskFactory _taskFactory = default!;

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSave)
            return;

        var viewModel = sender as OptionsWindowViewModel;
        ArgumentNullException.ThrowIfNull(viewModel);

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        var reserveRestart = false;

        switch (e.PropertyName)
        {
            case nameof(EnableMicrophone):
                currentConfig.UseAudioRedirection = viewModel.EnableMicrophone;
                break;

            case nameof(EnableWebCam):
                currentConfig.UseVideoRedirection = viewModel.EnableWebCam;
                break;

            case nameof(EnablePrinters):
                currentConfig.UsePrinterRedirection = viewModel.EnablePrinters;
                break;

            case nameof(InstallEveryonesPrinter):
                currentConfig.InstallEveryonesPrinter = viewModel.InstallEveryonesPrinter;
                break;

            case nameof(InstallAdobeReader):
                currentConfig.InstallAdobeReader = viewModel.InstallAdobeReader;
                break;

            case nameof(InstallHancomOfficeViewer):
                currentConfig.InstallHancomOfficeViewer = viewModel.InstallHancomOfficeViewer;
                break;

            case nameof(InstallRaiDrive):
                currentConfig.InstallRaiDrive = viewModel.InstallRaiDrive;
                break;

            case nameof(EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                reserveRestart = _appRestartManager.AskRestart();
                break;

            default:
                return;
        }

        await _preferencesManager.SavePreferencesAsync(currentConfig);

        if (reserveRestart)
        {
            _appRestartManager.ReserveRestart();
            await viewModel.RequestCloseAsync(viewModel, e);
        }
    }
}
