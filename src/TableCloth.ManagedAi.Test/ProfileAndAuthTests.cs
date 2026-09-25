using System.Security.AccessControl;
using System.Security.Principal;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ProfileAndAuthTests
{
    [TestMethod]
    public void ProfileIsPrivateAndDoesNotChangeParentEnvironment()
    {
        using var fixture = new InstallFixture();
        var pathBefore = Environment.GetEnvironmentVariable("PATH");
        var codexBefore = Environment.GetEnvironmentVariable("CODEX_HOME");
        var profile = new WindowsManagedAiProfile(fixture.Paths);
        profile.Prepare();
        profile.Prepare();
        Assert.AreEqual(pathBefore, Environment.GetEnvironmentVariable("PATH"));
        Assert.AreEqual(codexBefore, Environment.GetEnvironmentVariable("CODEX_HOME"));
        Assert.AreEqual(WindowsManagedAiProfile.Configuration, File.ReadAllText(Path.Combine(fixture.Paths.Profile, "config.toml")));
        Assert.AreEqual(WindowsManagedAiProfile.EmptySkillConfiguration, File.ReadAllText(fixture.Paths.SkillConfiguration));
        Assert.IsTrue(Directory.Exists(fixture.Paths.Skills));
        Assert.IsFalse(fixture.Paths.Skills.StartsWith(fixture.Paths.RuntimeRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
        File.WriteAllText(fixture.Paths.SkillConfiguration, "unsafe skill override");
        profile.Prepare();
        Assert.AreEqual(WindowsManagedAiProfile.EmptySkillConfiguration, File.ReadAllText(fixture.Paths.SkillConfiguration));
        var acl = new DirectoryInfo(fixture.Paths.Profile).GetAccessControl();
        Assert.IsTrue(acl.AreAccessRulesProtected);
        using var identity = WindowsIdentity.GetCurrent();
        var allowed = new HashSet<string> { identity.User!.Value, "S-1-5-18", "S-1-5-32-544" };
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            Assert.Contains(rule.IdentityReference.Value, allowed);
        File.WriteAllText(Path.Combine(fixture.Paths.Profile, "config.toml"), "unsafe configuration");
        Assert.AreEqual(AiFailureCode.BlockedByPolicy, Assert.ThrowsExactly<ManagedAiException>(profile.Prepare).Code);
    }

    [TestMethod]
    [DataRow("Logged in using ChatGPT", 0, true)]
    [DataRow("Logged in using an API key", 0, false)]
    [DataRow("Not logged in", 1, false)]
    [DataRow("ChatGPT token content or unexpected format", 0, false)]
    public async Task AuthenticationRequiresChatGptAndSuccessfulExit(string status, int code, bool expected)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new AuthRunner(status, code);
        var auth = new OpenAiCodexAuthManager(fixture.Paths, fixture.Installer, new WindowsManagedAiProfile(fixture.Paths), runner);
        Assert.AreEqual(expected, await auth.IsLoggedInAsync(CancellationToken.None));
        CollectionAssert.AreEqual(new[] { "login", "status" }, runner.Arguments!);
        Assert.AreEqual(fixture.Paths.Profile, runner.CodexHome);
    }

    [TestMethod]
    public void DiagnosticsContainOnlyAllowedFieldsAndExpireOldFiles()
    {
        using var fixture = new InstallFixture();
        Directory.CreateDirectory(fixture.Paths.Logs);
        var oldFile = Path.Combine(fixture.Paths.Logs, "diagnostic-20000101.jsonl");
        File.WriteAllText(oldFile, "expired");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-8));
        new DiagnosticsSink(fixture.Paths).Write(Guid.NewGuid(), "1.2.3", 123, 4, 1, 500, 0, null, false);
        Assert.IsFalse(File.Exists(oldFile));
        var content = File.ReadAllText(Directory.GetFiles(fixture.Paths.Logs).Single());
        using var json = System.Text.Json.JsonDocument.Parse(content);
        var keys = json.RootElement.EnumerateObject().Select(x => x.Name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Timestamp", "RunId", "Version", "DurationMs", "Events", "SearchCalls",
            "StdoutBytes", "StderrBytes", "Outcome" }, keys);
    }

    private sealed class AuthRunner(string status, int exitCode) : IJsonlProcessRunner
    {
        public string[]? Arguments;
        public string? CodexHome;
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken cancellationToken)
        {
            Arguments = spec.StartInfo.ArgumentList.ToArray();
            CodexHome = spec.StartInfo.Environment["CODEX_HOME"];
            stderr?.Invoke(status);
            return Task.FromResult(new ProcessOutcome(exitCode, 0, status.Length));
        }
    }
}
