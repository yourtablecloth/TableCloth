using Spork.Components;
using Spork.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;
using TableCloth.Models.UserData;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class StepsComposer : IStepsComposer
    {
        public StepsComposer(
            TaskFactory taskFactory,
            IStepsFactory stepsFactory,
            ICommandLineArguments commandLineArguments,
            IResourceCacheManager resourceCacheManager,
            IInstallRecordStore installRecordStore)
        {
            _taskFactory = taskFactory;
            _stepsFactory = stepsFactory;
            _commandLineArguments = commandLineArguments;
            _resourceCacheManager = resourceCacheManager;
            _installRecordStore = installRecordStore;
        }

        private readonly TaskFactory _taskFactory;
        private readonly IStepsFactory _stepsFactory;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IInstallRecordStore _installRecordStore;

        public IEnumerable<StepItemViewModel> ComposeSteps()
            => ComposeStepsInternal(_commandLineArguments.GetCurrent().SelectedServices);

        public IEnumerable<StepItemViewModel> ComposeStepsForSites(IEnumerable<string> selectedServiceIds)
            => ComposeStepsInternal(selectedServiceIds ?? Enumerable.Empty<string>());

        private IEnumerable<StepItemViewModel> ComposeStepsInternal(IEnumerable<string> targets)
        {
            var parsedArgs = _commandLineArguments.GetCurrent();
            var catalog = _resourceCacheManager.CatalogDocument;
            var steps = new List<StepItemViewModel>();

            // SetDesktopWallpaperStep은 사이트 설치 단계가 아니라 wsb 부팅 시점
            // (AppStartup.InitializeAsync)에서 1회만 적용되도록 옮겼다.
            steps.AddRange(new[]
            {
                new StepItemViewModel()
                {
                    Step = _stepsFactory.GetStepByName(nameof(PrepareDirectoriesStep)),
                    Argument = new InstallItemViewModel(),
                    TargetSiteName = UIStringResources.Option_Prerequisites,
                    PackageName = UIStringResources.Install_PrepareEnvironment,
                },
                // Smart App Control 비활성화는 본 단계에서 처리하지 않는다. citool --refresh 가
                // 실행 중인 Spork 프로세스를 untrusted로 재평가해 강제 종료시키는 사례가 잦아
                // StartupScript.cmd 의 reg.exe + citool.exe 호출로 옮겼다. EV 서명된 System32
                // 바이너리는 SAC 가 신뢰하므로 batch 단계에서 안전하게 실행된다.
                // Local Network Access(127.0.0.1 등) 프롬프트를 자동 허용하는 Edge 정책을 미리 적용.
                // 후속 ReloadEdgeStep이 msedge를 재기동하면 새 인스턴스부터 정책이 즉시 반영된다.
                new StepItemViewModel()
                {
                    Step = _stepsFactory.GetStepByName(nameof(AllowEdgeLocalNetworkAccessStep)),
                    Argument = new InstallItemViewModel(),
                    TargetSiteName = UIStringResources.Option_Prerequisites,
                    PackageName = UIStringResources.Install_AllowEdgeLocalNetworkAccess,
                },
            });

            // 같은 사이트를 반복 진입하는 워크플로에서 매번 같은 패키지를 재설치하지 않도록,
            // 이미 설치된 fingerprint 와 일치하는 항목은 본 단계에서 제외한다.
            // ForceReinstall 토글이 켜져 있으면 모든 항목을 다시 포함한다.
            var forceReinstall = _installRecordStore.ForceReinstall;

            bool ShouldInclude(string fingerprint)
                => forceReinstall || !_installRecordStore.IsInstalled(fingerprint);

            foreach (var eachTargetName in targets)
            {
                var targetService = catalog.Services.FirstOrDefault(x => string.Equals(eachTargetName, x.Id, StringComparison.Ordinal));

                if (targetService == null)
                    continue;

                steps.AddRange(targetService.Packages
                    .Where(eachPackage => ShouldInclude(PackageFingerprints.ForPackage(eachPackage.Url, eachPackage.Arguments)))
                    .Select(eachPackage => new StepItemViewModel()
                    {
                        Step = _stepsFactory.GetStepByName(nameof(PackageInstallStep)),
                        Argument = new PackageInstallItemViewModel
                        {
                            PackageUrl = eachPackage.Url,
                            Arguments = eachPackage.Arguments,
                        },
                        TargetSiteName = targetService.DisplayName,
                        TargetSiteUrl = targetService.Url,
                        PackageName = eachPackage.Name,
                    }));

                steps.AddRange(targetService.EdgeExtensions
                    .Where(eachEdgeExtension => ShouldInclude(PackageFingerprints.ForEdgeExtension(eachEdgeExtension.ExtensionId)))
                    .Select(eachEdgeExtension => new StepItemViewModel()
                    {
                        Step = _stepsFactory.GetStepByName(nameof(EdgeExtensionInstallStep)),
                        Argument = new EdgeExtensionInstallItemViewModel
                        {
                            EdgeExtensionId = eachEdgeExtension.ExtensionId,
                            EdgeCrxUrl = eachEdgeExtension.CrxUrl,
                        },
                        TargetSiteName = targetService.DisplayName,
                        TargetSiteUrl = targetService.Url,
                        PackageName = eachEdgeExtension.Name,
                    }));

                var bootstrapData = targetService.CustomBootstrap;

                if (!string.IsNullOrWhiteSpace(bootstrapData)
                    && ShouldInclude(PackageFingerprints.ForPowerShellScript(bootstrapData)))
                {
                    steps.Add(new StepItemViewModel()
                    {
                        Step = _stepsFactory.GetStepByName(nameof(PowerShellScriptRunStep)),
                        Argument = new PowerShellScriptInstallItemViewModel
                        {
                            ScriptContent = bootstrapData,
                        },
                        TargetSiteName = targetService.DisplayName,
                        TargetSiteUrl = targetService.Url,
                        PackageName = UIStringResources.Spork_CustomScript_Title,
                    });
                }
            }

            steps.AddRange(new[]
            {
                new StepItemViewModel()
                {
                    Step = _stepsFactory.GetStepByName(nameof(ConfigAhnLabSafeTransactionStep)),
                    Argument = new InstallItemViewModel(),
                    TargetSiteName = UIStringResources.Option_Config,
                    PackageName = UIStringResources.Install_ConfigASTx,
                },
                new StepItemViewModel()
                {
                    Step = _stepsFactory.GetStepByName(nameof(ReloadEdgeStep)),
                    Argument = new InstallItemViewModel(),
                    TargetSiteName = UIStringResources.Option_Config,
                    PackageName = UIStringResources.Install_ReloadMicrosoftEdge,
                },
            });

            // 보조 프로그램(Adobe Reader / 모두의 프린터 / 한컴오피스 뷰어 / RaiDrive) 자동 설치 step은
            // 카탈로그의 <Companions> 목록 + 보조 프로그램 탭의 PackageInstallStep 흐름으로 대체되었다.
            // (기존 OpenWebSiteStep 방식은 다운로드 페이지만 열고 실제 설치를 사용자에게 떠넘기던
            //  애매한 흐름이라 제거한다.)

            return steps;
        }
    }
}
