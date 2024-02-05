using Hostess.Steps;

namespace Hostess.Components
{
    public interface IStepFactory
    {
        IStep GetStepByName(string name);
    }

}
