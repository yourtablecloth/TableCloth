using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TableCloth.Test.MarkdownTestAppBuilder))]
[assembly: AvaloniaTestIsolation(AvaloniaTestIsolationLevel.PerTest)]

namespace TableCloth.Test;

internal sealed class HeadlessTestSession
{
    // Follow Avalonia NUnit's process-owned cached session; PerTest Dispatch still cleans up each application scope.
    // https://github.com/AvaloniaUI/Avalonia/blob/11.3.18/src/Headless/Avalonia.Headless.NUnit/AvaloniaTest.cs#L24-L29
    // Do not expose IDisposable: Avalonia 11.3.18 can capture a null worker task in StartNew (upstream #22222).
    // https://github.com/AvaloniaUI/Avalonia/pull/22222
    public static HeadlessTestSession Shared { get; } = new();

    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HeadlessTestSession).Assembly);

    private HeadlessTestSession() { }

    public Task Dispatch(Action action, CancellationToken cancellationToken)
        => _session.Dispatch(action, cancellationToken);

    public Task<TResult> Dispatch<TResult>(Func<TResult> action, CancellationToken cancellationToken)
        => _session.Dispatch(action, cancellationToken);

    public Task<TResult> Dispatch<TResult>(Func<Task<TResult>> action, CancellationToken cancellationToken)
        => _session.Dispatch(action, cancellationToken);
}
