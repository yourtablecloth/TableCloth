using System;
using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands
{
    public sealed class CopyCommandLineCommand : CommandBase
    {
        public CopyCommandLineCommand(
            CommandLineComposer commandLineComposer,
            AppMessageBox appMessageBox)
        {
            this.commandLineComposer = commandLineComposer;
            this.appMessageBox = appMessageBox;
        }

        private readonly CommandLineComposer commandLineComposer;
        private readonly AppMessageBox appMessageBox;

        public override void Execute(object? parameter)
        {
            if (parameter is not DetailPageViewModel viewModel)
                throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

            var expression = this.commandLineComposer.ComposeCommandLineExpression(viewModel);
            Clipboard.SetText(expression);

            this.appMessageBox.DisplayInfo(StringResources.Info_CopyCommandLineSuccess, MessageBoxButton.OK);
        }
    }
}
