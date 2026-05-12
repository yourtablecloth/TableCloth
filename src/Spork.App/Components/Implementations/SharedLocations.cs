using System;
using System.Diagnostics;
using System.IO;
using TableCloth;

namespace Spork.Components.Implementations
{
    public sealed class SharedLocations : ISharedLocations
    {
        public string ExecutableFilePath
        {
            get
            {
                var mainModule = Process.GetCurrentProcess().MainModule
                    .EnsureNotNull("Cannot obtain process main module information.");
                return mainModule.FileName
                    .EnsureNotNull("Cannot obtain executable file name.");
            }
        }

        public string ExecutableDirectoryPath
            => Path.GetDirectoryName(ExecutableFilePath).EnsureNotNull("Cannot obtain executable directory path.");

        public string GetDownloadDirectoryPath() =>
            NativeMethods.GetKnownFolderPath(NativeMethods.DownloadFolderGuid);

        public string GetPicturesDirectoryPath() =>
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        public string GetDefaultPowerShellExecutableFilePath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
    }
}
