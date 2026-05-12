using Spork.ViewModels;
using System.Collections.Generic;
using TableCloth.Models.Catalog;

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
        IEnumerable<StepItemViewModel> ComposeStepsForSites(IEnumerable<string> selectedServiceIds);

        /// <summary>
        /// 카탈로그의 보조 프로그램(<see cref="CatalogCompanion"/>) 하나에 대한 설치 단계를 구성합니다.
        /// 사이트 설치보다 단순한 파이프라인(prereq + 단일 PackageInstall + Edge 재시작)으로 구성됩니다.
        /// </summary>
        IEnumerable<StepItemViewModel> ComposeStepsForCompanion(CatalogCompanion companion);
    }
}