using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class DisclaimerWindowViewModelForDesigner : DisclaimerWindowViewModel { }

public partial class DisclaimerWindowViewModel : ViewModelBase
{
    public event EventHandler? ViewLoaded;
    public event EventHandler? DisclaimerAcknowledged;

    public async Task NotifyViewLoadedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => ViewLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task NotifyDisclaimerAcknowledgedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => DisclaimerAcknowledged?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [RelayCommand]
    private async Task DisclaimerWindowLoaded()
    {
        await NotifyViewLoadedAsync(this, EventArgs.Empty);
    }
}
