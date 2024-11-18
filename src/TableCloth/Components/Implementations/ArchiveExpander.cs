using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.Components.Implementations;

public sealed class ArchiveExpander : IArchiveExpander
{
    public async Task ExpandArchiveAsync(string zipFilePath, string destinationDirectoryPath, CancellationToken cancellationToken = default)
    {
        using var zipStream = File.OpenRead(zipFilePath);
        using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var eachEntry in zipArchive.Entries)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eachEntry.Name))
                    continue;

                var destPath = Path.Combine(destinationDirectoryPath, eachEntry.Name);

                using var outputStream = File.OpenWrite(destPath);
                using var eachStream = eachEntry.Open();
                await eachStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new IOException($"Cannot extract the file '{eachEntry.Name}' to '{destinationDirectoryPath}'.", ex);
            }
        }
    }
}
