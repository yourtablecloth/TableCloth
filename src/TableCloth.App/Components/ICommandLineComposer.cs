using System.Collections.Generic;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface ICommandLineComposer
{
    string ComposeCommandLineArguments(DetailPageViewModel viewModel, bool allowMultipleItems);
    string ComposeCommandLineExpression(DetailPageViewModel viewModel, bool allowMultipleItems);
    IReadOnlyList<string> GetCommandLineExpressionList(DetailPageViewModel viewModel, bool allowMultipleItems);
}