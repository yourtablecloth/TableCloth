using Spork.ViewModels;
using System.Collections.Generic;

namespace Spork.Steps
{
    public interface IStepsComposer
    {
        /// <summary>
        /// 현재 명령줄 인자(<see cref="Components.ICommandLineArguments"/>)의 SelectedServices를 기준으로 설치 단계를 구성합니다.
        /// </summary>
        IEnumerable<StepItemViewModel> ComposeSteps();

        /// <summary>
        /// 명령줄과 무관하게 주어진 사이트 ID 목록만으로 설치 단계를 구성합니다.
        /// 샌드박스 내부에서 사용자가 카탈로그 UI로 사이트를 선택한 경우 사용됩니다.
        /// </summary>
        /// <param name="forceReinstall">
        /// <see langword="true"/>이면 InstallRecord 의 fingerprint 일치 여부를 무시하고 모든 패키지를
        /// 다시 포함시킨다. 사용자가 카탈로그 카드의 배지를 통해 명시적으로 재설치를 요청한 경우.
        /// </param>
        IEnumerable<StepItemViewModel> ComposeStepsForSites(IEnumerable<string> selectedServiceIds, bool forceReinstall = false);
    }
}