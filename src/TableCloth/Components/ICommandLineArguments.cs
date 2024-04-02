using TableCloth.Models;

namespace TableCloth.Components;

public interface ICommandLineArguments
{
    CommandLineArgumentModel GetCurrent();
}