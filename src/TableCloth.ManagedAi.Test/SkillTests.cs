using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillTests
{
    [TestMethod]
    public async Task NewProfileStaysEmptyBeforeAndAfterRuntimeInstallationAndChatSynchronization()
    {
        using var fixture = new InstallFixture();
        var runner = new SkillRunner(fixture.Paths);
        var profile = new SkillProfile(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);
        Assert.IsEmpty(await manager.ListAsync(CancellationToken.None));
        Assert.IsNull(runner.Invocation);

        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        Assert.IsEmpty(await manager.ListAsync(CancellationToken.None));
        Assert.IsNotNull(runner.Invocation);
        var runtime = await fixture.Installer.GetActiveAsync(CancellationToken.None);
        Assert.IsNotNull(runtime);
        var working = fixture.Paths.Under("runs", "empty-profile");
        Directory.CreateDirectory(working);
        using (fixture.Paths.AcquireOperation())
            Assert.IsEmpty(await manager.SynchronizeAsync(runtime, working, CancellationToken.None));

        manager = new(fixture.Paths, fixture.Installer, profile, runner);
        Assert.IsEmpty(await manager.ListAsync(CancellationToken.None));
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(fixture.Paths.Skills));
        Assert.IsFalse(File.Exists(fixture.Paths.Under("profiles", "openai-codex", "certificate-expiry-skill.installed")));
        Assert.IsFalse(File.Exists(fixture.Paths.Under("profiles", "openai-codex", "windows-sandbox-skill.installed")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExistingFormerDefaultSkillsAndTheirDisabledPreferenceArePreserved(bool legacyMarkers)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        foreach (var id in new[] { "tablecloth-certificate-expiry", "tablecloth-windows-sandbox" })
        {
            var directory = CreateSkill(fixture.Paths.Skills, id);
            File.AppendAllText(Path.Combine(directory, "SKILL.md"), "User-maintained instructions.\r\n");
            Directory.CreateDirectory(Path.Combine(directory, "references"));
            File.WriteAllBytes(Path.Combine(directory, "references", "user-data.bin"), [0, 1, 2, 255]);
        }
        var markers = new[] { "certificate-expiry-skill.installed", "windows-sandbox-skill.installed" }
            .Select(name => fixture.Paths.Under("profiles", "openai-codex", name)).ToArray();
        if (legacyMarkers) foreach (var marker in markers) File.WriteAllText(marker, "legacy marker");
        var original = Directory.GetFiles(fixture.Paths.Skills, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(fixture.Paths.Skills, path), File.ReadAllBytes);
        var runner = new SkillRunner(fixture.Paths);
        var profile = new SkillProfile(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);

        Assert.HasCount(2, await manager.ListAsync(CancellationToken.None));
        await manager.SetEnabledAsync("tablecloth-certificate-expiry", false, CancellationToken.None);
        manager = new(fixture.Paths, fixture.Installer, profile, runner);
        var installed = await manager.ListAsync(CancellationToken.None);
        Assert.HasCount(2, installed);
        Assert.IsFalse(installed.Single(skill => skill.Id == "tablecloth-certificate-expiry").Enabled);
        Assert.IsTrue(installed.Single(skill => skill.Id == "tablecloth-windows-sandbox").Enabled);
        foreach (var file in original)
            CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(Path.Combine(fixture.Paths.Skills, file.Key)));
        Assert.HasCount(original.Count, Directory.GetFiles(fixture.Paths.Skills, "*", SearchOption.AllDirectories));
        foreach (var marker in markers)
        {
            if (legacyMarkers) Assert.AreEqual("legacy marker", File.ReadAllText(marker));
            else Assert.IsFalse(File.Exists(marker));
        }
    }

    [TestMethod]
    [DataRow("tablecloth-certificate-expiry")]
    [DataRow("tablecloth-windows-sandbox")]
    public async Task FormerDefaultSkillCanBeImportedEnabledAndRemovedWithoutReappearing(string id)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var source = CreateSkill(fixture.Paths.Under("cache", "local-source"), id);
        var runner = new SkillRunner(fixture.Paths);
        var profile = new SkillProfile(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);
        Assert.IsEmpty(await manager.ListAsync(CancellationToken.None));
        var installed = (await manager.ImportAsync(source, CancellationToken.None)).Single();
        Assert.AreEqual(id, installed.Id);
        Assert.IsTrue(installed.Enabled);
        Assert.IsNull(installed.Source);
        Assert.IsFalse((await manager.SetEnabledAsync(id, false, CancellationToken.None)).Single().Enabled);
        Assert.IsTrue((await manager.SetEnabledAsync(id, true, CancellationToken.None)).Single().Enabled);
        Assert.IsEmpty(await manager.RemoveAsync(id, CancellationToken.None));
        Assert.IsFalse(Directory.Exists(installed.Directory));
        Assert.AreEqual(AiFailureCode.SkillInvalid,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.SetEnabledAsync(id, true, CancellationToken.None))).Code);

        var runtime = await fixture.Installer.GetActiveAsync(CancellationToken.None);
        Assert.IsNotNull(runtime);
        var working = fixture.Paths.Under("runs", "removed-skill");
        Directory.CreateDirectory(working);
        using (fixture.Paths.AcquireOperation())
            Assert.IsEmpty(await manager.SynchronizeAsync(runtime, working, CancellationToken.None));
        manager = new(fixture.Paths, fixture.Installer, profile, runner);
        Assert.IsEmpty(await manager.ListAsync(CancellationToken.None));
        Assert.AreEqual(id, (await manager.ImportAsync(source, CancellationToken.None)).Single().Id);
        Assert.IsEmpty(await manager.RemoveAsync(id, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("tablecloth-certificate-expiry")]
    [DataRow("tablecloth-windows-sandbox")]
    public async Task FormerDefaultSkillUsesOrdinaryCatalogInstallationAndRemoval(string id)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var source = CreateSkill(fixture.Paths.Under("cache", "catalog-source"), id);
        var provenance = CatalogSource() with { CatalogId = id };
        var runner = new SkillRunner(fixture.Paths);
        var profile = new SkillProfile(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);
        var installed = (await manager.StoreCatalogAsync(source, provenance, null, CancellationToken.None)).Single();
        Assert.AreEqual(id, installed.Id);
        Assert.AreEqual(provenance, installed.Source);
        Assert.IsTrue(installed.Enabled);
        Assert.IsEmpty(await manager.RemoveAsync(id, CancellationToken.None));
        manager = new(fixture.Paths, fixture.Installer, profile, runner);
        Assert.IsEmpty(await manager.ListAsync(CancellationToken.None));
        Assert.IsFalse(Directory.Exists(installed.Directory));
        Assert.AreEqual(provenance, (await manager.StoreCatalogAsync(source, provenance, null, CancellationToken.None)).Single().Source);
        Assert.IsEmpty(await manager.RemoveAsync(id, CancellationToken.None));
    }

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
        Assert.HasCount(1, skills);
        Assert.IsTrue(skills.Single(x => x.Id == "owned-skill").Enabled);
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
        Assert.IsEmpty(await manager.RemoveAsync("owned-skill", CancellationToken.None));
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
            Assert.IsEmpty(await manager.RemoveAsync("imported-skill", CancellationToken.None));
            Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Paths.Skills, "imported-skill")));
            Assert.IsEmpty(await manager.ListAsync(CancellationToken.None));
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
        Assert.HasCount(2, skills);
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
        Assert.HasCount(1, skills);
        Assert.IsTrue(skills.All(x => x.IsValid));
        Assert.IsFalse(Directory.Exists(broken.Directory));
        using (fixture.Paths.AcquireOperation())
            Assert.HasCount(1, await manager.SynchronizeAsync(runtime, working, CancellationToken.None));
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

    [TestMethod]
    public async Task CatalogReplacementPersistsSourceAndPreservesDisabledStateAcrossManagerRestart()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var first = CreateSkill(fixture.Paths.Under("cache", "version-one"), "catalog-skill");
        var second = CreateSkill(fixture.Paths.Under("cache", "version-two"), "catalog-skill");
        File.AppendAllText(Path.Combine(second, "SKILL.md"), "New version content.\n");
        File.WriteAllText(Path.Combine(first, "obsolete.txt"), "Old version only.");
        var runner = new SkillRunner(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), runner);
        var oldSource = CatalogSource();
        var newSource = oldSource with { SourceRef = new string('2', 40) };
        var installed = await manager.StoreCatalogAsync(first, oldSource, null, CancellationToken.None);
        Assert.AreEqual(oldSource, installed.Single(x => x.Id == "catalog-skill").Source);
        await manager.SetEnabledAsync("catalog-skill", false, CancellationToken.None);
        var enabledState = File.ReadAllText(fixture.Paths.SkillState);

        manager = new(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), runner);
        var updated = await manager.StoreCatalogAsync(second, newSource, oldSource, CancellationToken.None);
        var skill = updated.Single(x => x.Id == "catalog-skill");
        Assert.AreEqual(newSource, skill.Source);
        Assert.IsFalse(skill.Enabled);
        Assert.AreEqual(enabledState, File.ReadAllText(fixture.Paths.SkillState));
        Assert.Contains("New version content.", File.ReadAllText(Path.Combine(skill.Directory, "SKILL.md")));
        Assert.IsFalse(File.Exists(Path.Combine(skill.Directory, "obsolete.txt")));
        Assert.IsFalse(Directory.GetDirectories(fixture.Paths.Skills).Any(x => Path.GetFileName(x).StartsWith('.')));
        manager = new(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), runner);
        Assert.AreEqual(newSource, (await manager.ListAsync(CancellationToken.None)).Single(x => x.Id == "catalog-skill").Source);
        await manager.RemoveAsync("catalog-skill", CancellationToken.None);
        Assert.IsFalse(Directory.Exists(skill.Directory));
        var local = (await manager.ImportAsync(first, CancellationToken.None)).Single(x => x.Id == "catalog-skill");
        Assert.IsNull(local.Source);
    }

    [TestMethod]
    public async Task CatalogReplacementCannotOverwriteLocalSkillsOrAChangedCatalogOriginOrVersion()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var source = CreateSkill(fixture.Paths.Under("cache", "catalog-source"), "catalog-skill");
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), new SkillRunner(fixture.Paths));
        var local = (await manager.ImportAsync(source, CancellationToken.None)).Single(x => x.Id == "catalog-skill");
        var original = File.ReadAllText(Path.Combine(local.Directory, "SKILL.md"));
        Assert.AreEqual(AiFailureCode.SkillAlreadyInstalled,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.StoreCatalogAsync(source, CatalogSource(), null, CancellationToken.None))).Code);
        Assert.AreEqual(AiFailureCode.BlockedByPolicy,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.StoreCatalogAsync(source, CatalogSource(), CatalogSource(), CancellationToken.None))).Code);
        Assert.AreEqual(original, File.ReadAllText(Path.Combine(local.Directory, "SKILL.md")));
        await manager.RemoveAsync("catalog-skill", CancellationToken.None);
        await manager.StoreCatalogAsync(source, CatalogSource(), null, CancellationToken.None);
        foreach (var expected in new[]
        {
            CatalogSource() with { Repository = "https://github.com/other/skills" },
            CatalogSource() with { SourcePath = "other" },
            CatalogSource() with { SourceRef = new string('3', 40) }
        })
            Assert.AreEqual(AiFailureCode.BlockedByPolicy,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.StoreCatalogAsync(source,
                    CatalogSource() with { SourceRef = new string('2', 40) }, expected, CancellationToken.None))).Code);
        Assert.AreEqual(CatalogSource(), (await manager.ListAsync(CancellationToken.None)).Single(x => x.Id == "catalog-skill").Source);
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("provider")]
    [DataRow("cancel")]
    public async Task FailedOrCanceledCatalogValidationRestoresFilesSourceEnabledStateAndPolicy(string failure)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var first = CreateSkill(fixture.Paths.Under("cache", "version-one"), "catalog-skill");
        var second = CreateSkill(fixture.Paths.Under("cache", "version-two"), "catalog-skill");
        File.AppendAllText(Path.Combine(second, "SKILL.md"), "replacement-version\n");
        var runner = new SkillRunner(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), runner);
        var old = (await manager.StoreCatalogAsync(first, CatalogSource(), null, CancellationToken.None)).Single(x => x.Id == "catalog-skill");
        await manager.SetEnabledAsync(old.Id, false, CancellationToken.None);
        var originalFiles = Directory.GetFiles(old.Directory).ToDictionary(x => Path.GetFileName(x)!, File.ReadAllText);
        var originalState = File.ReadAllText(fixture.Paths.SkillState);
        var originalPolicy = File.ReadAllText(fixture.Paths.SkillConfiguration);
        using var cancellation = new CancellationTokenSource();
        if (failure == "invalid") runner.InvalidMarker = "replacement-version";
        else runner.BeforeDiscovery = token =>
        {
            if (!File.ReadAllText(Path.Combine(old.Directory, "SKILL.md")).Contains("replacement-version", StringComparison.Ordinal)) return Task.CompletedTask;
            if (failure == "provider") throw new ManagedAiException(AiFailureCode.ProviderFailed);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        var replacement = CatalogSource() with { SourceRef = new string('2', 40) };
        if (failure == "cancel")
            await Assert.ThrowsAsync<OperationCanceledException>(() => manager.StoreCatalogAsync(second, replacement, CatalogSource(), cancellation.Token));
        else
            Assert.AreEqual(failure == "invalid" ? AiFailureCode.SkillInvalid : AiFailureCode.ProviderFailed,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.StoreCatalogAsync(second, replacement, CatalogSource(), cancellation.Token))).Code);
        foreach (var file in originalFiles) Assert.AreEqual(file.Value, File.ReadAllText(Path.Combine(old.Directory, file.Key!)));
        Assert.AreEqual(originalState, File.ReadAllText(fixture.Paths.SkillState));
        Assert.AreEqual(originalPolicy, File.ReadAllText(fixture.Paths.SkillConfiguration));
        Assert.IsFalse(Directory.GetDirectories(fixture.Paths.Skills).Any(x => Path.GetFileName(x).StartsWith('.')));
        var restored = (await manager.ListAsync(CancellationToken.None)).Single(x => x.Id == old.Id);
        Assert.AreEqual(CatalogSource(), restored.Source);
        Assert.IsFalse(restored.Enabled);
        Assert.IsTrue(restored.IsValid);
    }

    [TestMethod]
    public async Task LocalImportCannotSupplyCatalogProvenanceMetadata()
    {
        using var fixture = new InstallFixture();
        var source = CreateSkill(fixture.Paths.Under("cache", "spoofed-source"), "catalog-skill");
        File.WriteAllText(Path.Combine(source, ".tablecloth-source.json"), "{}");
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), new SkillRunner(fixture.Paths));
        Assert.AreEqual(AiFailureCode.SkillInvalid,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.ImportAsync(source, CancellationToken.None))).Code);
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Paths.Skills, "catalog-skill")));
    }

    [TestMethod]
    public async Task CommittedCatalogUpdateKeepsItsSuccessfulResultWhenBackupCleanupIsBlocked()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var first = CreateSkill(fixture.Paths.Under("cache", "previous-version"), "catalog-skill");
        var second = CreateSkill(fixture.Paths.Under("cache", "replacement-version"), "catalog-skill");
        File.AppendAllText(Path.Combine(second, "SKILL.md"), "Replacement instructions.\n");
        var runner = new SkillRunner(fixture.Paths);
        var manager = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, new SkillProfile(fixture.Paths), runner);
        await manager.StoreCatalogAsync(first, CatalogSource(), null, CancellationToken.None);
        FileStream? lockedBackup = null;
        runner.BeforeDiscovery = _ =>
        {
            var backup = Directory.EnumerateDirectories(fixture.Paths.Skills, ".previous-*").SingleOrDefault();
            if (backup is not null && lockedBackup is null)
                lockedBackup = new(Path.Combine(backup, "SKILL.md"), FileMode.Open, FileAccess.Read, FileShare.None);
            return Task.CompletedTask;
        };
        try
        {
            var nextSource = CatalogSource() with { SourceRef = new string('2', 40) };
            var result = await manager.StoreCatalogAsync(second, nextSource, CatalogSource(), CancellationToken.None);
            Assert.IsNotNull(lockedBackup);
            Assert.AreEqual(nextSource, result.Single(x => x.Id == "catalog-skill").Source);
            Assert.Contains("Replacement instructions.", File.ReadAllText(Path.Combine(fixture.Paths.Skills, "catalog-skill", "SKILL.md")));
        }
        finally { lockedBackup?.Dispose(); }
    }

    private static AiSkillSource CatalogSource() => new("catalog-skill", "https://github.com/fixture/skills", "skill", new string('1', 40));

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
        public string? InvalidMarker;
        public Func<CancellationToken, Task>? BeforeDiscovery;
        public string ExternalManifest => Path.Combine(paths.Root, "external-skills", "outside", "SKILL.md");
        public string ManagedManifest => Path.Combine(paths.Skills, "owned-skill", "SKILL.md");
        public string SystemManifest => Path.Combine(paths.Skills, ".system", "built-in", "SKILL.md");

        public async Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr,
            CancellationToken cancellationToken)
        {
            if (BeforeDiscovery is not null) await BeforeDiscovery(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Invocation = spec;
            Assert.AreEqual(CodexSkillListExchange.Initialize, spec.Input);
            Assert.Contains("skills/list", spec.Respond!("{\"id\":1,\"result\":{}}")!.Text!);
            var directories = Directory.Exists(paths.Skills)
                ? Directory.GetDirectories(paths.Skills).Where(x => !Path.GetFileName(x).StartsWith('.'))
                    .ToArray()
                : [];
            bool Invalid(string directory) => InvalidIds.Contains(Path.GetFileName(directory)) || InvalidMarker is not null &&
                File.ReadAllText(Path.Combine(directory, "SKILL.md")).Contains(InvalidMarker, StringComparison.Ordinal);
            var managed = directories.Where(x => !Invalid(x))
                .Select(x => new { name = Path.GetFileName(x), description = "Managed skill.",
                    path = Path.Combine(x, "SKILL.md"), scope = "user", enabled = true }).ToArray();
            var errors = directories.Where(Invalid)
                .Select(x => new { path = Path.Combine(x, "SKILL.md"), message = RawError }).ToArray();
            var all = managed.Cast<object>().Concat([
                new { name = "outside", description = "External skill.", path = ExternalManifest, scope = "user", enabled = true },
                new { name = "built-in", description = "System skill.", path = SystemManifest, scope = "system", enabled = true }
            ]).ToArray();
            var response = JsonSerializer.Serialize(new { id = 2, result = new
            { data = new[] { new { cwd = spec.StartInfo.WorkingDirectory, skills = all, errors } } } });
            Assert.IsTrue(spec.Respond(response)!.Close);
            return new ProcessOutcome(0, response.Length, 0);
        }
    }
}
