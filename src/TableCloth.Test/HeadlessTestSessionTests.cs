using Avalonia;
using Avalonia.Headless;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class HeadlessTestSessionTests
{
    [TestMethod]
    public async Task CachedSessionCreatesAnIsolatedApplicationForEachDispatch()
    {
        var assembly = typeof(HeadlessTestSession).Assembly;
        Assert.AreSame(HeadlessUnitTestSession.GetOrStartForAssembly(assembly),
            HeadlessUnitTestSession.GetOrStartForAssembly(assembly));
        var session = HeadlessTestSession.Shared;
        Assert.AreSame(session, HeadlessTestSession.Shared);
        const string resourceKey = "headless-session-isolation-marker";

        var firstApplication = await session.Dispatch(() =>
        {
            var application = Application.Current!;
            Assert.IsInstanceOfType<TableClothApplication>(application);
            application.Resources[resourceKey] = new object();
            return application;
        }, CancellationToken.None);

        await session.Dispatch(() =>
        {
            var application = Application.Current!;
            Assert.IsInstanceOfType<TableClothApplication>(application);
            Assert.AreNotSame(firstApplication, application);
            Assert.IsFalse(application.Resources.ContainsKey(resourceKey));
        }, CancellationToken.None);
    }

    [TestMethod]
    public async Task AsyncFailuresPropagateAndLeaveTheSessionUsable()
    {
        var session = HeadlessTestSession.Shared;
        var assertion = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => session.Dispatch<bool>(async () =>
        {
            await Task.Yield();
            Assert.Fail("headless assertion marker");
            return false;
        }, CancellationToken.None));
        Assert.Contains("headless assertion marker", assertion.Message);

        var marker = new InvalidOperationException("headless exception marker");
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.Dispatch<bool>(async () =>
        {
            await Task.Yield();
            throw marker;
        }, CancellationToken.None));
        Assert.AreSame(marker, exception);

        var completed = false;
        await session.Dispatch(() => { completed = true; }, CancellationToken.None);
        Assert.IsTrue(completed);
    }
}
