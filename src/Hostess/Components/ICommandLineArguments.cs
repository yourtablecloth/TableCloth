using TableCloth.Models;

namespace Hostess.Components
{
    public interface ICommandLineArguments
    {
        CommandLineArgumentModel Current { get; }
    }
}