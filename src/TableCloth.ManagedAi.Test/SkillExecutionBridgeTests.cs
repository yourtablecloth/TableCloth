using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillExecutionBridgeTests
{
    [TestMethod]
    [DataRow("missing")]
    [DataRow("disabled")]
    [DataRow("invalid")]
    public async Task IneligibleCalculatorDoesNotInstallRuntimesOrExposeATool(string state)
    {
        using var fixture = new InstallFixture();
        var run = fixture.Paths.Under("runs", "bridge-test");
        Directory.CreateDirectory(run);
        IReadOnlyList<AiSkill> skills = state == "missing" ? [] :
            [Calculator(fixture.Paths) with { Enabled = state != "disabled", IsValid = state != "invalid" }];
        var bridge = await new WindowsManagedAiSkillExecution(fixture.Paths,
            new WindowsManagedAiScriptRuntimeManager(fixture.Paths, fixture.RuntimeClients), fixture.Paths.Under("TableCloth.exe"))
            .PrepareAsync(skills, run, CancellationToken.None);
        Assert.IsNull(bridge);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    [TestMethod]
    public async Task RemovedScriptDoesNotExposeAToolOrDownloadRuntimes()
    {
        using var fixture = new InstallFixture();
        var run = fixture.Paths.Under("runs", "bridge-test");
        Directory.CreateDirectory(run);
        var calculator = InstallCalculator(fixture.Paths);
        var host = fixture.Paths.Under("TableCloth.exe");
        File.WriteAllText(host, "test host, never executed");
        var script = Path.Combine(calculator.Directory, "scripts", "calculate.py");
        File.Delete(script);
        var bridge = await
            new WindowsManagedAiSkillExecution(fixture.Paths,
                new WindowsManagedAiScriptRuntimeManager(fixture.Paths, fixture.RuntimeClients), host)
                .PrepareAsync([calculator], run, CancellationToken.None);
        Assert.IsNull(bridge);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("relative")]
    [DataRow("unknown.exe")]
    [DataRow("dotnet.exe")]
    [DataRow("python.exe")]
    [DataRow("empty")]
    [DataRow("null")]
    public async Task InvalidInjectedHostFailsWithoutFallingBackToAnotherExecutableOrDownloadingRuntimes(string state)
    {
        using var fixture = new InstallFixture();
        var run = fixture.Paths.Under("runs", "invalid-host-test");
        Directory.CreateDirectory(run);
        var calculator = InstallCalculator(fixture.Paths);
        var injectedHost = state switch
        {
            "missing" => fixture.Paths.Under("missing-application", "TableCloth.exe"),
            "relative" => "TableCloth.exe",
            "empty" => "",
            "null" => null!,
            _ => fixture.Paths.Under(state)
        };
        if (state is "unknown.exe" or "dotnet.exe" or "python.exe")
            File.WriteAllText(injectedHost, "unapproved host, never executed");
        // Even an available app host cannot repair an invalid dependency supplied by DI.
        var otherHost = fixture.Paths.Under("TableCloth.exe");
        File.WriteAllText(otherHost, "another host, never executed");

        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() =>
            new WindowsManagedAiSkillExecution(fixture.Paths,
                new WindowsManagedAiScriptRuntimeManager(fixture.Paths, fixture.RuntimeClients), injectedHost)
                .PrepareAsync([calculator], run, CancellationToken.None));

        Assert.AreEqual(AiFailureCode.SkillExecutionHostUnavailable, error.Code);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
        Assert.IsEmpty(Directory.GetFileSystemEntries(run));
        Assert.AreEqual("another host, never executed", File.ReadAllText(otherHost));
    }

    [TestMethod]
    public async Task UnavailableRuntimeStatusDoesNotRunGlobalInterpretersOrChangeTheProfile()
    {
        using var fixture = new InstallFixture();
        var status = await new WindowsManagedAiScriptRuntimeManager(fixture.Paths, fixture.RuntimeClients).GetStatusAsync(CancellationToken.None);
        Assert.IsFalse(status.Python.Installed);
        Assert.IsFalse(status.Node.Installed);
        Assert.IsNull(status.LongPaths.PythonSupported);
        Assert.IsNull(status.LongPaths.NodeSupported);
        Assert.AreEqual("RuntimeNotInstalled", status.Python.Failure);
        Assert.IsFalse(File.Exists(fixture.Paths.Under("skill-execution", "activation.json")));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Paths.Profile, "config.toml")));
    }

    [TestMethod]
    public void OrdinaryChatKeepsScriptExecutionUnavailable()
    {
        using var fixture = new InstallFixture();
        var runtime = Runtime(fixture.Paths);
        var prompt = "Calculate a discount.";
        var spec = OpenAiCodexInvocationBuilder.Chat(runtime, fixture.Paths.Root, prompt, new());

        Assert.AreEqual(prompt, spec.Input);
        Assert.IsFalse(spec.StartInfo.ArgumentList.Any(value => value.Contains("mcp_servers", StringComparison.Ordinal)));
        Assert.Contains("shell_tool = false", WindowsManagedAiProfile.Configuration);
        Assert.Contains("unified_exec = false", WindowsManagedAiProfile.Configuration);
        AssertPolicy(spec);
    }

    [TestMethod]
    public void RegisteredCalculatorUsesMcpWithoutEnablingShellExecution()
    {
        using var fixture = new InstallFixture();
        var runtime = Runtime(fixture.Paths);
        var host = fixture.Paths.Under("host with spaces", "TableCloth.exe");
        var manifest = fixture.Paths.Under("run with spaces", "bridge-manifest.json");
        var bridge = new AiSkillExecutionBridge(host, ["--skill-mcp", manifest],
            fixture.Paths.Root, ["tablecloth-calculators"]);

        var spec = OpenAiCodexInvocationBuilder.Chat(runtime, fixture.Paths.Root, "Calculate.", new(),
            skillExecution: bridge);
        var arguments = spec.StartInfo.ArgumentList.ToArray();
        var overrides = arguments.Where((value, index) => index > 0 && arguments[index - 1] == "-c").ToArray();

        Assert.IsTrue(overrides.Any(value => value.Contains("mcp_servers.", StringComparison.Ordinal)));
        Assert.IsTrue(overrides.Any(value => value.Contains("tablecloth-calculators", StringComparison.Ordinal)));
        var registration = overrides.Single(value => value.StartsWith("mcp_servers.", StringComparison.Ordinal));
        Assert.Contains("command=" + JsonSerializer.Serialize(host), registration);
        Assert.Contains("args=[\"--skill-mcp\"," + JsonSerializer.Serialize(manifest) + "]", registration);
        Assert.Contains("enabled_tools=[\"tablecloth-calculators\"]", registration);
        Assert.Contains("default_tools_approval_mode=\"prompt\"", registration);
        Assert.IsFalse(overrides.Any(value => value.Contains("shell_tool=true", StringComparison.Ordinal)));
        Assert.IsFalse(overrides.Any(value => value.Contains("unified_exec=true", StringComparison.Ordinal)));
        Assert.IsFalse(overrides.Any(value => value.Contains("enabled_tools", StringComparison.Ordinal) &&
            value.Contains("*", StringComparison.Ordinal)));
        Assert.Contains("Calculate.", spec.Input!);
        Assert.Contains("explicitly listed tablecloth_skills MCP tools", spec.Input!);
        Assert.Contains("Never say you used a skill or executed a script without a successful tool result", spec.Input!);
        AssertPolicy(spec);
    }

    [TestMethod]
    [DataRow("relative-command")]
    [DataRow("relative-manifest")]
    [DataRow("other-command")]
    [DataRow("slash-tool")]
    [DataRow("control-tool")]
    [DataRow("duplicate-tool")]
    [DataRow("empty-tools")]
    [DataRow("relative-working-directory")]
    public void MalformedBridgeCommandsAndToolNamesAreRejected(string change)
    {
        using var fixture = new InstallFixture();
        var command = change == "relative-command" ? "TableCloth.exe" : fixture.Paths.Under("TableCloth.exe");
        var manifest = change == "relative-manifest" ? "manifest.json" : fixture.Paths.Under("manifest.json");
        IReadOnlyList<string> tools = change switch
        {
            "slash-tool" => ["tablecloth-calculators", "arbitrary/tool"],
            "control-tool" => ["tool\nname"],
            "duplicate-tool" => ["tool", "TOOL"],
            "empty-tools" => [],
            _ => ["tablecloth-calculators"]
        };
        var bridge = new AiSkillExecutionBridge(command,
            [change == "other-command" ? "--run-arbitrary-script" : "--skill-mcp", manifest],
            change == "relative-working-directory" ? "relative" : fixture.Paths.Root, tools);

        Assert.AreEqual(AiFailureCode.BlockedByPolicy,
            Assert.ThrowsExactly<ManagedAiException>(() => OpenAiCodexInvocationBuilder.Chat(
                Runtime(fixture.Paths), fixture.Paths.Root, "Calculate.", new(), skillExecution: bridge)).Code);
    }

    [TestMethod]
    public void MultipleExplicitSkillToolsAreRegisteredWithoutShellOrWildcardApproval()
    {
        using var fixture = new InstallFixture();
        string[] tools = ["bok-ecos-stats", "kosis-stats", "tablecloth-calculators", "local-node-skill"];
        var bridge = new AiSkillExecutionBridge(fixture.Paths.Under("TableCloth.exe"),
            ["--skill-mcp", fixture.Paths.Under("manifest.json")], fixture.Paths.Root, tools);
        var spec = OpenAiCodexInvocationBuilder.Chat(Runtime(fixture.Paths), fixture.Paths.Root,
            "Run an installed skill.", new(), skillExecution: bridge);
        var registration = spec.StartInfo.ArgumentList.Single(value => value.StartsWith("mcp_servers.", StringComparison.Ordinal));

        Assert.Contains("enabled_tools=[" + string.Join(',', tools.Select(tool => JsonSerializer.Serialize(tool))) + "]", registration);
        foreach (var tool in tools) Assert.Contains(JsonSerializer.Serialize(tool) + "={approval_mode=\"approve\"}", registration);
        Assert.DoesNotContain("*", registration);
        Assert.DoesNotContain("shell_tool=true", registration);
        Assert.DoesNotContain("unified_exec=true", registration);
        Assert.Contains("Python or Node.js script", spec.Input!);
        Assert.Contains("worker has no network capability", spec.Input!);
        AssertPolicy(spec);
    }

    private static void AssertPolicy(ProcessRunSpec spec)
    {
        var arguments = spec.StartInfo.ArgumentList.ToArray();
        var sandbox = Array.IndexOf(arguments, "--sandbox");
        var approval = Array.IndexOf(arguments, "--ask-for-approval");
        Assert.AreNotEqual(-1, sandbox);
        Assert.AreEqual("read-only", arguments[sandbox + 1]);
        Assert.AreNotEqual(-1, approval);
        Assert.AreEqual("never", arguments[approval + 1]);
        Assert.Contains("--strict-config", arguments);
        Assert.IsFalse(spec.StartInfo.UseShellExecute);
    }

    private static ManagedRuntime Runtime(ManagedAiPaths paths) => new(
        new("0.156.1", "x86_64-pc-windows-msvc", new string('a', 64)), paths.Root,
        paths.Under("codex.exe"), paths.Profile, DateTimeOffset.UtcNow);

    private static AiSkill Calculator(ManagedAiPaths paths) => new("tablecloth-calculators", "Calculators",
        "Reviewed local calculators", paths.Under("profiles", "openai-codex", "codex-home", "skills", "tablecloth-calculators"), true);

    private static AiSkill InstallCalculator(ManagedAiPaths paths)
    {
        var calculator = Calculator(paths);
        var scripts = Path.Combine(calculator.Directory, "scripts");
        Directory.CreateDirectory(scripts);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "tools", "appcontainer-skill-poc",
            "skills", "tablecloth-calculators", "scripts", "calculate.py"))) repository = repository.Parent;
        Assert.IsNotNull(repository, "The pinned upstream calculator fixture must be present in the repository.");
        File.Copy(Path.Combine(repository.FullName, "tools", "appcontainer-skill-poc", "skills", "tablecloth-calculators",
            "scripts", "calculate.py"), Path.Combine(scripts, "calculate.py"));
        return calculator;
    }

}
