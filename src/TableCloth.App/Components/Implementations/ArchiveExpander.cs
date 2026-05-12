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

        var fullDestDirPath = Path.GetFullPath(destinationDirectoryPath + Path.DirectorySeparatorChar);

        foreach (var eachEntry in zipArchive.Entries)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(eachEntry.Name))
                    continue;

                // Use FullName to preserve directory structure, but ensure the result stays within destinationDirectoryPath
                var destPath = Path.GetFullPath(Path.Combine(destinationDirectoryPath, eachEntry.FullName));

                if (!destPath.StartsWith(fullDestDirPath, StringComparison.Ordinal))
                    throw new IOException($"Entry is outside the target directory: '{eachEntry.FullName}'.");

                var destDirectory = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrWhiteSpace(destDirectory) && !Directory.Exists(destDirectory))
                    Directory.CreateDirectory(destDirectory);

                using var outputStream = File.OpenWrite(destPath);
                using var eachStream = eachEntry.Open();
                await eachStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new IOException($"Cannot extract the file '{eachEntry.FullName}' to '{destinationDirectoryPath}'.", ex);
            }
        }
    }
}
