using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TableCloth
{
    internal static class Helpers
    {
        public static bool IsDevelopmentBuild =>
#if DEBUG
            true
#else
            false
#endif
            ;

        public static string[] GetCommandLineArguments()
            => Environment.GetCommandLineArgs().Skip(1).ToArray();

        public static bool IsWindowsSandboxRunning()
            => Process.GetProcesses().Where(x => x.ProcessName.StartsWith("WindowsSandbox", StringComparison.OrdinalIgnoreCase)).Any();

        public static bool IsUnderWindowsSandboxSession()
        {
            // Note: %windir%\system32\win32queryhost.sandbox.dll 파일은 최신 버전의 Windows Sandbox부터 들어간 기능.
            // 모든 버전을 고려해야 한다면 이 파일의 존재 유무로 샌드박스 환경인지 아닌지 판정하는 것은 적절하지 않음.
            return SandboxAccountNames.Contains(Environment.UserName, StringComparer.OrdinalIgnoreCase);
        }

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

        public static Process CreateRunProcess(string comSpecPath, string targetExecutablePath, string arguments)
            => new Process()
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo(comSpecPath,
                    "/c start \"\" \"" + targetExecutablePath + "\" \"" + arguments + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

        public static string GetDefaultWindowsSandboxPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsSandbox.exe");

        public static readonly string[] SandboxAccountNames = new string[]
        {
            "WDAGUtilityAccount",
        };
    }
}
