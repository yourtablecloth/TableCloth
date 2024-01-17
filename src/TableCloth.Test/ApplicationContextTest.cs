using System.Windows.Threading;

namespace TableCloth.Test;

// https://stackoverflow.com/questions/67647877/problems-running-multiple-xunit-tests-under-sta-thread-wpf
public abstract class ApplicationContextTest : IDisposable
{
    protected ApplicationContextTest() => ApplicationState.CreateNew();

    public void Dispose() => ApplicationState.Shutdown();

    protected async Task AwaitDispatcher() => await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
}
