using Hostess.ViewModels;
using System.Collections.Generic;

namespace Hostess.Components
{
    public interface IStepsComposer
    {
        IEnumerable<StepItemViewModel> ComposeSteps();
    }
}