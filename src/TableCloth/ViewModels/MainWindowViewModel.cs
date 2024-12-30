using System;
using TableCloth.Commands.MainWindow;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class MainWindowViewModelForDesigner : MainWindowViewModel { }

public class MainWindowViewModel : ViewModelBase
{
    protected MainWindowViewModel() { }

    public MainWindowViewModel(
        MainWindowLoadedCommand mainWindowLoadedCommand,
        MainWindowClosedCommand mainWindowClosedCommand)
    {
        _mainWindowLoadedCommand = mainWindowLoadedCommand;
        _mainWindowClosedCommand = mainWindowClosedCommand;
    }

    private readonly MainWindowLoadedCommand _mainWindowLoadedCommand = default!;
    private readonly MainWindowClosedCommand _mainWindowClosedCommand = default!;

    public MainWindowLoadedCommand MainWindowLoadedCommand
        => _mainWindowLoadedCommand;

    public MainWindowClosedCommand MainWindowClosedCommand
        => _mainWindowClosedCommand;
}
