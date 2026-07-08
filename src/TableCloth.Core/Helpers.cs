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
        // 전원 관리 옵션 "이름(GUID)"이 아니라, 활성 옵션의 실제 CPU 성능 상한(최대 프로세서 상태)을 읽어
        // 판단한다. 이름 기반 판별은 복제/OEM 커스텀 옵션을 오탐하고, AC의 '균형 조정'처럼 실제로는
        // 100%로 동작하는 경우도 잘못 경고했다. 여기서는 현재 전원 소스(AC/DC)의 최대 프로세서 상태를
        // 그대로 읽어, CPU가 실제로 제한(스로틀)될 때만(=100% 미만) Windows Sandbox 성능 저하로 판정한다.

        /// <summary>Windows 프로세서 전원 관리 하위 그룹 GUID(GUID_PROCESSOR_SETTINGS_SUBGROUP).</summary>
        private static readonly Guid ProcessorSettingsSubgroupGuid =
            new Guid("54533251-82be-4824-96c1-47b60b740d00");

        /// <summary>"최대 프로세서 상태" 설정 GUID(GUID_PROCESSOR_THROTTLE_MAXIMUM). 값은 0~100(%).</summary>
        private static readonly Guid ProcessorThrottleMaximumSettingGuid =
            new Guid("bc5038f7-23e0-4960-96da-33abaf5935ec");

        private const uint ERROR_SUCCESS = 0u;

        /// <summary>제한이 없는(=최고 성능) 최대 프로세서 상태 값(%).</summary>
        private const int FullProcessorStatePercent = 100;

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadACValueIndex(
            IntPtr rootPowerKey, in Guid schemeGuid, in Guid subGroupOfPowerSettingsGuid,
            in Guid powerSettingGuid, out uint acValueIndex);

        [DllImport("powrprof.dll")]
        private static extern uint PowerReadDCValueIndex(
            IntPtr rootPowerKey, in Guid schemeGuid, in Guid subGroupOfPowerSettingsGuid,
            in Guid powerSettingGuid, out uint dcValueIndex);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;       // 0 = 배터리(오프라인), 1 = AC(온라인), 255 = 알 수 없음
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        /// <summary>
        /// 최대 프로세서 상태 값(%)이 "제한됨(스로틀)"에 해당하는지 여부를 반환하는 순수 함수.
        /// 100% 미만이면 CPU 성능이 상한으로 제한된 것으로 본다.
        /// </summary>
        public static bool IsProcessorStateThrottled(int maxProcessorStatePercent)
            => maxProcessorStatePercent < FullProcessorStatePercent;

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

                return Marshal.PtrToStructure<Guid>(activePolicyGuidPtr);
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

        /// <summary>현재 배터리(DC)로 실행 중인지 여부. 판별 불가/AC면 <see langword="false"/>.</summary>
        private static bool IsRunningOnBattery()
        {
            if (!GetSystemPowerStatus(out var status))
                return false;

            // 0 = 배터리. 1(AC)/255(알 수 없음)은 AC로 간주해 AC 설정을 읽는다(보수적).
            return status.ACLineStatus == 0;
        }

        /// <summary>
        /// 현재 전원 소스(AC 또는 배터리)에 적용되는 활성 옵션의 "최대 프로세서 상태"(%)를 읽는다.
        /// 읽기에 실패하면 <see langword="null"/>을 반환한다.
        /// </summary>
        public static int? GetActiveMaxProcessorStatePercent()
        {
            var activeSchemeGuid = GetActivePowerSchemeGuid();
            if (!activeSchemeGuid.HasValue)
                return null;

            var schemeGuid = activeSchemeGuid.Value;

            var result = IsRunningOnBattery()
                ? PowerReadDCValueIndex(IntPtr.Zero, in schemeGuid, in ProcessorSettingsSubgroupGuid,
                    in ProcessorThrottleMaximumSettingGuid, out var value)
                : PowerReadACValueIndex(IntPtr.Zero, in schemeGuid, in ProcessorSettingsSubgroupGuid,
                    in ProcessorThrottleMaximumSettingGuid, out value);

            if (result != ERROR_SUCCESS)
                return null;

            return (int)value;
        }

        /// <summary>
        /// 호스트의 CPU 최대 성능이 제한(스로틀)되어 Windows Sandbox가 느려질 가능성이 높은지 여부를 반환한다.
        /// 현재 전원 소스의 최대 프로세서 상태가 100% 미만이면 <see langword="true"/>. 판별에 실패하면
        /// (예: 지원되지 않는 환경) <see langword="null"/>을 반환해, 확신할 때만 경고하도록 한다.
        /// </summary>
        public static bool? IsSandboxCpuLikelyThrottled()
        {
            var maxProcessorStatePercent = GetActiveMaxProcessorStatePercent();

            if (!maxProcessorStatePercent.HasValue)
                return null;

            return IsProcessorStateThrottled(maxProcessorStatePercent.Value);
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
