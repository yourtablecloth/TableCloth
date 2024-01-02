using Microsoft.Win32;
using System;
using System.IO;
using TableCloth;

namespace Hostess.Components
{
    public sealed class SharedLocations
    {
        public string GetDownloadDirectoryPath() =>
            NativeMethods.GetKnownFolderPath(NativeMethods.DownloadFolderGuid);

        public string GetPicturesDirectoryPath() =>
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        public bool TryGetMicrosoftEdgeExecutableFilePath(out string msedgePath)
        {
            // msedge.exe 파일 경로를 유추하고, Policy를 반영하기 위해 잠시 실행했다가 종료하는 동작을 추가
            msedgePath = null;
            var msedgeKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", false);

            if (msedgeKey != null)
            {
                using (msedgeKey)
                {
                    msedgePath = (string)msedgeKey.GetValue(null, null);
                }
            }

            return !string.IsNullOrWhiteSpace(msedgePath) && File.Exists(msedgePath);
        }

        public string GetDefaultX86MicrosoftEdgeExecutableFilePath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft", "Edge", "Application", "msedge.exe");

        public string GetDefaultPowerShellExecutableFilePath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
    }
}
