using System;
using TableCloth.Commands.DisclaimerWindow;

namespace TableCloth.ViewModels;

public sealed class DisclaimerWindowViewModel : ViewModelBase
{
    [Obsolete("This constructor should be used only in design time context.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public DisclaimerWindowViewModel() { }
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

    public void NotifyViewLoaded(object? sender, EventArgs e)
        => ViewLoaded?.Invoke(sender, e);

    public void NotifyDisclaimerAcknowledged(object? sender, EventArgs e)
        => DisclaimerAcknowledged?.Invoke(sender, e);

    private readonly DisclaimerWindowLoadedCommand _disclaimerWindowLoadedCommand;
    private readonly DisclaimerWindowAcknowledgeCommand _disclaimerWindowAcknowledgeCommand;

    public DisclaimerWindowLoadedCommand DisclaimerWindowLoadedCommand
        => _disclaimerWindowLoadedCommand;

    public DisclaimerWindowAcknowledgeCommand DisclaimerWindowAcknowledgeCommand
        => _disclaimerWindowAcknowledgeCommand;
}
