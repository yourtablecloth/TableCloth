using System.Collections.Generic;
using System.Linq;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Models
{
    public sealed class CommandLineArgumentModel
    {
        public CommandLineArgumentModel(
            string[] rawArguments,
            string[] selectedServices = default,
            bool? enableMicrophone = default,
            bool? enableWebCam = default,
            bool? enablePrinters = default,
            string certPrivateKeyPath = default,
            string certPublicKeyPath = default,
            bool showCommandLineHelp = default,
            bool showVersionHelp = default,
            bool dryRun = default,
            bool simulateFailure = false,
            IEnumerable<MappedFolderSetting> mappedFolders = default,
            string targetUrl = default)
        {
            RawArguments = rawArguments;
            SelectedServices = selectedServices ?? Enumerable.Empty<string>();
            EnableMicrophone = enableMicrophone;
            EnableWebCam = enableWebCam;
            EnablePrinters = enablePrinters;
            CertPrivateKeyPath = certPrivateKeyPath;
            CertPublicKeyPath = certPublicKeyPath;
            ShowCommandLineHelp = showCommandLineHelp;
            ShowVersionHelp = showVersionHelp;
            DryRun = dryRun;
            SimulateFailure = simulateFailure;
            MappedFolders = mappedFolders ?? Enumerable.Empty<MappedFolderSetting>();
            TargetUrl = targetUrl;
        }

        public string[] RawArguments { get; private set; }

        public bool? EnableMicrophone { get; private set; }

        public bool? EnableWebCam { get; private set; }

        public bool? EnablePrinters { get; private set; }

        public string CertPrivateKeyPath { get; private set; } = null;

        public string CertPublicKeyPath { get; private set; } = null;

        public bool ShowCommandLineHelp { get; private set; }

        public bool ShowVersionHelp { get; private set; }

        public IEnumerable<string> SelectedServices { get; private set; } = new List<string>();

        /// <summary>
        /// 외부(무설치 `.wsb` 딥링크, 브라우저 익스텐션)에서 전달된 대상 URL.
        /// </summary>
        /// <remarks>
        /// 신뢰할 수 없는 입력이다. 실제로 열기 전에 반드시
        /// <see cref="Catalog.CatalogTargetUrlMatcher"/> 로 카탈로그 도메인 게이트를 통과시켜야 한다.
        /// </remarks>
        public string TargetUrl { get; private set; } = null;

        public bool DryRun { get; private set; }

        public bool SimulateFailure { get; private set; }

        public IEnumerable<MappedFolderSetting> MappedFolders { get; private set; } = new List<MappedFolderSetting>();

        /// <summary>
        /// 카탈로그 도메인 게이트를 통과한 결과로 대상 사이트/URL 만 교체한 새 모델을 만든다.
        /// </summary>
        /// <remarks>
        /// 게이트를 통과하지 못한 URL 은 <paramref name="targetUrl"/> 에 <see langword="null"/> 을 넘겨
        /// 제거한다. 검증되지 않은 URL 이 하위 단계(샌드박스 구성)로 흘러가지 않게 하는 것이 목적이다.
        /// </remarks>
        public CommandLineArgumentModel WithResolvedTarget(IEnumerable<string> selectedServices, string targetUrl)
            => new CommandLineArgumentModel(RawArguments,
                selectedServices: (selectedServices ?? Enumerable.Empty<string>()).ToArray(),
                enableMicrophone: EnableMicrophone,
                enableWebCam: EnableWebCam,
                enablePrinters: EnablePrinters,
                certPrivateKeyPath: CertPrivateKeyPath,
                certPublicKeyPath: CertPublicKeyPath,
                showCommandLineHelp: ShowCommandLineHelp,
                showVersionHelp: ShowVersionHelp,
                dryRun: DryRun,
                simulateFailure: SimulateFailure,
                mappedFolders: MappedFolders,
                targetUrl: targetUrl);

        public override string ToString()
        {
            var options = new List<string>();

            if (ShowCommandLineHelp)
                options.Add(ConstantStrings.TableCloth_Switch_Help);
            else if (ShowVersionHelp)
                options.Add(ConstantStrings.TableCloth_Switch_Version);
            else
            {
                if (EnableMicrophone.HasValue && EnableMicrophone.Value)
                    options.Add(ConstantStrings.TableCloth_Switch_EnableMicrophone);
                if (EnableWebCam.HasValue && EnableWebCam.Value)
                    options.Add(ConstantStrings.TableCloth_Switch_EnableCamera);
                if (EnablePrinters.HasValue && EnablePrinters.Value)
                    options.Add(ConstantStrings.TableCloth_Switch_EnablePrinter);

                if (!string.IsNullOrWhiteSpace(CertPublicKeyPath))
                {
                    options.Add(ConstantStrings.TableCloth_Switch_CertPublicKey);
                    options.Add(CertPublicKeyPath);
                }

                if (!string.IsNullOrWhiteSpace(CertPrivateKeyPath))
                {
                    options.Add(ConstantStrings.TableCloth_Switch_CertPrivateKey);
                    options.Add(CertPrivateKeyPath);
                }

                if (!string.IsNullOrWhiteSpace(TargetUrl))
                {
                    options.Add(ConstantStrings.TableCloth_Switch_TargetUrl);
                    options.Add(TargetUrl);
                }

                if (DryRun)
                    options.Add(ConstantStrings.TableCloth_Switch_DryRun);
                if (SimulateFailure)
                    options.Add(ConstantStrings.TableCloth_Switch_SimulateFailure);

                foreach (var eachSite in SelectedServices)
                    options.Add(eachSite);
            }

            return string.Join(" ", options.ToArray());
        }
    }
}
