using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TableCloth
{
    public static class Helpers
    {
        static Helpers()
        {
            IsAppxInstallation = NativeMethods.TryGetApplicationPackagedAsMSIX(out AppxPackageName);
        }

        public static readonly string
#if !NETFX
            ?
#endif
            AppxPackageName;

        public static readonly bool IsAppxInstallation;

        public static bool IsDevelopmentBuild =>
#if DEBUG
            true
#else
            false
#endif
            ;

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

        public static string GetAppVersion()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var versionInfo = executingAssembly.GetName().Version?.ToString() ?? "Unknown";

            try
            {
                var resourceNames = executingAssembly.GetManifestResourceNames();
                var commitTextFileName = resourceNames.Where(x => x.EndsWith("commit.txt", StringComparison.Ordinal)).FirstOrDefault();

                if (commitTextFileName != null)
                {
                    var resourceStream = executingAssembly.GetManifestResourceStream(commitTextFileName);
                    var commitId = string.Empty;

                    if (resourceStream == null)
                        commitId = "Unknown Commit ID";
                    else
                    {
                        using (resourceStream)
                        {
                            var streamReader = new StreamReader(resourceStream, new UTF8Encoding(false), true);
                            commitId = streamReader.ReadToEnd().Trim();

                            if (commitId.Length > 8)
                                commitId = commitId.Substring(0, 8);

                            versionInfo = $"{versionInfo}, #{commitId.Substring(0, 8)}";
                        }
                    }
                }
            }
            catch { }

            return versionInfo;
        }

        public static string GetDefaultCommandLineInterpreterPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

        public static string GetDefaultWindowsSandboxPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsSandbox.exe");

        public static readonly string[] ValidAccountNames = new string[]
        {
            "ContainerAdministrator",
            "ContainerUser",
            "WDAGUtilityAccount",
        };
    }
}
