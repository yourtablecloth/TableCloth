using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [RelayCommand]
    private void MainWindowLoaded()
    {
        _mainWindowLoadedCommand.Execute(this);
    }

    private MainWindowLoadedCommand _mainWindowLoadedCommand = default!;

    [RelayCommand]
    private void MainWindowClosed()
    {
        _mainWindowClosedCommand.Execute(this);
    }

    private MainWindowClosedCommand _mainWindowClosedCommand = default!;
}
