using System;
using System.Windows;

namespace Spork.Components
{
    public interface IApplicationService
    {
        Window GetMainWindow();

        Window GetActiveWindow();

        object DispatchInvoke(Delegate @delegate, object[] arguments);

        void ApplyCosmeticChange(Window targetWindow);

        void ApplyCosmeticChangeToMainWindow();

        void Shutdown(int exitCode = default);
    }

}
