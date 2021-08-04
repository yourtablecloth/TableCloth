using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IAppUserInterface
    {
        void StartApplication(IEnumerable<string> args);
    }
}
