using Hostess.ViewModels;
using System.Collections.Generic;

namespace Hostess.Components
{
    public interface IStepsPlayer
    {
        bool PlaySteps(IEnumerable<InstallItemViewModel> composedSteps);
    }
}