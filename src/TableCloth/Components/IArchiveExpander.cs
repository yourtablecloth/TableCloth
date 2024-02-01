using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.Components;

public interface IArchiveExpander
{
    Task ExpandArchiveAsync(string zipFilePath, string destinationDirectoryPath, CancellationToken cancellationToken = default);
}
