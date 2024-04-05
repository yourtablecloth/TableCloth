using System.Threading.Tasks;
using TableCloth.Models;

namespace Spork.Components
{
    public interface ICommandLineArguments
    {
        Task<string> GetHelpStringAsync();

        Task<string> GetVersionStringAsync();

        CommandLineArgumentModel GetCurrent();
    }
}