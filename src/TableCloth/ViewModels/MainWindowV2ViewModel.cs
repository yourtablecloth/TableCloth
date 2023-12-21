using System;
using TableCloth.Commands.MainWindowV2;

namespace TableCloth.ViewModels;

public class MainWindowV2ViewModel : ViewModelBase
{
    [Obsolete("This constructor should be used only in design time context.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public MainWindowV2ViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public MainWindowV2ViewModel(
        MainWindowV2LoadedCommand mainWindowV2LoadedCommand,
        MainWindowV2ClosedCommand mainWindowV2ClosedCommand)
    {
        _mainWindowV2LoadedCommand = mainWindowV2LoadedCommand;
        _mainWindowV2ClosedCommand = mainWindowV2ClosedCommand;
    }

    private readonly MainWindowV2LoadedCommand _mainWindowV2LoadedCommand;
    private readonly MainWindowV2ClosedCommand _mainWindowV2ClosedCommand;

    public MainWindowV2LoadedCommand MainWindowV2LoadedCommand
        => _mainWindowV2LoadedCommand;

    public MainWindowV2ClosedCommand MainWindowV2ClosedCommand
        => _mainWindowV2ClosedCommand;
}
