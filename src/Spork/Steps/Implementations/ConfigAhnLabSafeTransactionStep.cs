using Spork.Components;
using Spork.ViewModels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class ConfigAhnLabSafeTransactionStep : StepBase<InstallItemViewModel>
    {
        public ConfigAhnLabSafeTransactionStep(
            IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel _, CancellationToken cancellationToken = default)
        {
            var stSessPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AhnLab", "Safe Transaction", "StSess.exe");

            var hasStSess = File.Exists(stSessPath);
            return Task.FromResult(hasStSess);
        }

        public override Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            var stSessPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AhnLab", "Safe Transaction", "StSess.exe");

            var comSpecPath = Helpers.GetDefaultCommandLineInterpreterPath();

            if (!File.Exists(comSpecPath))
                TableClothAppException.Throw(ErrorStrings.Error_CommandLineInterpreter_Missing);

            // To Do: 상세한 설명을 담은 UI를 제작할 필요가 있음.
            _appMessageBox.DisplayInfo(UIStringResources.Instruction_ConfigASTx);

            using (var process = Helpers.CreateRunProcess(comSpecPath, stSessPath, "/config"))
            {
                if (!process.Start())
                    TableClothAppException.Throw(ErrorStrings.Error_StSessConfig_CanNotStart);
                else
                    _appMessageBox.DisplayInfo(UIStringResources.Await_ConfigASTx);
            }

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
