using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class DisclaimerWindowViewModelForDesigner : DisclaimerWindowViewModel { }

public partial class DisclaimerWindowViewModel : ObservableObject
{
    protected DisclaimerWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public DisclaimerWindowViewModel(
        TaskFactory taskFactory)
    {
        _taskFactory = taskFactory;
    }

    private readonly TaskFactory _taskFactory = default!;

    public event EventHandler? ViewLoaded;
    public event EventHandler? DisclaimerAcknowledged;

    public async Task NotifyViewLoadedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => ViewLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task NotifyDisclaimerAcknowledgedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => DisclaimerAcknowledged?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [RelayCommand]
    private async Task DisclaimerWindowLoaded()
    {
        await NotifyViewLoadedAsync(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task DisclaimerWindowAcknowledge()
    {
        await NotifyDisclaimerAcknowledgedAsync(this, EventArgs.Empty);
    }
}
