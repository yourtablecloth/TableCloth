using Microsoft.Win32;
using System;
using System.IO;

namespace Spork.Browsers.Implementations
{
    public sealed class X86ChromiumEdgeWebBrowserService : IWebBrowserService
    {
        public bool TryGetBrowserExecutablePath(out string executableFilePath)
        {
            executableFilePath = null;
            var msedgeKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", false);

            if (msedgeKey != null)
            {
                using (msedgeKey)
                {
                    executableFilePath = (string)msedgeKey.GetValue(null, null);
                }
            }

            if (string.IsNullOrWhiteSpace(executableFilePath) || !File.Exists(executableFilePath))
            {
                executableFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft", "Edge", "Application", "msedge.exe");
            }

            return File.Exists(executableFilePath);
        }
    }
}
