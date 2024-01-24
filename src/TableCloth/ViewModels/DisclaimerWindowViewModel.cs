using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Commands.DisclaimerWindow;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class DisclaimerWindowViewModelForDesigner : DisclaimerWindowViewModel { }

public class DisclaimerWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected DisclaimerWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public DisclaimerWindowViewModel(
        DisclaimerWindowLoadedCommand disclaimerWindowLoadedCommand,
        DisclaimerWindowAcknowledgeCommand disclaimerWindowAcknowledgeCommand)
    {
        _disclaimerWindowLoadedCommand = disclaimerWindowLoadedCommand;
        _disclaimerWindowAcknowledgeCommand = disclaimerWindowAcknowledgeCommand;
    }

    public event EventHandler? ViewLoaded;
    public event EventHandler? DisclaimerAcknowledged;

    public async Task NotifyViewLoadedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => ViewLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task NotifyDisclaimerAcknowledgedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => DisclaimerAcknowledged?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    private readonly DisclaimerWindowLoadedCommand _disclaimerWindowLoadedCommand;
    private readonly DisclaimerWindowAcknowledgeCommand _disclaimerWindowAcknowledgeCommand;

    public DisclaimerWindowLoadedCommand DisclaimerWindowLoadedCommand
        => _disclaimerWindowLoadedCommand;

    public DisclaimerWindowAcknowledgeCommand DisclaimerWindowAcknowledgeCommand
        => _disclaimerWindowAcknowledgeCommand;
}
