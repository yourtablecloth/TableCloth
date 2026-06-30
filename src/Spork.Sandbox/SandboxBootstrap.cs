using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Spork.Components;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Answers;
using TableCloth.Models.WindowsSandbox;

#nullable enable annotations

namespace Spork.Sandbox
{
    /// <summary>
    /// Windows Sandbox 진입 직후 한 번만 필요한 부팅 초기화 — 8.8.8.8/1.1.1.1 DNS 강제 설정,
    /// 호스트가 떨궈둔 인증서 쌍의 NPKI 표준 경로 배치, RO 마운트된 NPKI를 쓰기 가능 사본으로 복제.
    /// 본 구현은 Spork.Sandbox 어셈블리에만 존재하며, 단독 Spork.exe 릴리스(비-샌드박스 재단)는
    /// 이 어셈블리를 참조하지 않으므로 sandbox 전용 코드가 standalone 바이너리에 포함되지 않는다.
    /// </summary>
    public sealed class SandboxBootstrap : ISandboxBootstrap
    {
        public SandboxBootstrap(ILogger<SandboxBootstrap> logger)
        {
            _logger = logger;
        }

        private readonly ILogger<SandboxBootstrap> _logger;

        /// <summary>
        /// 호스트 staging이 그대로 노출되는 샌드박스 사용자 계정명. 환경 변수나 UserName이 이 값일 때만
        /// 부팅 초기화 작업을 시도한다(다른 환경에서 실수로 DNS를 바꾸지 않도록 가드).
        /// </summary>
        private const string SandboxUserName = "WDAGUtilityAccount";

        private static readonly string[] PublicDnsServers = new[] { "8.8.8.8", "1.1.1.1" };

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunningInSandbox())
            {
                _logger.LogDebug("SandboxBootstrap skipped — not running inside Windows Sandbox.");
                return Task.CompletedTask;
            }

            // 호스트의 라이트/다크 선호를 시작 시점에 게스트 테마로 반영(이슈 #246). 가장 먼저 적용해
            // 이후 띄워질 앱(은행 사이트 등)과 Spork 자체 UI가 올바른 테마로 시작하도록 한다.
            // 다크 모드면 지정 배경 이미지도 함께 적용한다.
            TryApplyHostTheme();

            // 호스트가 고대비를 쓰고 있으면 같은 구성표로 맞춘다(베스트에포트).
            TryApplyHighContrast();

            // DNS 설정은 fire-and-forget. catalog HTTP 호출 전에 끝나는 게 이상적이지만 그렇지 못해도
            // catalog 로드는 1.5s × 3회 retry 백오프가 있어 자체 회복 가능. UI 진입(splash)을 막지 않는
            // 쪽이 사용자 체감 속도에 더 중요하므로 await 하지 않는다. 실패해도 _logger 외엔 영향 없음.
            _ = Task.Run(() => TryConfigurePublicDnsAsync(cancellationToken), cancellationToken);

