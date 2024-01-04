using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface ICommandLineComposer
{
    string ComposeCommandLineArguments(ITableClothViewModel viewModel, bool allowMultipleItems);
    string ComposeCommandLineExpression(ITableClothViewModel viewModel, bool allowMultipleItems);
}