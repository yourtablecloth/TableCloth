using Hostess.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hostess.Components
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