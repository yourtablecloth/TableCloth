using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace TableCloth.ResourceBuilder
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var ignoreWarning = string.Equals(
                Environment.GetEnvironmentVariable("IGNORE_WARNING"),
                "1", StringComparison.OrdinalIgnoreCase);

            // Create temporary directory
            var tempDirectoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
            if (Directory.Exists(tempDirectoryPath))
                Directory.Delete(tempDirectoryPath, true);
            if (File.Exists(tempDirectoryPath))
                File.Delete(tempDirectoryPath);
            if (!Directory.Exists(tempDirectoryPath))
                Directory.CreateDirectory(tempDirectoryPath);

            // Result variables
            var allCopiedFiles = new List<string>();
            var allGeneratedIconFiles = new List<string>();

            try
            {
                // args[0]: Input Directory Path
                var inputDirectoryPath = args.ElementAtOrDefault(0);
                if (string.IsNullOrWhiteSpace(inputDirectoryPath) ||
                    !Directory.Exists(inputDirectoryPath))
                {
                    Environment.ExitCode = 1;
                    Console.Error.WriteLine("First argument should be an existing directory path.");
                    return;
                }

                // args[1]: Output ZIP File Path
                var outputZipFilePath = args.ElementAtOrDefault(1);
                if (string.IsNullOrWhiteSpace(outputZipFilePath) ||
                    Directory.Exists(outputZipFilePath))
                {
                    Environment.ExitCode = 1;
                    Console.Error.WriteLine("Second argument should be an non-existing ZIP file output path.");
                    return;
                }

                if (File.Exists(outputZipFilePath))
                {
                    if (!ignoreWarning)
                        Console.Out.WriteLine($"Warning: {outputZipFilePath} file will be overwritten.");
                }

                // Collect all png files (including sub directoriess) into temporary directory
                var allPngFiles = Directory.GetFiles(inputDirectoryPath, "*.png", SearchOption.AllDirectories);
                foreach (var eachPngFilePath in allPngFiles)
                {
                    var pngFileName = Path.GetFileName(eachPngFilePath);
                    var destPngFilePath = Path.Combine(tempDirectoryPath, pngFileName);

                    if (File.Exists(destPngFilePath))
                    {
                        if (!ignoreWarning)
                            Console.Out.WriteLine($"Warning: {pngFileName} has duplicated. Skipping.");
                        continue;
                    }

                    Console.Out.WriteLine($"Source - {eachPngFilePath} => Destination - {destPngFilePath}");
                    File.Copy(eachPngFilePath, destPngFilePath);
                    allCopiedFiles.Add(destPngFilePath);
                }

                // Convert all png files into ico files into same directory
                foreach (var eachCopiedFilePath in allCopiedFiles)
                {
                    var basePath = Path.GetDirectoryName(eachCopiedFilePath);
                    var pngFileNameWithoutExt = Path.GetFileNameWithoutExtension(eachCopiedFilePath);
                    var destIcoFilePath = Path.Combine(basePath, $"{pngFileNameWithoutExt}.ico");

                    Console.Out.WriteLine($"Coverting image file {eachCopiedFilePath} into icon file {destIcoFilePath}");
                    File.WriteAllBytes(
                        destIcoFilePath,
                        ConvertImageToIcon(eachCopiedFilePath));
                    allGeneratedIconFiles.Add(destIcoFilePath);
                }

                // Create a ZIP file and delete temporary directory
                var targetFiles = Enumerable.Concat(allCopiedFiles, allGeneratedIconFiles);
                using (var outputFileStream = File.OpenWrite(outputZipFilePath))
                {
                    Console.Out.WriteLine($"Creating {outputZipFilePath} zip file.");
                    using (var archive = new ZipArchive(outputFileStream, ZipArchiveMode.Create))
                    {
                        foreach (var eachTargetFile in targetFiles)
                        {
                            var entryName = Path.GetFileName(eachTargetFile);
                            var entry = archive.CreateEntry(entryName);

                            Console.Out.WriteLine($"Compressing {entryName} file.");

                            using (var entryStream = entry.Open())
                            using (var sourceStream = File.OpenRead(eachTargetFile))
                            {
                                sourceStream.CopyTo(entryStream);
                            }
                        }
                    }
                }

                if (File.Exists(outputZipFilePath))
                    Console.Out.WriteLine($"{outputZipFilePath} zip file created successfully.");
                else
                    throw new Exception($"{outputZipFilePath} zip file does not created due to error.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error occurred: ");
                Console.Error.WriteLine(ex.ToString());
                Environment.ExitCode = 2;
            }
            finally
            {
                // Try Cleanup
                try
                {
                    var allTempFiles = Enumerable.Concat(allCopiedFiles, allGeneratedIconFiles);
                    foreach (var eachTempFile in allTempFiles)
                        if (File.Exists(eachTempFile))
                            File.Delete(eachTempFile);

                    if (Directory.Exists(tempDirectoryPath))
                        Directory.Delete(tempDirectoryPath, true);
                }
                catch (Exception subEx)
                {
                    Console.Error.WriteLine("Cannot post cleanup due to error: ");
                    Console.Error.WriteLine(subEx.ToString());
                    Console.Error.WriteLine("Review this error log and do manual cleanup.");
                }
            }
        }

        // https://stackoverflow.com/questions/21387391/how-to-convert-an-image-to-an-icon-without-losing-transparency
        private static byte[] ConvertImageToIcon(string imageFilePath)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            using (var fs = File.OpenRead(imageFilePath))
            using (var img = Image.FromStream(fs))
            {
                // Header
                bw.Write((short)0);   // 0 : reserved
                bw.Write((short)1);   // 2 : 1=ico, 2=cur
                bw.Write((short)1);   // 4 : number of images

                // Image directory
                var w = img.Width;
                if (w >= 256) w = 0;
                bw.Write((byte)w);    // 0 : width of image

                var h = img.Height;
                if (h >= 256) h = 0;
                bw.Write((byte)h);    // 1 : height of image

                bw.Write((byte)0);    // 2 : number of colors in palette
                bw.Write((byte)0);    // 3 : reserved
                bw.Write((short)0);   // 4 : number of color planes
                bw.Write((short)0);   // 6 : bits per pixel

                var sizeHere = ms.Position;
                bw.Write(0);     // 8 : image size

                var start = (int)ms.Position + 4;
                bw.Write(start);      // 12: offset of image data

                // Image data
                img.Save(ms, ImageFormat.Png);
                var imageSize = (int)ms.Position - start;
                ms.Seek(sizeHere, SeekOrigin.Begin);
                bw.Write(imageSize);
                ms.Seek(0L, SeekOrigin.Begin);

                return ms.ToArray();
            }
        }
    }
}
