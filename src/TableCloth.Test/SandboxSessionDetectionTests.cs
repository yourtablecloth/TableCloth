namespace TableCloth.Test;

[TestClass]
public sealed class SandboxSessionDetectionTests
{
    [TestMethod]
    public void BackgroundServerIsNotMistakenForASession()
    {
        Assert.IsFalse(Helpers.IsWindowsSandboxSessionProcessName("WindowsSandboxServer"));
        Assert.IsFalse(Helpers.IsWindowsSandboxSessionProcessName("WindowsSandboxService"));
        Assert.IsTrue(Helpers.IsWindowsSandboxSessionProcessName("WindowsSandbox"));
        Assert.IsTrue(Helpers.IsWindowsSandboxSessionProcessName("WindowsSandboxClient"));
        Assert.IsTrue(Helpers.IsWindowsSandboxSessionProcessName("WindowsSandboxRemoteSession"));
    }
}
