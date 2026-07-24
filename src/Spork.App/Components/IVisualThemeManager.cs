using System.Windows;

namespace Spork.Components
{
    public interface IVisualThemeManager
    {
        /// <summary>지정 창에 OS 라이트/다크 테마를 적용하고 변경 훅을 건다.</summary>
        void ApplyAutoThemeChange(Window targetWindow);

        /// <summary>
        /// 현재 메인 창을 대상으로 테마를 적용한다(내부에서 창을 해석). ViewModel이 WPF <c>Window</c>나
        /// <c>Application.Current</c>를 직접 참조하지 않고 테마를 적용할 수 있게 하는 UI 중립 진입점. (이슈 #296)
        /// </summary>
        void ApplyAutoThemeChange();
    }
}