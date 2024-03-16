using Spork.ViewModels;
using System.Collections.Generic;

namespace Spork.Steps
{
    public interface IStepsComposer
    {
        IEnumerable<StepItemViewModel> ComposeSteps();
    }
}