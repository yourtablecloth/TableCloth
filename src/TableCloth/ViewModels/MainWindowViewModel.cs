using CommunityToolkit.Mvvm.ComponentModel;
using System;
using TableCloth.Commands.MainWindow;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class MainWindowViewModelForDesigner : MainWindowViewModel { }

public partial class MainWindowViewModel : ViewModelBase
{
    protected MainWindowViewModel() { }

    public MainWindowViewModel(
        MainWindowLoadedCommand mainWindowLoadedCommand,
        MainWindowClosedCommand mainWindowClosedCommand)
    {
        _mainWindowLoadedCommand = mainWindowLoadedCommand;
        _mainWindowClosedCommand = mainWindowClosedCommand;
    }

    [ObservableProperty]
    private MainWindowLoadedCommand _mainWindowLoadedCommand = default!;

    [ObservableProperty]
    private MainWindowClosedCommand _mainWindowClosedCommand = default!;
}
