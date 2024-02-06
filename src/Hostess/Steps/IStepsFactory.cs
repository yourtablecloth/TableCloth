using Hostess.Steps;

namespace Hostess.Steps
{
    public interface IStepsFactory
    {
        IStep GetStepByName(string name);
    }

}
