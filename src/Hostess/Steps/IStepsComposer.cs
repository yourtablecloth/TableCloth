using Hostess.ViewModels;
using System.Collections.Generic;

namespace Hostess.Steps
{
    public interface IStepsComposer
    {
        IEnumerable<StepItemViewModel> ComposeSteps();
    }
}