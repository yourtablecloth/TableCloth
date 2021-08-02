using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IAppStartup
    {
        void InitializeEnvironment(IEnumerable<string> args);

        void StartApplication(IEnumerable<string> args);
    }
}
