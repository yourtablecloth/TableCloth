using System.Collections.Generic;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface ICommandLineComposer
{
    string ComposeCommandLineArguments(ITableClothViewModel viewModel, bool allowMultipleItems);
    string ComposeCommandLineExpression(ITableClothViewModel viewModel, bool allowMultipleItems);
    IReadOnlyList<string> GetCommandLineExpressionList(ITableClothViewModel viewModel, bool allowMultipleItems);
}