            TryPlaceCertPair();
            TryCopyNpkiMountToCanonicalPath();
            return Task.CompletedTask;
        }

        private static bool IsRunningInSandbox()
        {
            // wsb LogonCommand로 부팅된 샌드박스에서는 사용자 계정이 WDAGUtilityAccount로 고정된다.
            // 호스트 측에서 우연히 같은 계정으로 실행되는 케이스를 막기 위해 Desktop 경로까지 함께 확인.
            return string.Equals(Environment.UserName, SandboxUserName, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(SandboxMountPaths.SandboxDesktop);
        }

        private async Task TryConfigurePublicDnsAsync(CancellationToken cancellationToken)
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up)
                        continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;

                    await SetDnsForInterfaceAsync(nic.Name, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure public DNS servers in sandbox.");
            }
        }

        private async Task SetDnsForInterfaceAsync(string interfaceName, CancellationToken cancellationToken)
        {
            // 첫 항목은 primary로 set, 나머지는 add dns ... index=N. netsh는 stdout/stderr를 흡수해
            // UI에 토할 출력이 새지 않도록 한다.
            await RunNetshAsync(
                $"interface ipv4 set dns name=\"{interfaceName}\" static {PublicDnsServers[0]} primary",
                cancellationToken).ConfigureAwait(false);

            for (var i = 1; i < PublicDnsServers.Length; i++)
            {
                await RunNetshAsync(
                    $"interface ipv4 add dns name=\"{interfaceName}\" addr={PublicDnsServers[i]} index={i + 1}",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task RunNetshAsync(string arguments, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo("netsh.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return;

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                _logger.LogDebug("netsh {args} exited with code {code}.", arguments, process.ExitCode);
            }
        }

        private void TryPlaceCertPair()
        {
            try
            {
                var answers = LoadSporkAnswers();
                if (answers == null || !answers.HasCertPair)
                    return;

                var certStagingDir = Path.Combine(AppContext.BaseDirectory, "certs");
                if (!Directory.Exists(certStagingDir))
                {
                    _logger.LogWarning("HasCertPair=true but staging directory '{dir}' not found.", certStagingDir);
                    return;
                }

                var npkiTargetDir = BuildNpkiTargetPath(answers);
                var desktopCertsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Certificates");

                Directory.CreateDirectory(npkiTargetDir);
                Directory.CreateDirectory(desktopCertsDir);

                foreach (var sourceFile in Directory.EnumerateFiles(certStagingDir))
                {
                    var fileName = Path.GetFileName(sourceFile);
                    // NPKI에는 복사본을 두고(은행 SW가 갱신/캐시 쓰기 작업), Desktop에는 사용자가 백업할 수 있는
                    // 사본을 같이 둔다. 원본은 staging에 남겨두지 않고 정리.
                    File.Copy(sourceFile, Path.Combine(npkiTargetDir, fileName), overwrite: true);
                    File.Copy(sourceFile, Path.Combine(desktopCertsDir, fileName), overwrite: true);
                }

                try { Directory.Delete(certStagingDir, recursive: true); }
                catch (Exception ex) { _logger.LogDebug(ex, "Failed to remove cert staging directory."); }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to place cert pair into NPKI standard path.");
            }
        }

        /// <summary>
        /// 은행/금융 SW가 인증서를 기대하는 표준 경로(<c>%USERPROFILE%\AppData\LocalLow\NPKI\&lt;O&gt;\...</c>).
        /// 개인용 인증서는 <c>USER\&lt;Subject DN&gt;</c> 하위로 더 들어간다.
        /// 호스트 측 SandboxBuilder의 옛 GetNPKIPathForSandbox 로직과 동일한 규칙.
        /// </summary>
        private static string BuildNpkiTargetPath(SporkAnswers answers)
        {
            var basePath = SandboxMountPaths.NpkiCanonicalPath; // %userprofile%\AppData\LocalLow\NPKI
            if (string.IsNullOrWhiteSpace(answers.CertOrganization))
                return basePath;

            var path = Path.Combine(basePath, answers.CertOrganization);
            if (answers.CertIsPersonalCert && !string.IsNullOrWhiteSpace(answers.CertSubjectNameForNpkiApp))
                path = Path.Combine(path, "USER", answers.CertSubjectNameForNpkiApp);

            return path;
        }

        private void TryCopyNpkiMountToCanonicalPath()
        {
            // 호스트 NPKI 폴더가 RO 마운트로 Desktop\NPKI에 노출된 경우, AppData\LocalLow\NPKI로 쓰기 가능 사본을
            // 만들어 두지 않으면 은행 SW가 갱신/캐시 쓰기를 시도하다 실패한다. junction 대신 사본을 두는 이유는
            // 기존 SandboxBuilder 주석과 동일.
            try
            {
                if (!Directory.Exists(SandboxMountPaths.NpkiDesktopMount))
                    return;

                Directory.CreateDirectory(SandboxMountPaths.NpkiCanonicalPath);
                CopyDirectoryRecursive(SandboxMountPaths.NpkiDesktopMount, SandboxMountPaths.NpkiCanonicalPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy NPKI mount to canonical path.");
            }
        }

        private static void CopyDirectoryRecursive(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (var file in Directory.EnumerateFiles(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(file));
                File.Copy(file, target, overwrite: true);
            }

            foreach (var dir in Directory.EnumerateDirectories(source))
            {
                var target = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, target);
            }
        }

        // --- 이슈 #246: 호스트 라이트/다크 테마를 시작 시점에 게스트로 반영 ---
        private const int HwndBroadcast = 0xFFFF;
        private const uint WmSettingChange = 0x001A;
        private const uint SmtoAbortIfHung = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeoutW(
            IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

        private const uint SpiSetDeskWallpaper = 0x0014;
        private const uint SpiSetHighContrast = 0x0043;
        private const uint SpifUpdateIniFile = 0x0001;
        private const uint SpifSendChange = 0x0002;
        private const uint HcfHighContrastOn = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct HIGHCONTRAST
        {
            public uint cbSize;
            public uint dwFlags;
            public IntPtr lpszDefaultScheme;
        }

        // 같은 user32!SystemParametersInfoW 이지만 pvParam 형태가 달라 관리 시그니처를 둘로 나눈다.
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfoString(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfoHighContrast(uint uiAction, uint uiParam, ref HIGHCONTRAST pvParam, uint fWinIni);

        /// <summary>
        /// 호스트가 <see cref="SporkAnswers.HostUsesLightTheme"/>로 전달한 라이트/다크 선호를
        /// 게스트의 <c>HKCU\...\Themes\Personalize</c>에 적용하고, 설정 앱과 동일하게
        /// <c>WM_SETTINGCHANGE("ImmersiveColorSet")</c>를 브로드캐스트해 이미 떠 있는 셸/앱을 갱신한다.
        /// 값이 <see langword="null"/>(알 수 없음)이면 샌드박스 기본 테마를 유지한다.
        /// 한계: RDP에는 호스트 테마 전이를 세션으로 전달할 채널이 없어 "시작 시점 일치"까지만 가능하다.
        /// </summary>
        private void TryApplyHostTheme()
        {
            try
            {
                var answers = LoadSporkAnswers();
                if (answers?.HostUsesLightTheme is not bool usesLightTheme)
                    return;

                var mode = usesLightTheme ? 1 : 0;
                using (var personalizeKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    // 설정 앱이 라이트/다크 토글 시 함께 바꾸는 두 값(앱 테마 + 시스템 테마)을 같이 맞춘다.
                    personalizeKey.SetValue("AppsUseLightTheme", mode, RegistryValueKind.DWord);
                    personalizeKey.SetValue("SystemUsesLightTheme", mode, RegistryValueKind.DWord);
                }

                // 이미 떠 있는 explorer/앱이 새 테마를 다시 읽도록 알림(설정 앱과 동일 메커니즘).
                // 응답 없는 창에서 멈추지 않도록 SendMessageTimeout + ABORTIFHUNG + 1s 타임아웃.
                SendMessageTimeoutW((IntPtr)HwndBroadcast, WmSettingChange, IntPtr.Zero,
                    "ImmersiveColorSet", SmtoAbortIfHung, 1000u, out _);

                // 다크 모드로 시작하면 번들된 배경 이미지를 적용한다(이슈 #246).
                if (!usesLightTheme)
                    TryApplyDarkWallpaper();

                _logger.LogDebug("Applied host theme to sandbox: {Theme}.", usesLightTheme ? "Light" : "Dark");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply host theme to sandbox.");
            }
        }

        /// <summary>
        /// 다크 모드 시작 시 App\Assets\sandbox-dark-wallpaper.jpg 를 바탕 화면 이미지로 적용한다.
        /// 이미지 라이선스/크레디트는 docs/CREDITS.md 참조(Unsplash License, 사진: Ty Rethy).
        /// </summary>
        private void TryApplyDarkWallpaper()
        {
            try
            {
                var wallpaperPath = Path.Combine(AppContext.BaseDirectory, "Assets", "sandbox-dark-wallpaper.jpg");
                if (!File.Exists(wallpaperPath))
                {
                    _logger.LogDebug("Dark wallpaper not found at {Path}; skipping.", wallpaperPath);
                    return;
                }

                // SPI_SETDESKWALLPAPER 는 JPG 경로를 받아 내부적으로 적용한다.
                SystemParametersInfoString(SpiSetDeskWallpaper, 0u, wallpaperPath,
                    SpifUpdateIniFile | SpifSendChange);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply dark wallpaper.");
            }
        }

        /// <summary>
        /// 호스트가 고대비를 쓰고 있으면(<see cref="SporkAnswers.HostHighContrastScheme"/> 비어있지 않음)
        /// 같은 구성표 이름으로 게스트 고대비를 켠다. 베스트에포트 — 고대비의 프로그래밍 방식 적용은
        /// 환경에 따라 완전하지 않을 수 있다(이슈 #246).
        /// </summary>
        private void TryApplyHighContrast()
        {
            var schemePtr = IntPtr.Zero;
            try
            {
                var answers = LoadSporkAnswers();
                var scheme = answers?.HostHighContrastScheme;
                if (string.IsNullOrWhiteSpace(scheme))
                    return;

                schemePtr = Marshal.StringToHGlobalUni(scheme);
                var hc = new HIGHCONTRAST
                {
                    cbSize = (uint)Marshal.SizeOf<HIGHCONTRAST>(),
                    dwFlags = HcfHighContrastOn,
                    lpszDefaultScheme = schemePtr,
                };

                SystemParametersInfoHighContrast(SpiSetHighContrast, hc.cbSize, ref hc,
                    SpifUpdateIniFile | SpifSendChange);

                _logger.LogDebug("Applied high contrast scheme to sandbox: {Scheme}.", scheme);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply high contrast to sandbox.");
            }
            finally
            {
                if (schemePtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(schemePtr);
            }
        }

        private SporkAnswers? LoadSporkAnswers()
        {
            try
            {
                var answerFilePath = Path.Combine(AppContext.BaseDirectory, "SporkAnswers.json");
                if (!File.Exists(answerFilePath))
                    return null;

                using var stream = File.OpenRead(answerFilePath);
                return JsonSerializer.Deserialize<SporkAnswers>(stream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load SporkAnswers.json.");
                return null;
            }
        }
    }
}
