using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillTests
{
    [TestMethod]
    public void NativeExchangeReadsMetadataWithoutStartingATurn()
    {
        var exchange = new CodexSkillListExchange();
        var next = exchange.Accept("{\"id\":1,\"result\":{}}")!.Text!;
        Assert.Contains("initialized", next);
        Assert.Contains("skills/list", next);
        Assert.DoesNotContain("thread/start", next);
        Assert.IsNull(exchange.Accept("{\"method\":\"skills/changed\",\"params\":{}}"));
        var path = Path.Combine(Path.GetFullPath("."), "skill", "SKILL.md");
        Assert.IsTrue(exchange.Accept(Response(path))!.Close);
        var skill = exchange.Complete(0).Skills.Single();
        Assert.AreEqual("fixture-skill", skill.Name);
        Assert.AreEqual(path, skill.Path);
        Assert.AreEqual(AiFailureCode.SkillIsolationFailed,
            Assert.ThrowsExactly<ManagedAiException>(() => exchange.Complete(1)).Code);
    }

    [TestMethod]
    public async Task ManagedCatalogDisablesExternalSkillsAndPersistsUserChoiceOutsideRuntime()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var managedDirectory = Path.Combine(fixture.Paths.Skills, "owned-skill");
        Directory.CreateDirectory(managedDirectory);
        File.WriteAllText(Path.Combine(managedDirectory, "SKILL.md"), "---\nname: owned-skill\ndescription: Owned skill.\n---\n");
        var runner = new SkillRunner(fixture.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(runner.ExternalManifest)!);
        File.WriteAllText(runner.ExternalManifest, "external fixture");
        var profile = new SkillProfile(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);

        var skills = await manager.ListAsync(CancellationToken.None);
        Assert.HasCount(3, skills);
        Assert.IsTrue(skills.Single(x => x.Id == "owned-skill").Enabled);
        Assert.IsTrue(skills.Single(x => x.Id == "tablecloth-certificate-expiry").Enabled);
        Assert.IsTrue(skills.Single(x => x.Id == "tablecloth-windows-sandbox").Enabled);
        Assert.IsFalse(Directory.Exists(runner.Invocation!.StartInfo.WorkingDirectory));
        CollectionAssert.AreEqual(new[] { "--strict-config", "app-server", "--listen", "stdio://" },
            runner.Invocation.StartInfo.ArgumentList.ToArray());
        var overlay = File.ReadAllText(fixture.Paths.SkillConfiguration);
        Assert.Contains(Escaped(runner.ExternalManifest), overlay);
        Assert.DoesNotContain(Escaped(runner.ManagedManifest), overlay);
        Assert.DoesNotContain(Escaped(runner.SystemManifest), overlay);

        skills = await manager.SetEnabledAsync("owned-skill", false, CancellationToken.None);
        Assert.IsFalse(skills.Single(x => x.Id == "owned-skill").Enabled);
        overlay = File.ReadAllText(fixture.Paths.SkillConfiguration);
        Assert.Contains(Escaped(runner.ExternalManifest), overlay);
        Assert.Contains(Escaped(runner.ManagedManifest), overlay);
        Assert.IsTrue(File.Exists(fixture.Paths.SkillState));

        fixture.Paths.DeleteOwnedDirectory(fixture.Paths.RuntimeRoot);
        Assert.IsTrue(File.Exists(Path.Combine(managedDirectory, "SKILL.md")));
        Assert.IsTrue(File.Exists(fixture.Paths.SkillState));
        Assert.IsFalse((await manager.ListAsync(CancellationToken.None)).Single(x => x.Id == "owned-skill").Enabled);
        Assert.HasCount(2, await manager.RemoveAsync("owned-skill", CancellationToken.None));
        Assert.IsFalse(Directory.Exists(managedDirectory));
        Assert.IsTrue(File.Exists(runner.ExternalManifest));
    }

    [TestMethod]
    public async Task ImportCopiesOneBoundedSkillAndRejectsDuplicate()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var sourceRoot = Path.Combine(Path.GetTempPath(), "tc-skill-source-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(sourceRoot, "imported-skill");
        Directory.CreateDirectory(Path.Combine(source, "references"));
        File.WriteAllText(Path.Combine(source, "SKILL.md"), "---\nname: imported-skill\ndescription: Imported skill.\n---\n");
        File.WriteAllText(Path.Combine(source, "references", "guide.md"), "guide");
        try
        {
            var profile = new SkillProfile(fixture.Paths);
            var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, new SkillRunner(fixture.Paths));
            var imported = await manager.ImportAsync(source, CancellationToken.None);
            Assert.AreEqual("imported-skill", imported.Single(x => x.Id == "imported-skill").Id);
            Assert.AreEqual("guide", File.ReadAllText(Path.Combine(fixture.Paths.Skills, "imported-skill", "references", "guide.md")));
            var duplicate = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.ImportAsync(source, CancellationToken.None));
            Assert.AreEqual(AiFailureCode.SkillAlreadyInstalled, duplicate.Code);
            Assert.HasCount(2, await manager.RemoveAsync("imported-skill", CancellationToken.None));
            Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Paths.Skills, "imported-skill")));
            Assert.HasCount(1, await manager.RemoveAsync("tablecloth-certificate-expiry", CancellationToken.None));
            Assert.HasCount(0, await manager.RemoveAsync("tablecloth-windows-sandbox", CancellationToken.None));
            Assert.HasCount(0, await manager.ListAsync(CancellationToken.None));
        }
        finally { if (Directory.Exists(sourceRoot)) Directory.Delete(sourceRoot, recursive: true); }
    }

    [TestMethod]
    public async Task InvalidLocalSkillRemainsManageableWithoutAllowingChatOrReenable()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        CreateSkill(fixture.Paths.Skills, "normal-skill");
        CreateSkill(fixture.Paths.Skills, "broken-skill");
        var runner = new SkillRunner(fixture.Paths);
        runner.InvalidIds.Add("broken-skill");
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), runner);

        var skills = await manager.ListAsync(CancellationToken.None);
        Assert.HasCount(4, skills);
        Assert.AreEqual("Managed skill.", skills.Single(x => x.Id == "normal-skill").Description);
        Assert.IsTrue(skills.Single(x => x.Id == "normal-skill").IsValid);
        var broken = skills.Single(x => x.Id == "broken-skill");
        Assert.IsFalse(broken.IsValid);
        Assert.IsTrue(broken.Enabled);
        Assert.AreEqual("broken-skill", broken.Name);
        Assert.DoesNotContain(SkillRunner.RawError, broken.Description);
        Assert.Contains(Escaped(Path.Combine(broken.Directory, "SKILL.md")), File.ReadAllText(fixture.Paths.SkillConfiguration));

        Assert.AreEqual(AiFailureCode.SkillInvalid,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.SetEnabledAsync("broken-skill", true, CancellationToken.None))).Code);
        Assert.IsFalse(File.Exists(fixture.Paths.SkillState));
        skills = await manager.SetEnabledAsync("broken-skill", false, CancellationToken.None);
        Assert.IsFalse(skills.Single(x => x.Id == "broken-skill").Enabled);
        var state = File.ReadAllText(fixture.Paths.SkillState);
        Assert.AreEqual(AiFailureCode.SkillInvalid,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.SetEnabledAsync("broken-skill", true, CancellationToken.None))).Code);
        Assert.AreEqual(state, File.ReadAllText(fixture.Paths.SkillState));

        var runtime = await fixture.Installer.GetActiveAsync(CancellationToken.None);
        Assert.IsNotNull(runtime);
        var working = fixture.Paths.Under("runs", "strict-skill-check");
        Directory.CreateDirectory(working);
        using (fixture.Paths.AcquireOperation())
            Assert.AreEqual(AiFailureCode.SkillInvalid,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.SynchronizeAsync(runtime, working, CancellationToken.None))).Code);

        skills = await manager.RemoveAsync("broken-skill", CancellationToken.None);
        Assert.HasCount(3, skills);
        Assert.IsTrue(skills.All(x => x.IsValid));
        Assert.IsFalse(Directory.Exists(broken.Directory));
        using (fixture.Paths.AcquireOperation())
            Assert.HasCount(3, await manager.SynchronizeAsync(runtime, working, CancellationToken.None));
    }

    [TestMethod]
    public async Task ExistingInvalidSkillDoesNotBlockValidImportAndInvalidNewSkillRollsBack()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var existing = CreateSkill(fixture.Paths.Skills, "existing-broken");
        var sourceRoot = fixture.Paths.Under("cache", "import-fixture");
        var goodSource = CreateSkill(sourceRoot, "good-import");
        var badSource = CreateSkill(sourceRoot, "bad-import");
        var runner = new SkillRunner(fixture.Paths);
        runner.InvalidIds.UnionWith(["existing-broken", "bad-import"]);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), runner);

        var skills = await manager.ImportAsync(goodSource, CancellationToken.None);
        Assert.IsTrue(skills.Single(x => x.Id == "good-import").IsValid);
        Assert.IsFalse(skills.Single(x => x.Id == "existing-broken").IsValid);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.Paths.Skills, "good-import", "SKILL.md")));
        Assert.AreEqual(AiFailureCode.SkillInvalid,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.ImportAsync(badSource, CancellationToken.None))).Code);
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Paths.Skills, "bad-import")));
        Assert.IsTrue(Directory.Exists(existing));
        Assert.IsTrue(Directory.Exists(badSource));
        Assert.IsTrue((await manager.ListAsync(CancellationToken.None)).Single(x => x.Id == "good-import").IsValid);
    }

    private static string CreateSkill(string root, string id)
    {
        var directory = Path.Combine(root, id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "SKILL.md"), $"---\nname: {id}\ndescription: Skill fixture.\n---\n");
        return directory;
    }

    private static string Response(string path) => JsonSerializer.Serialize(new
    {
        id = 2,
        result = new
        {
            data = new[] { new { cwd = Path.GetFullPath("."), skills = new[] { new { name = "fixture-skill",
                description = "Fixture skill.", path, scope = "user", enabled = true } }, errors = Array.Empty<object>() } }
        }
    });

    private static string Escaped(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private sealed class SkillProfile(ManagedAiPaths paths) : IManagedAiProfile
    {
        public void Prepare()
        {
            Directory.CreateDirectory(paths.Skills);
            File.WriteAllText(paths.SkillConfiguration, WindowsManagedAiProfile.EmptySkillConfiguration);
        }
    }

    private sealed class SkillRunner(ManagedAiPaths paths) : IJsonlProcessRunner
    {
        public const string RawError = "Provider detail containing private document text.";
        public ProcessRunSpec? Invocation;
        public HashSet<string> InvalidIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string ExternalManifest => Path.Combine(paths.Root, "external-skills", "outside", "SKILL.md");
        public string ManagedManifest => Path.Combine(paths.Skills, "owned-skill", "SKILL.md");
        public string SystemManifest => Path.Combine(paths.Skills, ".system", "built-in", "SKILL.md");

        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr,
            CancellationToken cancellationToken)
        {
            Invocation = spec;
            Assert.AreEqual(CodexSkillListExchange.Initialize, spec.Input);
            Assert.Contains("skills/list", spec.Respond!("{\"id\":1,\"result\":{}}")!.Text!);
            var directories = Directory.Exists(paths.Skills)
                ? Directory.GetDirectories(paths.Skills).Where(x => !Path.GetFileName(x).StartsWith('.'))
                    .ToArray()
                : [];
            var managed = directories.Where(x => !InvalidIds.Contains(Path.GetFileName(x)))
                .Select(x => new { name = Path.GetFileName(x), description = "Managed skill.",
                    path = Path.Combine(x, "SKILL.md"), scope = "user", enabled = true }).ToArray();
            var errors = directories.Where(x => InvalidIds.Contains(Path.GetFileName(x)))
                .Select(x => new { path = Path.Combine(x, "SKILL.md"), message = RawError }).ToArray();
            var all = managed.Cast<object>().Concat([
                new { name = "outside", description = "External skill.", path = ExternalManifest, scope = "user", enabled = true },
                new { name = "built-in", description = "System skill.", path = SystemManifest, scope = "system", enabled = true }
            ]).ToArray();
            var response = JsonSerializer.Serialize(new { id = 2, result = new
            { data = new[] { new { cwd = spec.StartInfo.WorkingDirectory, skills = all, errors } } } });
            Assert.IsTrue(spec.Respond(response)!.Close);
            return Task.FromResult(new ProcessOutcome(0, response.Length, 0));
        }
    }
}
