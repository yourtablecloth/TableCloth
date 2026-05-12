using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

#nullable enable

namespace TableCloth
{
    public static class Helpers
    {
        public static bool IsDevelopmentBuild =>
#if DEBUG
            true
#else
            false
#endif
            ;

        private static string[]? _effectiveCommandLineArguments;

        /// <summary>
        /// verb 기반 디스패처가 발리언트 토큰을 소비한 뒤 모듈에 전달할 "유효" 인수 배열을 설정한다.
        /// 단일 바이너리 진입점이 <c>TableCloth.exe spork --foo bar</c>의 "spork" 토큰을 소비하고
        /// <c>["--foo", "bar"]</c>만 모듈에 노출하고 싶을 때 사용한다.
        /// </summary>
        public static void SetEffectiveCommandLineArguments(string[] args)
            => _effectiveCommandLineArguments = args ?? Array.Empty<string>();

        /// <summary>
        /// verb 디스패처가 설정한 유효 인수가 있으면 그것을, 없으면 OS 프로세스 인수에서
        /// 실행 파일 경로(arg 0)를 제외한 나머지를 반환한다.
        /// </summary>
        public static string[] GetCommandLineArguments()
            => _effectiveCommandLineArguments ?? Environment.GetCommandLineArgs().Skip(1).ToArray();

        public static bool IsWindowsSandboxRunning()
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

                if (commitTextFileName == null)
                    return versionInfo;

                var resourceStream = executingAssembly.GetManifestResourceStream(commitTextFileName);
                if (resourceStream == null)
                    return versionInfo;

                using (resourceStream)
                {
                    var streamReader = new StreamReader(resourceStream, new UTF8Encoding(false), true);
                    var commitId = streamReader.ReadToEnd().Trim();

                    if (string.IsNullOrEmpty(commitId))
                        return versionInfo;

                    if (commitId.Length > 8)
                        commitId = commitId.Substring(0, 8);

                    versionInfo = $"{versionInfo}, #{commitId}";
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
