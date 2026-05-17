using Microsoft.Win32;
using Spork.Components;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    /// <summary>
    /// Microsoft Edge가 public 컨텍스트(https://...)에서 사용자 PC의 로컬 서버(127.0.0.1:port 등)로
    /// 요청을 보낼 때 표시하는 Local Network Access(LNA) 권한 프롬프트를 정책으로 미리 자동 허용한다.
    ///
    /// 한국 인터넷뱅킹/금융 사이트 대부분은 안랩 Safe Transaction, INISafe, 금결원 NPKI 클라이언트 등
    /// 사용자 PC에 설치된 보안 SW가 로컬 포트에서 띄운 서버에 fetch를 시도하므로 매 사이트마다
    /// 사용자에게 "이 장치에서 다른 앱 및 서비스에 액세스" 프롬프트가 떠 자동화 흐름을 끊는다.
    /// </summary>
    /// <remarks>
    /// Edge 138부터 Chromium 신 LNA permission 흐름으로 갈렸기 때문에 구 정책 하나로는 부족하다.
    /// 세 정책을 함께 기록한다:
    /// <list type="bullet">
    ///   <item>
    ///     <b>LocalNetworkAccessAllowedForUrls</b> (Edge 140+) — 모든 origin("*")을 LNA 검사에서 면제.
    ///     레지스트리는 named subkey 안에 1, 2, 3... 값 이름으로 REG_SZ 목록.
    ///     <see href="https://learn.microsoft.com/deployedge/microsoft-edge-browser-policies/localnetworkaccessallowedforurls"/>
    ///   </item>
    ///   <item>
    ///     <b>LocalNetworkAccessPermissionsPolicyDefaultEnabled</b> (Edge 146+) — cross-origin iframe이
    ///     상위 origin의 LNA 권한을 자동 상속하게 한다. 우리은행 등은 INISafe/aXweb 등을 iframe으로
    ///     로드하므로 위 allow 정책만으로는 iframe origin의 요청이 차단된다.
    ///     <see href="https://learn.microsoft.com/deployedge/microsoft-edge-browser-policies/localnetworkaccesspermissionspolicydefaultenabled"/>
    ///   </item>
    ///   <item>
    ///     <b>InsecurePrivateNetworkRequestsAllowed</b> (Edge 87~) — 구 Private Network Access 프롬프트
    ///     대응. LNA가 도입되기 전 버전이나 일부 mixed-content 경로에 대한 fallback.
    ///   </item>
    /// </list>
    /// 모든 정책은 HKLM 하위에 기록되며 샌드박스 안의 정책 머신 단위 설정이라 호스트에 영향 없음.
    /// </remarks>
    public sealed class AllowEdgeLocalNetworkAccessStep : StepBase<InstallItemViewModel>
    {
        public AllowEdgeLocalNetworkAccessStep(
            IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        private const string EdgePolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Edge";
        private const string LocalNetworkAccessAllowedForUrlsSubKey = "LocalNetworkAccessAllowedForUrls";
        private const string LocalNetworkAccessPermissionsPolicyDefaultEnabledValueName = "LocalNetworkAccessPermissionsPolicyDefaultEnabled";
        private const string InsecurePrivateNetworkRequestsAllowedValueName = "InsecurePrivateNetworkRequestsAllowed";

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
        {
            // 모든 정책이 이미 우리가 원하는 값으로 설정되어 있다면 skip.
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(EdgePolicyKeyPath, writable: false))
                {
                    if (key == null)
                        return Task.FromResult(true);

                    var pnaValue = key.GetValue(InsecurePrivateNetworkRequestsAllowedValueName) as int?;
                    var lnaDefault = key.GetValue(LocalNetworkAccessPermissionsPolicyDefaultEnabledValueName) as int?;

                    using (var allowList = key.OpenSubKey(LocalNetworkAccessAllowedForUrlsSubKey, writable: false))
                    {
                        var firstEntry = allowList?.GetValue("1") as string;
                        var allSet = pnaValue == 1 && lnaDefault == 1 && string.Equals(firstEntry, "*", StringComparison.Ordinal);
                        return Task.FromResult(!allSet);
                    }
                }
            }
            catch
            {
                return Task.FromResult(true);
            }
        }

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            try
            {
                using (var edgeKey = Registry.LocalMachine.CreateSubKey(EdgePolicyKeyPath))
                {
                    if (edgeKey == null)
                        return Task.CompletedTask;

                    // 1) 모든 origin에 LNA 자동 허용. 정책 키는 LocalNetworkAccessAllowedForUrls라는 *subkey*이며
                    //    값 이름은 "1", "2", ... 순서로 매겨 REG_SZ로 저장한다(Edge ADMX 규약).
                    using (var allowList = edgeKey.CreateSubKey(LocalNetworkAccessAllowedForUrlsSubKey))
                    {
                        allowList?.SetValue("1", "*", RegistryValueKind.String);
                    }
                    progressCallback?.Invoke(33d);

                    // 2) cross-origin iframe이 상위 origin LNA 권한을 자동 상속하게 함(Edge 146+).
                    edgeKey.SetValue(LocalNetworkAccessPermissionsPolicyDefaultEnabledValueName, 1, RegistryValueKind.DWord);
                    progressCallback?.Invoke(66d);

                    // 3) 구 PNA fallback(Edge 87~). 새 LNA를 못 받은 빌드/경로에서도 프롬프트 회피.
                    edgeKey.SetValue(InsecurePrivateNetworkRequestsAllowedValueName, 1, RegistryValueKind.DWord);
                    progressCallback?.Invoke(90d);
                }

                // MS 공식 문서상 HKLM 직접 쓰기는 도메인 미가입 환경에선 즉시 효력이지만, AD GPO 경로와의
                // 일관성을 위해(또 일부 Edge 빌드가 Windows Policy Service 캐시를 참고하는 케이스 대비)
                // best-effort로 gpupdate를 호출해 Windows GP 엔진에도 새 값을 알린다. 실패해도 무시.
                TryInvokeGroupPolicyUpdate();
                progressCallback?.Invoke(100d);
            }
            catch (Exception ex)
            {
                // 정책 기록 실패는 카탈로그 설치 흐름을 막아서는 안 된다. 사용자는 사이트에서 프롬프트를
                // 한 번 더 보게 될 뿐이다.
                _appMessageBox.DisplayError(ex, false);
            }

            return Task.CompletedTask;
        }

        private static void TryInvokeGroupPolicyUpdate()
        {
            try
            {
                var gpupdatePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "gpupdate.exe");

                if (!File.Exists(gpupdatePath))
                    return;

                var psi = new ProcessStartInfo(gpupdatePath, "/force /target:computer")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var process = Process.Start(psi))
                {
                    // 깨끗한 sandbox에선 처리할 GPO가 거의 없어 보통 수 초 안에 끝난다. 무한정 기다리지 않도록 캡.
                    process?.WaitForExit(15000);
                }
            }
            catch
            {
                // gpupdate 호출 실패는 무시. HKLM 직접 쓰기로 정책 본체는 이미 적용된 상태.
            }
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
