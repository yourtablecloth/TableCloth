using Spork.Steps;

namespace Spork.Steps
{
    public interface IStepsFactory
    {
        IStep GetStepByName(string name);
    }

}
