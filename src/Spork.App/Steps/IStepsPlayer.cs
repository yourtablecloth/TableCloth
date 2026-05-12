using Spork.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps
{
    public interface IStepsPlayer
    {
        bool IsRunning { get; }

        Task<bool> PlayStepsAsync(
            IEnumerable<StepItemViewModel> composedSteps,
            bool dryRun,
            CancellationToken cancellationToken = default);
    }
}