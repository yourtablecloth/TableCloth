using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Commands.DisclaimerWindow;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class DisclaimerWindowViewModelForDesigner : DisclaimerWindowViewModel { }

public partial class DisclaimerWindowViewModel : ViewModelBase
{
    protected DisclaimerWindowViewModel() { }

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

    [RelayCommand]
    private void DisclaimerWindowLoaded()
    {
        _disclaimerWindowLoadedCommand.Execute(this);
    }

    private DisclaimerWindowLoadedCommand _disclaimerWindowLoadedCommand = default!;

    [RelayCommand]
    private void DisclaimerWindowAcknowledge()
    {
        _disclaimerWindowAcknowledgeCommand.Execute(this);
    }

    private DisclaimerWindowAcknowledgeCommand _disclaimerWindowAcknowledgeCommand = default!;
}
