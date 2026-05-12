using Microsoft.Win32;
using Spork.Components;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    /// <summary>
    /// Windows Smart App Control (SAC)을 비활성화하는 단계입니다.
    /// SAC가 활성화되어 있으면 서명되지 않은 프로그램이 실행되지 않을 수 있어
    /// 인터넷 뱅킹 프로그램 설치에 문제가 발생할 수 있습니다.
    /// 
    /// 참고: https://learn.microsoft.com/en-us/windows/security/application-security/application-control/app-control-for-business/appcontrol
    /// </summary>
    public sealed class DisableSmartAppControlStep : StepBase<InstallItemViewModel>
    {
        public DisableSmartAppControlStep(
            IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        private const string CiPolicyKeyPath = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
        private const string VerifiedAndReputablePolicyStateValue = "VerifiedAndReputablePolicyState";

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
        {
            // SAC가 활성화되어 있는 경우에만 이 단계를 실행합니다.
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(CiPolicyKeyPath, writable: false))
                {
                    if (key == null)
                        return Task.FromResult(false);

                    var value = key.GetValue(VerifiedAndReputablePolicyStateValue);
                    if (value == null)
                        return Task.FromResult(false);

                    // 0 = Off, 1 = Enforce, 2 = Evaluation
                    // 1 또는 2인 경우에만 비활성화가 필요합니다.
                    if (value is int intValue)
                        return Task.FromResult(intValue == 1 || intValue == 2);

                    return Task.FromResult(false);
                }
            }
            catch
            {
                // 레지스트리 접근 실패 시 단계를 건너뜁니다.
                return Task.FromResult(false);
            }
        }

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            try
            {
                // 레지스트리 값을 0 (Off)으로 설정합니다.
                using (var key = Registry.LocalMachine.OpenSubKey(CiPolicyKeyPath, writable: true))
                {
                    if (key != null)
                    {
                        key.SetValue(VerifiedAndReputablePolicyStateValue, 0, RegistryValueKind.DWord);
                    }
                }

                progressCallback?.Invoke(50d);

                // citool.exe를 실행하여 정책을 새로 고칩니다.
                RefreshAppControlPolicy();

                progressCallback?.Invoke(100d);
            }
            catch (AggregateException aex)
            {
                _appMessageBox.DisplayError(aex.InnerException, false);
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, false);
            }

            return Task.CompletedTask;
        }

        private void RefreshAppControlPolicy()
        {
            try
            {
                var ciToolPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "citool.exe");

                if (!System.IO.File.Exists(ciToolPath))
                    return;

                var psi = new ProcessStartInfo(ciToolPath, "--refresh")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var process = Process.Start(psi))
                {
                    process?.WaitForExit(10000); // 최대 10초 대기
                }
            }
            catch
            {
                // citool.exe 실행 실패는 무시합니다.
                // Windows 버전에 따라 citool.exe가 없을 수 있습니다.
            }
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
