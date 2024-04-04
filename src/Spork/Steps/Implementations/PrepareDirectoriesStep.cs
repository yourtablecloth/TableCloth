using Spork.Components;
using Spork.ViewModels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    public sealed class PrepareDirectoriesStep : StepBase<InstallItemViewModel>
    {
        public PrepareDirectoriesStep(
            ISharedLocations sharedLocations)
        {
            _sharedLocations = sharedLocations;
        }

        private readonly ISharedLocations _sharedLocations;

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();

            if (!Directory.Exists(downloadFolderPath))
                Directory.CreateDirectory(downloadFolderPath);

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => false;
    }
}
