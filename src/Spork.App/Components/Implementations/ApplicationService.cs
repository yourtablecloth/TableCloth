using System;
using System.Linq;
using System.Windows;
using TableCloth;

namespace Spork.Components.Implementations
{
    public sealed class ApplicationService : IApplicationService
    {
        public ApplicationService(IVisualThemeManager visualThemeManager)
        {
            _visualThemeManager = visualThemeManager;
        }

        private readonly IVisualThemeManager _visualThemeManager;

        // Application은 DI로 받지 않고 WPF의 표준 정적 참조를 사용한다. DI에서 Application을
        // 받으면 SporkApplication 팩토리가 다시 호출되면서 순환 의존이 발생하여
        // "Cannot create more than one System.Windows.Application instance"가 던져진다.
        // Application.Current는 SporkApplication 베이스 ctor에서 이미 채워져 있다.
        private static Application Application
            => System.Windows.Application.Current
               ?? throw new InvalidOperationException("Application.Current is not yet available.");

        public object DispatchInvoke(Delegate @delegate, object[] arguments)
        {
            var dispatcher = Application.Dispatcher;

            if (dispatcher == null)
                TableClothAppException.Throw("Dispatcher cannot be null reference.");

            return dispatcher.Invoke(@delegate, arguments);
        }

        // https://stackoverflow.com/questions/2038879/refer-to-active-window-in-wpf
        public Window GetActiveWindow()
            => DispatchInvoke(
                new Func<Application, Window>((Application _application) => _application.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive)),
                new object[] { Application }) as Window;

        public Window GetMainWindow()
            => DispatchInvoke(
                new Func<Application, Window>((Application _application) => _application.MainWindow),
                new object[] { Application }) as Window;

        public void ApplyCosmeticChange(Window targetWindow)
            => DispatchInvoke(
                new Action<Window, IVisualThemeManager>((Window _targetWindow, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_targetWindow)),
                new object[] { targetWindow, _visualThemeManager });

        public void ApplyCosmeticChangeToMainWindow()
            => DispatchInvoke(
                new Action<Application, IVisualThemeManager>((Application _application, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow)),
                new object[] { Application, _visualThemeManager });

        public void Shutdown(int exitCode = default)
            => DispatchInvoke(
                new Action<Application, int>((Application _application, int _exitCode) => _application.Shutdown(_exitCode)),
                new object[] { Application, exitCode });
    }
}
