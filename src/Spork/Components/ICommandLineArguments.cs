using TableCloth.Models;

namespace Spork.Components
{
    public interface ICommandLineArguments
    {
        CommandLineArgumentModel GetCurrent();
    }
}