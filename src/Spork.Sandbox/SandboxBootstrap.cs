using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Spork.Components;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Answers;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Serialization;

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

        // probe-then-fallback(이슈 #285): 게스트 현재 DNS로 아래 호스트가 하나라도 해석되면 DNS가 동작한다고
        // 보고 공용 DNS로 덮어쓰지 않는다. 모두 실패해야 폴백한다(보수적 판정).
        private static readonly string[] DnsProbeHosts = new[] { "github.com", "www.microsoft.com" };

        public Task RunAsync(CancellationToken cancellationToken = default)
        {
            if (!IsRunningInSandbox())
            {
                _logger.LogDebug("SandboxBootstrap skipped — not running inside Windows Sandbox.");
                return Task.CompletedTask;
            }

            // 호스트의 라이트/다크 선호를 시작 시점에 게스트 테마로 반영(이슈 #246). 가장 먼저 적용해
            // 이후 띄워질 앱(은행 사이트 등)과 Spork 자체 UI가 올바른 테마로 시작하도록 한다.
            // (다크 배경 이미지는 되돌림 방지를 위해 시작 배치 StartupScript.cmd 에서 정책으로 설정한다.)
            TryApplyHostTheme();

            // 호스트가 고대비를 쓰고 있으면 같은 구성표로 맞춘다(베스트에포트).
            TryApplyHighContrast();

            // DNS 설정은 fire-and-forget. catalog HTTP 호출 전에 끝나는 게 이상적이지만 그렇지 못해도
            // catalog 로드는 1.5s × 3회 retry 백오프가 있어 자체 회복 가능. UI 진입(splash)을 막지 않는
            // 쪽이 사용자 체감 속도에 더 중요하므로 await 하지 않는다. 실패해도 _logger 외엔 영향 없음.
            _ = Task.Run(() => TryConfigurePublicDnsAsync(cancellationToken), cancellationToken);

            TryPlaceCertPair();
            TryCopyNpkiMountToCanonicalPath();
            TryPropagateZScalerRootCert();
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
                // 옵션(이슈 #285): 기본 true(폴백 허용). false면 어떤 DNS 수정도 하지 않는다(사내 정책 등).
                var allowFallback = LoadSporkAnswers()?.EnableSandboxPublicDnsFallback ?? true;
                if (!allowFallback)
                {
                    _logger.LogDebug("Public DNS fallback disabled by preference — leaving guest DNS untouched.");
                    return;
                }

                // probe-then-fallback: 기존 DNS로 이름 해석이 되면(사내 내부 리졸버 등) 덮어쓰지 않는다.
                // 실패할 때만 공용 DNS로 폴백해, DNS가 안 잡히는 바 샌드박스만 구제한다.
                if (await IsDnsResolutionWorkingAsync(cancellationToken).ConfigureAwait(false))
                {
                    _logger.LogDebug("Guest DNS already resolves — skipping public DNS fallback.");
                    return;
                }

                _logger.LogDebug("Guest DNS resolution failing — applying public DNS fallback (8.8.8.8/1.1.1.1).");
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

        /// <summary>
        /// 게스트의 현재 DNS로 대표 호스트를 해석해 본다. 하나라도 3초 내 해석되면 DNS가 동작하는 것으로 본다.
        /// 모두 실패해야 "해석 불가"로 판정 — 정상 DNS(사내 내부 리졸버 포함)를 덮어쓰지 않도록 보수적으로 판단한다.
        /// </summary>
        private async Task<bool> IsDnsResolutionWorkingAsync(CancellationToken cancellationToken)
        {
            foreach (var host in DnsProbeHosts)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token).ConfigureAwait(false);
                    if (addresses.Length > 0)
                        return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "DNS probe for {host} failed.", host);
                }
            }
            return false;
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

        /// <summary>
        /// 호스트가 <c>App\zscaler\zscaler.pem</c>으로 스테이징한 ZScaler 루트 인증서를 게스트로 전파한다(이슈 #292).
        /// ZScaler 같은 SSL 검사(엔드포인트 통제) 환경에서는 호스트가 신뢰하는 ZScaler 루트 인증서가
        /// 샌드박스로 자동 전파되지 않아 게스트 HTTPS 연결이 모두 차단된다. 옵션이 켜져 있고 PEM이 존재하면:
        /// <list type="number">
        /// <item>사용자 신뢰 루트(<c>CurrentUser\Root</c>)에 인증서를 등록해 브라우저·SChannel HTTPS를 복원하고,</item>
        /// <item>Node.js를 위해 <c>NODE_EXTRA_CA_CERTS</c>(User 스코프)를 PEM 경로로 설정하고,</item>
        /// <item>git이 SChannel(윈도우 인증서 저장소)을 쓰도록 <c>http.sslBackend=schannel</c>을 구성한다.</item>
        /// </list>
        /// 옵션이 꺼져 있거나 PEM이 없으면 아무 것도 하지 않는다(베스트에포트).
        /// </summary>
        private void TryPropagateZScalerRootCert()
        {
            try
            {
                if (!(LoadSporkAnswers()?.EnableZScalerRootCertPropagation ?? false))
                    return;

                var pemPath = Path.Combine(AppContext.BaseDirectory, "zscaler", "zscaler.pem");
                if (!File.Exists(pemPath))
                {
                    _logger.LogDebug("ZScaler propagation enabled but no staged PEM found — skipping.");
                    return;
                }

                var certificates = new X509Certificate2Collection();
                certificates.ImportFromPemFile(pemPath);
                if (certificates.Count < 1)
                {
                    _logger.LogDebug("Staged ZScaler PEM contained no certificates — skipping.");
                    return;
                }

                try
                {
                    // 1) 현재 사용자 신뢰 루트에 등록(상승 불필요, SChannel/브라우저 대응).
                    //    Store.Add는 인증서를 저장소로 복제하므로, 아래 finally에서 원본을 해제해도 안전하다.
                    using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                    {
                        store.Open(OpenFlags.ReadWrite);
                        store.AddRange(certificates);
                    }

                    // 2) Node.js: 저장소 무관하게 PEM을 직접 지정(User 스코프 + 현재 세션 즉시 반영).
                    Environment.SetEnvironmentVariable("NODE_EXTRA_CA_CERTS", pemPath, EnvironmentVariableTarget.User);
                    Environment.SetEnvironmentVariable("NODE_EXTRA_CA_CERTS", pemPath, EnvironmentVariableTarget.Process);

                    // 3) git이 SChannel(윈도우 인증서 저장소)을 사용하도록 구성(선택).
                    TryConfigureGitSChannel();

                    _logger.LogDebug("Propagated {count} ZScaler root certificate(s) into the sandbox.", certificates.Count);
                }
                finally
                {
                    // X509Certificate2는 IDisposable(키 핸들 보유)이므로 등록 후 원본을 해제한다.
                    foreach (var cert in certificates)
                        cert.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to propagate ZScaler root certificate into sandbox.");
            }
        }

        private void TryConfigureGitSChannel()
        {
            try
            {
                var startInfo = new ProcessStartInfo("git.exe", "config --global http.sslBackend schannel")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var process = Process.Start(startInfo);
                if (process is not null && !process.WaitForExit(TimeSpan.FromSeconds(5)))
                {
                    // 5초 내 종료하지 않으면(예: git 자격 증명 프롬프트로 멈춤) 좀비로 남지 않도록 강제 종료한다.
                    try { process.Kill(entireProcessTree: true); }
                    catch { /* 이미 종료됐거나 접근 불가 — 무시 */ }
                }
            }
            catch (Exception ex)
            {
                // git이 없을 수 있다(샌드박스 기본 이미지). 인증서 등록만으로도 대부분의 HTTPS가 복원되므로 무해.
                _logger.LogDebug(ex, "Skipped configuring git SChannel backend (git may be unavailable).");
            }
        }

        // --- 이슈 #246: 호스트 라이트/다크 테마를 시작 시점에 게스트로 반영 ---
        private const int HwndBroadcast = 0xFFFF;
        private const uint WmSettingChange = 0x001A;
        private const uint SmtoAbortIfHung = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeoutW(
            IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

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

                _logger.LogDebug("Applied host theme to sandbox: {Theme}.", usesLightTheme ? "Light" : "Dark");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply host theme to sandbox.");
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
                return JsonSerializer.Deserialize(stream, SporkJsonContext.Default.SporkAnswers);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load SporkAnswers.json.");
                return null;
            }
        }
    }
}
