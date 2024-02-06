using Microsoft.Win32;
using System;
using System.IO;
using TableCloth;

namespace Hostess.Components.Implementations
{
    public sealed class SharedLocations : ISharedLocations
    {
        public string GetDownloadDirectoryPath() =>
            NativeMethods.GetKnownFolderPath(NativeMethods.DownloadFolderGuid);

        public string GetPicturesDirectoryPath() =>
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        public string GetDefaultPowerShellExecutableFilePath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
    }
}
