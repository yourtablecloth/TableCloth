using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IAppUserInterface
    {
        object MainWindowHandle { get; }

        void StartApplication(IEnumerable<string> args);
    }
}
