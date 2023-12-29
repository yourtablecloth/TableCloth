using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TableCloth
{
    internal static class Helpers
    {
        public static string[] GetCommandLineArguments()
            => Environment.GetCommandLineArgs().Skip(1).ToArray();

        public static bool GetSandboxRunningState()
            => Process.GetProcesses().Where(x => x.ProcessName.StartsWith("WindowsSandbox", StringComparison.OrdinalIgnoreCase)).Any();

        public static void OpenExplorer(string targetDirectoryPath)
        {
            if (!Directory.Exists(targetDirectoryPath))
                return;

            var psi = new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                targetDirectoryPath)
            {
                UseShellExecute = false,
            };

            Process.Start(psi);
        }
    }
}
