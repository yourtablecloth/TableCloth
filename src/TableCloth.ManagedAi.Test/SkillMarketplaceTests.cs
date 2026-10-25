using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillMarketplaceTests
{
    private const string Commit = "1111111111111111111111111111111111111111";
    private const string RootTree = "2222222222222222222222222222222222222222";
    private const string SkillTree = "3333333333333333333333333333333333333333";
    private const string Api = "https://api.github.com/repos/fixture/skills";

    [TestMethod]
    public void PoCEvaluationCatalogMatchesRuntimeParserWithoutReviewState()
    {
        var entries = SkillCatalogClient.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "SkillCatalog.xml")));
        CollectionAssert.AreEquivalent(new[]
        {
            "official-report-skillset", "tablecloth-certificate-expiry", "tablecloth-windows-sandbox",
            "tablecloth-calculators", "bok-ecos-stats", "kosis-stats"
        }, entries.Select(x => x.Id).ToArray());
        // CanInstall checks technical file-installation requirements, not successful skill execution.
        Assert.IsTrue(entries.All(x => x.CanInstall));
        Assert.IsTrue(entries.All(x => x.SourceRef.Length == 40));
        Assert.AreEqual(4, entries.Count(x => x.Network == "none"));
        Assert.AreEqual(2, entries.Count(x => x.Network == "required"));
        foreach (var id in new[] { "bok-ecos-stats", "kosis-stats" })
        {
            var entry = entries.Single(x => x.Id == id);
            Assert.AreEqual("required", entry.Network);
            Assert.AreEqual("https://github.com/NomaDamas/k-skill", entry.Repository);
            Assert.AreEqual(id, entry.SourcePath);
            Assert.AreEqual("0102e5133865f00886e37ac8cf39a49e2c7a3d39", entry.SourceRef);
        }
    }

    [TestMethod]
    public void VersionOneCatalogReadsProvenanceAndUsesTechnicalInstallationRequirements()
    {
        var entry = SkillCatalogClient.Parse(Bytes(Catalog())).Single();
        Assert.AreEqual("fixture-skill", entry.Id);
        Assert.AreEqual("Author", entry.Author);
        Assert.AreEqual("https://github.com/fixture", entry.AuthorUrl);
        Assert.AreEqual("https://github.com/fixture/skills/blob/" + Commit + "/LICENSE", entry.LicenseSource);
        Assert.AreEqual("Review the upstream dependencies.", entry.Notes);
        Assert.IsTrue(entry.CanInstall);
        Assert.IsFalse((entry with { InstallMode = "adapter" }).CanInstall);
        Assert.IsFalse((entry with { SourceRef = "main" }).CanInstall);
        Assert.IsFalse((entry with { Runtime = "Other" }).CanInstall);
        Assert.IsFalse((entry with { LicenseSource = string.Empty }).CanInstall);
        Assert.IsFalse((entry with { LicenseSource = "https://github.com/fixture/skills/blob/main/LICENSE" }).CanInstall);
        var legacy = Catalog().Replace("Status=\"listed\"", "Status=\"anything\"", StringComparison.Ordinal)
            .Replace("<License ", "<License ReviewStatus=\"rejected\" ", StringComparison.Ordinal)
            .Replace("<Install ", "<Install ReviewStatus=\"pending\" ", StringComparison.Ordinal);
        Assert.AreEqual(entry, SkillCatalogClient.Parse(Bytes(legacy)).Single());
    }

    [TestMethod]
    public void CatalogWithoutPinnedSourceOrLicenseIsRejected()
    {
        var xml = "<SkillCatalog Version=\"1\"><Skill Id=\"k-dart\" Category=\"Finance\" DisplayName=\"DART\" Status=\"listed\">" +
            "<Description>Public filings.</Description><Source Repository=\"https://github.com/NomaDamas/k-skill\" Path=\"k-dart\"/>" +
            "<License SPDX=\"MIT\" ReviewStatus=\"pending\"/><Requirements Runtime=\"Codex\" Network=\"required\"/>" +
            "<Install Mode=\"upstream\" ReviewStatus=\"pending\"/></Skill></SkillCatalog>";
        Assert.AreEqual(AiFailureCode.SkillInvalid,
            Assert.ThrowsExactly<ManagedAiException>(() => SkillCatalogClient.Parse(Bytes(xml))).Code);
    }

    [TestMethod]
    [DataRow("Version=\"1\"", "Version=\"2\"")]
    [DataRow("Runtime=\"Codex\"", "Runtime=\"Other\"")]
    [DataRow("Mode=\"upstream\"", "Mode=\"execute\"")]
    [DataRow("Network=\"required\"", "Network=\"anything\"")]
    [DataRow("Path=\"skill\"", "Path=\"../skill\"")]
    [DataRow("Path=\"skill\"", "Path=\"skill\\child\"")]
    [DataRow("Path=\"skill\"", "Path=\"skill/%2e%2e\"")]
    [DataRow("Path=\"skill\"", "Path=\"skill/CON.txt\"")]
    [DataRow("Path=\"skill\"", "Path=\"skill/item.\"")]
    [DataRow("https://github.com/fixture/skills\"", "http://github.com/fixture/skills\"")]
    [DataRow("https://github.com/fixture/skills\"", "https://example.com/fixture/skills\"")]
    [DataRow("https://github.com/fixture/skills\"", "https://github.com/fixture/skills?token=secret\"")]
    [DataRow("Ref=\"1111111111111111111111111111111111111111\"", "Ref=\"main\"")]
    [DataRow("<Description>", "<Description extra=\"value\">")]
    [DataRow("<Source ", "<Source Unexpected=\"value\" ")]
    public void CatalogRejectsUnsupportedSchemaAndUnsafeSources(string from, string to)
    {
        var error = Assert.ThrowsExactly<ManagedAiException>(() => SkillCatalogClient.Parse(Bytes(Catalog().Replace(from, to, StringComparison.Ordinal))));
        Assert.AreEqual(AiFailureCode.SkillInvalid, error.Code);
    }

    [TestMethod]
    public void CatalogRejectsEntitiesDuplicateIdsDuplicateFieldsAndOversizedInput()
    {
        string[] inputs =
        [
            "<!DOCTYPE SkillCatalog [<!ENTITY external SYSTEM 'file:///C:/Windows/win.ini'>]>" + Catalog().Replace("A fixture skill.", "&external;", StringComparison.Ordinal),
            Catalog().Replace("</SkillCatalog>", CatalogSkill() + "</SkillCatalog>", StringComparison.Ordinal),
            Catalog().Replace("</Skill>", "<Notes>Duplicate.</Notes></Skill>", StringComparison.Ordinal),
            Catalog().Replace("A fixture skill.", new string('x', 4097), StringComparison.Ordinal),
            "<SkillCatalog Version=\"1\">" + new string(' ', SkillCatalogClient.MaxCatalogBytes) + "</SkillCatalog>",
            "<SkillCatalog Version=\"1\"><Skill><a><b><c/></b></a></Skill></SkillCatalog>",
            "<SkillCatalog Version=\"1\">" + string.Concat(Enumerable.Range(1, 257).Select(i => CatalogSkill().Replace("fixture-skill\"", $"fixture-{i}\"", StringComparison.Ordinal))) + "</SkillCatalog>"
        ];
        foreach (var input in inputs)
            Assert.AreEqual(AiFailureCode.SkillInvalid,
                Assert.ThrowsExactly<ManagedAiException>(() => SkillCatalogClient.Parse(Bytes(input))).Code);
    }

    [TestMethod]
    public async Task MarketplaceRechecksLatestTechnicalRequirementsBeforeDownloadingAnyUpstreamFiles()
    {
        using var fixture = new Fixture();
        fixture.Http.Reply(SkillCatalogClient.CatalogUrl, Catalog());
        var marketplace = fixture.Marketplace;
        Assert.IsTrue((await marketplace.ListAsync(CancellationToken.None)).Single().CanInstall);
        fixture.Http.Reply(SkillCatalogClient.CatalogUrl, Catalog().Replace("Mode=\"upstream\"", "Mode=\"adapter\"", StringComparison.Ordinal));
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => marketplace.InstallAsync("fixture-skill", CancellationToken.None));
        Assert.AreEqual(AiFailureCode.BlockedByPolicy, error.Code);
        Assert.HasCount(2, fixture.Http.Requests);
        Assert.IsTrue(fixture.Http.Requests.All(x => x == SkillCatalogClient.CatalogUrl));
        Assert.IsFalse(fixture.Manager.Imported);
    }

    [TestMethod]
    public async Task ImmutableGitTreeAndContentImportPreservesFilesAndCleansStaging()
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        var result = await fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None);
        Assert.AreEqual("fixture-skill", result.Single().Id);
        Assert.AreEqual("# Fixture skill\n", fixture.Manager.Contents["SKILL.md"]);
        Assert.AreEqual("Reference material.\n", fixture.Manager.Contents["references/guide.md"]);
        Assert.AreEqual("Fixture upstream license.\n", fixture.Manager.Contents["LICENSE.upstream"]);
        Assert.IsTrue(fixture.Http.Requests.Where(x => x.Contains("/contents/", StringComparison.Ordinal)).All(x => x.EndsWith("?ref=" + Commit, StringComparison.Ordinal)));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Paths.Under("cache", "marketplace")).Any());
        Assert.IsTrue(fixture.Manager.AcquiredOwnLease);
    }

    [TestMethod]
    [DataRow("120000", "blob")]
    [DataRow("160000", "commit")]
    [DataRow("040000", "blob")]
    public async Task InstallerRejectsSymlinksSubmodulesAndUnknownModes(string mode, string type)
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        fixture.Tree(SkillTree, [TreeItem("SKILL.md", mode, type, RootTree, 10)]);
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None));
        Assert.AreEqual(AiFailureCode.SkillInvalid, error.Code);
        Assert.IsFalse(fixture.Manager.Imported);
        Assert.IsFalse(fixture.Http.Requests.Any(x => x.Contains("/contents/", StringComparison.Ordinal)));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Paths.Under("cache", "marketplace")).Any());
    }

    [TestMethod]
    public async Task InstallerRejectsSymlinkInSourceDirectoryPath()
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        fixture.Tree(RootTree, [TreeItem("skill", "120000", "blob", SkillTree, 20)]);
        Assert.AreEqual(AiFailureCode.SkillInvalid,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
        Assert.IsFalse(fixture.Manager.Imported);
    }

    [TestMethod]
    [DataRow("../escape.md")]
    [DataRow("file:stream")]
    [DataRow("NUL.txt")]
    [DataRow("trailing.")]
    [DataRow("nested/file.md")]
    public async Task InstallerRejectsUnsafeGitTreePathsBeforeContentRequests(string path)
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        fixture.Tree(SkillTree, [TreeItem(path, "100644", "blob", RootTree, 10)]);
        Assert.AreEqual(AiFailureCode.SkillInvalid,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
        Assert.IsFalse(fixture.Http.Requests.Any(x => x.Contains("/contents/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task InstallerRejectsCaseCollisionsTruncatedTreesAndOversizedFiles()
    {
        foreach (var variation in new[] { "case", "truncated", "size", "count" })
        {
            using var fixture = new Fixture();
            fixture.ValidRepository();
            if (variation == "case") fixture.Tree(SkillTree, [TreeItem("SKILL.md", "100644", "blob", RootTree, 10), TreeItem("skill.md", "100644", "blob", RootTree, 10)]);
            if (variation == "truncated") fixture.Tree(SkillTree, [], truncated: true);
            if (variation == "size") fixture.Tree(SkillTree, [TreeItem("SKILL.md", "100644", "blob", RootTree, 1024 * 1024 + 1)]);
            if (variation == "count") fixture.Tree(SkillTree, Enumerable.Range(1, 129).Select(x => TreeItem(x == 1 ? "SKILL.md" : $"file-{x}.md", "100644", "blob", RootTree, 10)).ToArray());
            Assert.AreEqual(AiFailureCode.SkillInvalid,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
            Assert.IsFalse(fixture.Manager.Imported);
        }
    }

    [TestMethod]
    public async Task InstallerRejectsMismatchedBlobIdentityAndGitLfsPointer()
    {
        foreach (var lfs in new[] { false, true })
        {
            using var fixture = new Fixture();
            fixture.ValidRepository();
            var content = lfs ? "version https://git-lfs.github.com/spec/v1\noid sha256:123\nsize 3\n" : "Unexpected replacement.\n";
            var expected = lfs ? BlobSha(Bytes(content)) : RootTree;
            fixture.Tree(SkillTree, [TreeItem("SKILL.md", "100644", "blob", expected, Bytes(content).Length)]);
            fixture.Content("skill/SKILL.md", content, expected);
            Assert.AreEqual(AiFailureCode.SkillInvalid,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
            Assert.IsFalse(fixture.Manager.Imported);
        }
    }

    [TestMethod]
    [DataRow("https://github.com/fixture/skills/blob/main/LICENSE")]
    [DataRow("https://github.com/other/skills/blob/1111111111111111111111111111111111111111/LICENSE")]
    [DataRow("https://github.com/fixture/skills/blob/1111111111111111111111111111111111111111/../LICENSE")]
    public async Task InstallerRequiresLicenseEvidenceFromTheSamePinnedRepository(string licenseSource)
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        fixture.Http.Reply(SkillCatalogClient.CatalogUrl, Catalog().Replace(
            "Source=\"https://github.com/fixture/skills/blob/" + Commit + "/LICENSE\"", "Source=\"" + licenseSource + "\"", StringComparison.Ordinal));
        Assert.AreEqual(AiFailureCode.BlockedByPolicy,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
        Assert.HasCount(1, fixture.Http.Requests);
        Assert.IsFalse(fixture.Manager.Imported);
    }

    [TestMethod]
    public async Task InstallerRejectsLinkedLicenseAndReservedLicenseFileCollision()
    {
        foreach (var variation in new[] { "link", "collision", "source" })
        {
            using var fixture = new Fixture();
            fixture.ValidRepository();
            if (variation == "link")
                fixture.Tree(RootTree, [TreeItem("skill", "040000", "tree", SkillTree, 0), TreeItem("LICENSE", "120000", "blob", RootTree, 5)]);
            else if (variation == "collision")
                fixture.Tree(SkillTree, [TreeItem("SKILL.md", "100644", "blob", RootTree, 5), TreeItem("LICENSE.upstream", "100644", "blob", RootTree, 5)]);
            else
                fixture.Tree(SkillTree, [TreeItem("SKILL.md", "100644", "blob", RootTree, 5), TreeItem(".tablecloth-source.json", "100644", "blob", RootTree, 5)]);
            Assert.AreEqual(AiFailureCode.SkillInvalid,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
            Assert.IsFalse(fixture.Manager.Imported);
        }
    }

    [TestMethod]
    public async Task RedirectAndRateLimitErrorsDoNotFollowUrlsOrLeaveStaging()
    {
        foreach (var status in new[] { HttpStatusCode.Redirect, HttpStatusCode.Forbidden })
        {
            using var fixture = new Fixture();
            fixture.ValidRepository();
            fixture.Http.Reply(Api + "/git/commits/" + Commit, "", status);
            Assert.AreEqual(AiFailureCode.CatalogUnavailable,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
            Assert.HasCount(2, fixture.Http.Requests);
            Assert.IsFalse(fixture.Manager.Imported);
            Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Paths.Under("cache", "marketplace")).Any());
        }
    }

    [TestMethod]
    public async Task CatalogHttpFailuresAreBoundedAndCancellationPropagates()
    {
        using var fixture = new Fixture();
        fixture.Http.Reply(SkillCatalogClient.CatalogUrl, new string('x', SkillCatalogClient.MaxCatalogBytes + 1));
        Assert.AreEqual(AiFailureCode.OutputLimitExceeded,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.ListAsync(CancellationToken.None))).Code);
        fixture.Http.Reply(SkillCatalogClient.CatalogUrl, "", HttpStatusCode.Redirect);
        Assert.AreEqual(AiFailureCode.CatalogUnavailable,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.ListAsync(CancellationToken.None))).Code);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Marketplace.ListAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task ImportFailureCleansDownloadedContentWithoutReplacingExistingSkill()
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        fixture.Manager.Failure = new ManagedAiException(AiFailureCode.SkillAlreadyInstalled);
        Assert.AreEqual(AiFailureCode.SkillAlreadyInstalled,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Paths.Under("cache", "marketplace")).Any());
    }

    [TestMethod]
    public async Task RepeatedLargeTreeMetadataCannotExceedTheOperationTransferBudget()
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        var sourcePath = string.Join('/', Enumerable.Repeat("s", 30));
        fixture.Http.Reply(SkillCatalogClient.CatalogUrl, Catalog().Replace("Path=\"skill\"", "Path=\"" + sourcePath + "\"", StringComparison.Ordinal));
        fixture.Http.Reply(Api + "/git/trees/" + RootTree, JsonSerializer.Serialize(new
        {
            sha = RootTree, truncated = false,
            tree = new[] { TreeItem("s", "040000", "tree", RootTree, 0) }, padding = new string('x', 900000)
        }));
        Assert.AreEqual(AiFailureCode.OutputLimitExceeded,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
        Assert.IsLessThan(30, fixture.Http.Requests.Count);
        Assert.IsFalse(fixture.Manager.Imported);
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Paths.Under("cache", "marketplace")).Any());
    }

    [TestMethod]
    public async Task InstallationCancellationCleansStagingAndDoesNotImportPartialContent()
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        using var cancellation = new CancellationTokenSource();
        fixture.Http.BeforeReply = url => { if (url.Contains("/contents/", StringComparison.Ordinal)) cancellation.Cancel(); };
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Marketplace.InstallAsync("fixture-skill", cancellation.Token));
        Assert.IsFalse(fixture.Manager.Imported);
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Paths.Under("cache", "marketplace")).Any());
    }

    [TestMethod]
    public async Task ExistingLocalIdAndDifferentCatalogOriginCannotBeOverwritten()
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        fixture.Manager.Items = [new("fixture-skill", "Local", "Local.", "C:\\fixture", true)];
        Assert.AreEqual(AiFailureCode.SkillAlreadyInstalled,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", CancellationToken.None))).Code);
        Assert.AreEqual(AiFailureCode.BlockedByPolicy,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.UpdateAsync("fixture-skill", CancellationToken.None))).Code);
        fixture.Manager.Items = [fixture.Manager.Items.Single() with { Source = new("fixture-skill", "https://github.com/other/skills", "skill", Commit) }];
        Assert.AreEqual(AiFailureCode.BlockedByPolicy,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.UpdateAsync("fixture-skill", CancellationToken.None))).Code);
        Assert.IsTrue(fixture.Http.Requests.All(x => x == SkillCatalogClient.CatalogUrl));
        Assert.IsFalse(fixture.Manager.Imported);
    }

    [TestMethod]
    public async Task MarketplaceUpdatePassesCapturedSourceToStoreAndSameVersionDoesNotDownload()
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        var previous = new AiSkillSource("fixture-skill", "https://github.com/fixture/skills", "skill", new string('0', 40));
        fixture.Manager.Items = [new("fixture-skill", "Catalog skill", "Installed.", "C:\\fixture", false) { Source = previous }];
        var result = await fixture.Marketplace.UpdateAsync("fixture-skill", CancellationToken.None);
        Assert.AreEqual(previous, fixture.Manager.CapturedPreviousSource);
        Assert.AreEqual(Commit, result.Single().Source!.SourceRef);
        Assert.IsFalse(result.Single().Enabled);
        fixture.Http.Requests.Clear();
        fixture.Manager.Imported = false;
        await fixture.Marketplace.UpdateAsync("fixture-skill", CancellationToken.None);
        Assert.HasCount(1, fixture.Http.Requests);
        Assert.IsFalse(fixture.Manager.Imported);
    }

    [TestMethod]
    [DataRow("integrity")]
    [DataRow("cancel")]
    [DataRow("success")]
    public async Task DownloadedUpdateUsesRealStoreAndKeepsExistingInstallUntilValidated(string outcome)
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        var manager = new OpenAiCodexSkillManager(fixture.Paths, new NoRuntime(), new StoredProfile(fixture.Paths), new NoRunner());
        var first = fixture.Paths.Under("cache", "previous-source", "fixture-skill");
        Directory.CreateDirectory(first);
        File.WriteAllText(Path.Combine(first, "SKILL.md"), "Previous version instructions.");
        File.WriteAllText(Path.Combine(first, "old-only.txt"), "Preserve until committed.");
        var previous = new AiSkillSource("fixture-skill", "https://github.com/fixture/skills", "skill", new string('0', 40));
        var old = (await manager.StoreCatalogAsync(first, previous, null, CancellationToken.None)).Single(x => x.Id == "fixture-skill");
        await manager.SetEnabledAsync(old.Id, false, CancellationToken.None);
        var files = Directory.GetFiles(old.Directory).ToDictionary(x => Path.GetFileName(x)!, File.ReadAllText);
        var state = File.ReadAllText(fixture.Paths.SkillState);
        var marketplace = new ManagedAiSkillMarketplace(new(fixture.Http), new(fixture.Http, fixture.Paths, manager), manager);
        using var cancellation = new CancellationTokenSource();
        if (outcome == "integrity") fixture.Content("skill/SKILL.md", "Tampered content.", RootTree);
        if (outcome == "cancel") fixture.Http.BeforeReply = url => { if (url.Contains("/contents/", StringComparison.Ordinal)) cancellation.Cancel(); };
        if (outcome == "cancel")
            await Assert.ThrowsAsync<OperationCanceledException>(() => marketplace.UpdateAsync(old.Id, cancellation.Token));
        else if (outcome == "integrity")
            Assert.AreEqual(AiFailureCode.SkillInvalid,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => marketplace.UpdateAsync(old.Id, cancellation.Token))).Code);
        else await marketplace.UpdateAsync(old.Id, cancellation.Token);
        var current = (await manager.ListAsync(CancellationToken.None)).Single(x => x.Id == old.Id);
        Assert.IsFalse(current.Enabled);
        Assert.AreEqual(state, File.ReadAllText(fixture.Paths.SkillState));
        if (outcome == "success")
        {
            Assert.AreEqual(Commit, current.Source!.SourceRef);
            Assert.AreEqual("# Fixture skill\n", File.ReadAllText(Path.Combine(current.Directory, "SKILL.md")));
            Assert.IsFalse(File.Exists(Path.Combine(current.Directory, "old-only.txt")));
            Assert.AreEqual("Fixture upstream license.\n", File.ReadAllText(Path.Combine(current.Directory, "LICENSE.upstream")));
        }
        else
        {
            Assert.AreEqual(previous, current.Source);
            foreach (var file in files) Assert.AreEqual(file.Value, File.ReadAllText(Path.Combine(current.Directory, file.Key)));
        }
        Assert.IsFalse(Directory.GetDirectories(fixture.Paths.Skills).Any(x => Path.GetFileName(x).StartsWith('.')));
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(fixture.Paths.Under("cache", "marketplace")).Any());
    }

    [TestMethod]
    [DataRow("success")]
    [DataRow("invalid")]
    [DataRow("cancel")]
    public async Task CacheCleanupIoFailureDoesNotReplaceTheCommittedResultOrPrimaryException(string outcome)
    {
        using var fixture = new Fixture();
        fixture.ValidRepository();
        using var cancellation = new CancellationTokenSource();
        FileStream? lockedCache = null;
        fixture.Manager.OnStored = source =>
        {
            lockedCache = new(Path.Combine(source, "SKILL.md"), FileMode.Open, FileAccess.Read, FileShare.None);
            if (outcome == "invalid") throw new ManagedAiException(AiFailureCode.SkillInvalid);
            if (outcome == "cancel") { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
        };
        try
        {
            if (outcome == "cancel")
                await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Marketplace.InstallAsync("fixture-skill", cancellation.Token));
            else if (outcome == "invalid")
                Assert.AreEqual(AiFailureCode.SkillInvalid,
                    (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Marketplace.InstallAsync("fixture-skill", cancellation.Token))).Code);
            else Assert.AreEqual(Commit,
                (await fixture.Marketplace.InstallAsync("fixture-skill", cancellation.Token)).Single().Source!.SourceRef);
            Assert.IsNotNull(lockedCache);
        }
        finally { lockedCache?.Dispose(); }
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
    private static string Catalog() => "<SkillCatalog Version=\"1\">" + CatalogSkill() + "</SkillCatalog>";
    private static string CatalogSkill() =>
        $"<Skill Id=\"fixture-skill\" Category=\"Finance\" DisplayName=\"Fixture\" Status=\"listed\">" +
        $"<Description>A fixture skill.</Description><Source Repository=\"https://github.com/fixture/skills\" Path=\"skill\" Ref=\"{Commit}\"/>" +
        "<Author Name=\"Author\" Url=\"https://github.com/fixture\"/>" +
        $"<License SPDX=\"MIT\" Source=\"https://github.com/fixture/skills/blob/{Commit}/LICENSE\"/>" +
        "<Requirements Runtime=\"Codex\" Network=\"required\"/><Install Mode=\"upstream\"/>" +
        "<Notes>Review the upstream dependencies.</Notes></Skill>";

    private static object TreeItem(string path, string mode, string type, string sha, long size) => new { path, mode, type, sha, size };
    private static string BlobSha(byte[] content)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Bytes("blob " + content.Length + "\0"));
        hash.AppendData(content);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Paths = new(Path.Combine(Path.GetTempPath(), "tc-market-" + Guid.NewGuid().ToString("N")));
            Manager = new(Paths);
            Marketplace = new(new(Http), new(Http, Paths, Manager), Manager);
        }
        public ManagedAiPaths Paths { get; }
        public HttpFixture Http { get; } = new();
        public ImportManager Manager { get; }
        public ManagedAiSkillMarketplace Marketplace { get; }

        public void ValidRepository()
        {
            Http.Reply(SkillCatalogClient.CatalogUrl, Catalog());
            Http.Reply(Api + "/git/commits/" + Commit, JsonSerializer.Serialize(new { sha = Commit, tree = new { sha = RootTree } }));
            var license = "Fixture upstream license.\n";
            Tree(RootTree, [TreeItem("skill", "040000", "tree", SkillTree, 0), TreeItem("LICENSE", "100644", "blob", BlobSha(Bytes(license)), Bytes(license).Length)]);
            var referenceTree = "4444444444444444444444444444444444444444";
            var manifest = "# Fixture skill\n";
            var guide = "Reference material.\n";
            Tree(SkillTree, [TreeItem("SKILL.md", "100644", "blob", BlobSha(Bytes(manifest)), Bytes(manifest).Length), TreeItem("references", "040000", "tree", referenceTree, 0)]);
            Tree(referenceTree, [TreeItem("guide.md", "100644", "blob", BlobSha(Bytes(guide)), Bytes(guide).Length)]);
            Content("skill/SKILL.md", manifest, BlobSha(Bytes(manifest)));
            Content("skill/references/guide.md", guide, BlobSha(Bytes(guide)));
            Content("LICENSE", license, BlobSha(Bytes(license)));
        }

        public void Tree(string sha, object[] tree, bool truncated = false) => Http.Reply(Api + "/git/trees/" + sha,
            JsonSerializer.Serialize(new { sha, tree, truncated }));
        public void Content(string path, string content, string sha) => Http.Reply(Api + "/contents/" + path + "?ref=" + Commit,
            JsonSerializer.Serialize(new { type = "file", path, sha, size = Bytes(content).Length, encoding = "base64", content = Convert.ToBase64String(Bytes(content)) }));
        public void Dispose()
        {
            if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
            Http.Dispose();
        }
    }

    private sealed class HttpFixture : HttpMessageHandler, IHttpClientFactory
    {
        private readonly Dictionary<string, (string Content, HttpStatusCode Status)> responses = [];
        public List<string> Requests { get; } = [];
        public Action<string>? BeforeReply;
        public void Reply(string url, string content, HttpStatusCode status = HttpStatusCode.OK) => responses[url] = (content, status);
        public HttpClient CreateClient(string name)
        {
            Assert.AreEqual(SkillCatalogClient.HttpClientName, name);
            return new(this, disposeHandler: false);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add(url);
            BeforeReply?.Invoke(url);
            Assert.IsTrue(responses.TryGetValue(url, out var response), url);
            var message = new HttpResponseMessage(response.Status) { Content = new StringContent(response.Content) };
            if (response.Status == HttpStatusCode.Redirect) message.Headers.Location = new("https://example.com/untrusted");
            return Task.FromResult(message);
        }
    }

    private sealed class ImportManager(ManagedAiPaths paths) : IManagedAiSkillManager, IManagedAiCatalogSkillStore
    {
        public bool Imported;
        public bool AcquiredOwnLease;
        public ManagedAiException? Failure;
        public AiSkillSource? CapturedPreviousSource;
        public Action<string>? OnStored;
        public Dictionary<string, string> Contents { get; } = [];
        public IReadOnlyList<AiSkill> Items = [];
        public string StorageDirectory => paths.Skills;
        public Task<IReadOnlyList<AiSkill>> StoreCatalogAsync(string sourceDirectory, AiSkillSource source, AiSkillSource? expectedSource,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedPreviousSource = expectedSource;
            using var lease = paths.AcquireOperation();
            AcquiredOwnLease = true;
            Assert.AreEqual("fixture-skill", Path.GetFileName(sourceDirectory));
            Assert.IsTrue(sourceDirectory.StartsWith(paths.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                Contents[Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/')] = File.ReadAllText(file);
            if (Failure is not null) throw Failure;
            Imported = true;
            Items = [new("fixture-skill", "Fixture", "Fixture.", sourceDirectory, Items.FirstOrDefault()?.Enabled ?? true) { Source = source }];
            OnStored?.Invoke(sourceDirectory);
            return Task.FromResult(Items);
        }
        public Task<IReadOnlyList<AiSkill>> ImportAsync(string sourceDirectory, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiSkill>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(Items);
        public Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<AiSkill>> RemoveAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoRuntime : IManagedRuntimeManager
    {
        public Task<ManagedRuntime?> GetActiveAsync(CancellationToken token) => Task.FromResult<ManagedRuntime?>(null);
        public Task<ManagedRuntime> InstallAsync(string? version, IProgress<AiProgress>? progress, CancellationToken token) => throw new NotSupportedException();
        public Task<ManagedRuntime> RollbackAsync(CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class NoRunner : IJsonlProcessRunner
    {
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr,
            CancellationToken token) => throw new AssertFailedException("No runtime is installed.");
    }
    private sealed class StoredProfile(ManagedAiPaths paths) : IManagedAiProfile
    {
        public void Prepare()
        {
            Directory.CreateDirectory(paths.Skills);
            File.WriteAllText(paths.SkillConfiguration, "# Empty generated policy.\n");
        }
    }
}
