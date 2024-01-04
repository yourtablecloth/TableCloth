using TableCloth.Models;

namespace TableCloth.Components;

public interface ICommandLineArguments
{
    CommandLineArgumentModel Current { get; }
}