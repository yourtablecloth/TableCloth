using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IAppStartup
    {
        void StartApplication(IEnumerable<string> args);
    }
}
