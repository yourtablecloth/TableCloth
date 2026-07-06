using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

#nullable enable

namespace TableCloth
{
    public static class Helpers
    {
        /// <summary>
        /// Windows 기본 제공 "고성능(High performance)" 전원 관리 옵션의 GUID.
        /// </summary>
        private static readonly Guid HighPerformancePowerSchemeGuid =
            new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

        /// <summary>
        /// Windows 기본 제공 "최고의 성능(Ultimate Performance)" 전원 관리 옵션의 GUID.
        /// </summary>
        private static readonly Guid UltimatePerformancePowerSchemeGuid =
            new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        private const uint ERROR_SUCCESS = 0u;

        /// <summary>
        /// 지정한 전원 관리 옵션 GUID가 "고성능" 또는 "최고의 성능" 구성표인지 여부를 반환한다.
        /// Windows Sandbox의 시작 성능에 유리한 구성표를 판별하는 데 사용한다.
        /// </summary>
        public static bool IsHighPerformancePowerScheme(Guid schemeGuid)
            => schemeGuid == HighPerformancePowerSchemeGuid
            || schemeGuid == UltimatePerformancePowerSchemeGuid;

        /// <summary>
        /// 호스트에서 현재 활성화된 전원 관리 옵션의 GUID를 <c>PowerGetActiveScheme</c>로 읽는다.
        /// 읽기에 실패하면 <see langword="null"/>을 반환한다.
        /// </summary>
        public static Guid? GetActivePowerSchemeGuid()
        {
            var activePolicyGuidPtr = IntPtr.Zero;

            try
            {
                if (PowerGetActiveScheme(IntPtr.Zero, out activePolicyGuidPtr) != ERROR_SUCCESS)
                    return null;

                if (activePolicyGuidPtr == IntPtr.Zero)
                    return null;

                return (Guid)Marshal.PtrToStructure(activePolicyGuidPtr, typeof(Guid));
            }
            catch
            {
                return null;
            }
            finally
            {
                if (activePolicyGuidPtr != IntPtr.Zero)
                    LocalFree(activePolicyGuidPtr);
            }
        }

        /// <summary>
        /// 호스트의 활성 전원 관리 옵션이 고성능 계열인지 여부를 반환한다.
        /// 판별에 실패하면(예: 지원되지 않는 환경) <see langword="null"/>을 반환한다.
        /// </summary>
        public static bool? IsHighPerformancePowerSchemeActive()
        {
            var activeSchemeGuid = GetActivePowerSchemeGuid();

            if (!activeSchemeGuid.HasValue)
                return null;

            return IsHighPerformancePowerScheme(activeSchemeGuid.Value);
        }

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
