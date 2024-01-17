using System.Reflection;
using System.Windows.Threading;

namespace TableCloth.Test;

// https://stackoverflow.com/questions/67647877/problems-running-multiple-xunit-tests-under-sta-thread-wpf
public static class ApplicationState
{
    private static bool hasApplicationPreviouslyInitialized;

    public static void CreateNew()
    {
        Shutdown();
        if (Application.Current == null)
        {
            new Application()
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            if (!hasApplicationPreviouslyInitialized)
            {
                hasApplicationPreviouslyInitialized = true;
            }
            else
            {
                var pre = typeof(Application);
                var clear = pre.GetMethod("ApplicationInit", BindingFlags.Static | BindingFlags.NonPublic);
                clear.Invoke(null, null);
            }
        }
    }

    public static void Shutdown()
    {
        if (Application.Current != null)
        {
            Application.Current.Resources.MergedDictionaries.Clear();

            Application.Current.Shutdown(0);
            var result = Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Result;

            var appCreated = typeof(Application).GetField("_appCreatedInThisAppDomain",
                BindingFlags.Static |
                BindingFlags.NonPublic);
            appCreated.SetValue(null, false);

        }
    }
}
