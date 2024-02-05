using System;
using System.Linq;
using System.Windows;
using TableCloth;

namespace Hostess.Components.Implementations
{
    public sealed class ApplicationService : IApplicationService
    {
        public ApplicationService(
            Application application,
            IVisualThemeManager visualThemeManager)
        {
            _application = application;
            _visualThemeManager = visualThemeManager;
        }

        private readonly Application _application;
        private readonly IVisualThemeManager _visualThemeManager;

        public object DispatchInvoke(Delegate @delegate, object[] arguments)
        {
            var dispatcher = _application?.Dispatcher;

            if (dispatcher == null)
                TableClothAppException.Throw("Dispatcher cannot be null reference.");

            return dispatcher.Invoke(@delegate, arguments);
        }

        // https://stackoverflow.com/questions/2038879/refer-to-active-window-in-wpf
        public Window GetActiveWindow()
            => DispatchInvoke(
                new Func<Application, Window>((Application _application) => _application.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive)),
                new object[] { _application }) as Window;

        public Window GetMainWindow()
            => DispatchInvoke(
                new Func<Application, Window>((Application _application) => _application.MainWindow),
                new object[] { _application }) as Window;

        public void ApplyCosmeticChange(Window targetWindow)
            => DispatchInvoke(
                new Action<Window, IVisualThemeManager>((Window _targetWindow, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_targetWindow)),
                new object[] { targetWindow, _visualThemeManager });

        public void ApplyCosmeticChangeToMainWindow()
            => DispatchInvoke(
                new Action<Application, IVisualThemeManager>((Application _application, IVisualThemeManager _visualThemeManager) => _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow)),
                new object[] { _application, _visualThemeManager });

        public void Shutdown(int exitCode = default)
            => DispatchInvoke(
                new Action<Application, int>((Application _application, int _exitCode) => _application.Shutdown(_exitCode)),
                new object[] { _application, exitCode });
    }
}
