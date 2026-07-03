using Spork.Components;
using Spork.ViewModels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    public sealed class ConfigAhnLabSafeTransactionStep : StepBase<InstallItemViewModel>
    {
        public ConfigAhnLabSafeTransactionStep(
            IAppUserInterface appUserInterface)
        {
            _appUserInterface = appUserInterface;
        }

        private readonly IAppUserInterface _appUserInterface;

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel _, CancellationToken cancellationToken = default)
        {
            var stSessPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AhnLab", "Safe Transaction", "StSess.exe");

            var hasStSess = File.Exists(stSessPath);
            return Task.FromResult(hasStSess);
        }

        public override Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            var stSessPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AhnLab", "Safe Transaction", "StSess.exe");

            // 이슈 #275: 기존의 단순 OK 메시지 2개 대신, 시각 가이드 + 설정 열기 + 확인 게이트를 담은
            // 전용 안내 창을 띄운다. 사용자가 "원격 접속 차단" 해제를 확인해야 계속할 수 있게 유도한다.
            // 실제 설정값은 ASTx가 암호화 저장하고 외부에서 검증할 수 없어, 확인은 사용자 확인 게이트로만 가능하다.
            _appUserInterface.ShowAhnLabSafeTxGuideDialog(stSessPath);

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
