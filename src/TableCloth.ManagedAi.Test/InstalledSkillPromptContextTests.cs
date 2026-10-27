using System.Net;
using System.Text;
using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class InstalledSkillPromptContextTests
{
    [TestMethod]
    public async Task UsesOnlyPinnedPublicDocumentsAndCachesSuccessfulResponsesWithoutReadingInstalledBodies()
    {
        using var fixture = new InstallFixture();
        var skill = PublicSkill("public-guide") with { Directory = fixture.Paths.Under("private-installed-guide") };
        Directory.CreateDirectory(skill.Directory);
        File.WriteAllText(Path.Combine(skill.Directory, "SKILL.md"), "LOCAL_BODY_MUST_NOT_BE_FORWARDED");
        File.WriteAllText(Path.Combine(skill.Directory, ".env"), "PRIVATE_SECRET_MUST_NOT_BE_READ");
        var requests = new List<Uri>();
        using var clients = new TestHttpFactory(request =>
        {
            requests.Add(request.RequestUri!);
            Assert.IsNull(request.Headers.Authorization);
            Assert.IsFalse(request.Headers.Contains("Cookie"));
            var file = request.RequestUri!.Segments[^1];
            return Ok(file == "SKILL.md" ? "Public guide: [reference](references/public.md)" : "PUBLIC_REFERENCE");
        });

        var context = await InstalledSkillPromptContext.CreateAsync([skill], clients, CancellationToken.None);
        Assert.Contains("PUBLIC_REFERENCE", context);
        Assert.IsFalse(context.Contains("LOCAL_BODY_MUST_NOT_BE_FORWARDED", StringComparison.Ordinal));
        Assert.IsFalse(context.Contains("PRIVATE_SECRET_MUST_NOT_BE_READ", StringComparison.Ordinal));
        Assert.IsFalse(context.Contains(skill.Directory, StringComparison.Ordinal));
        Assert.HasCount(3, requests);
        Assert.IsTrue(requests.All(x => x.Scheme == "https" && x.Host == "raw.githubusercontent.com" &&
            x.AbsolutePath.StartsWith("/owner/public-guide/" + new string('a', 40) + "/skill/", StringComparison.Ordinal)));
        var second = await InstalledSkillPromptContext.CreateAsync([skill], clients, CancellationToken.None);
        Assert.AreEqual(context, second);
        Assert.HasCount(3, requests);
        using var parsed = Parse(context);
        Assert.HasCount(3, parsed.RootElement.GetProperty("documents").EnumerateArray().ToArray());
        Assert.IsFalse(parsed.RootElement.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public async Task LocalImportsForwardOnlyIdsAndDisabledOrInvalidSkillsAreExcluded()
    {
        using var clients = new TestHttpFactory(_ => throw new AssertFailedException("Local metadata must not cause an HTTP request."));
        var local = new AiSkill("local-guide", "Private local name", "Private description", "C:\\private", true);
        var context = await InstalledSkillPromptContext.CreateAsync([
            local, PublicSkill("disabled") with { Enabled = false }, PublicSkill("invalid") with { IsValid = false }
        ], clients, CancellationToken.None);

        using var parsed = Parse(context);
        CollectionAssert.AreEqual(new[] { "local-guide" }, parsed.RootElement.GetProperty("localSkillIdsWithoutForwardedBodies")
            .EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.HasCount(0, parsed.RootElement.GetProperty("documents").EnumerateArray().ToArray());
        Assert.IsFalse(context.Contains("Private local name", StringComparison.Ordinal));
        Assert.IsFalse(context.Contains("Private description", StringComparison.Ordinal));
        Assert.IsFalse(context.Contains("C:\\private", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task RejectsUnpinnedOrUnsafeSourcesBeforeAnyRequest()
    {
        var skill = PublicSkill("safe");
        var source = skill.Source!;
        using var clients = new TestHttpFactory(_ => throw new AssertFailedException("Unsafe sources must not be requested."));
        var variants = new[]
        {
            skill with { Source = source with { SourceRef = "main" } },
            skill with { Source = source with { Repository = "https://github.com.evil.example/owner/repo" } },
            skill with { Source = source with { Repository = "https://user:secret@github.com/owner/repo" } },
            skill with { Source = source with { Repository = "http://github.com/owner/repo" } },
            skill with { Source = source with { SourcePath = "../private" } },
            skill with { Source = source with { SourcePath = "skill%2f..%2fprivate" } },
            skill with { Source = source with { CatalogId = "other" } }
        };
        var context = await InstalledSkillPromptContext.CreateAsync(variants, clients, CancellationToken.None);
        using var parsed = Parse(context);
        Assert.AreEqual(variants.Length, parsed.RootElement.GetProperty("omittedDocuments").GetInt32());
        Assert.HasCount(0, parsed.RootElement.GetProperty("documents").EnumerateArray().ToArray());
    }

    [TestMethod]
    public async Task FetchesAtMostFourSafeRelativeReferencesFromTheSamePinnedSource()
    {
        const string manifest = """
            [external](https://evil.example/private.md)
            [escape](references/../../private.md)
            [encoded](references/%2e%2e/private.md)
            [query](references/private.md?token=secret)
            [1](references/one.md) [duplicate](references/one.md)
            [2](./references/two.md) [3](references/three.md)
            [4](references/four.md) [5](references/five.md)
            """;
        var paths = new List<string>();
        using var clients = new TestHttpFactory(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            Assert.AreEqual("raw.githubusercontent.com", request.RequestUri.Host);
            return Ok(request.RequestUri.AbsolutePath.EndsWith("/SKILL.md", StringComparison.Ordinal) ? manifest : "Public reference");
        });
        await InstalledSkillPromptContext.CreateAsync([PublicSkill("references")], clients, CancellationToken.None);
        Assert.HasCount(6, paths);
        Assert.IsFalse(paths.Any(x => x.Contains("private", StringComparison.Ordinal) || x.Contains("five", StringComparison.Ordinal)));
        Assert.HasCount(1, paths.Where(x => x.EndsWith("/references/one.md", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task PublicBacktickPlainAndInstructionCliReferencePathsResolveWithoutFollowingUnsafePaths()
    {
        const string manifest = """
            ## References
            - `references/document-types.md`: Report structures.
            - `references/quality-checklist.md`: Review checklist.
            Plain reference: references/plain-guide.md
            `https://evil.example/references/private.md`
            `/references/private.md` `../references/private.md`
            `C:/references/private.md` `//host/references/private.md`
            `references/../private.md` `references/%2e%2e/private.md`
            `references/private.md?token=secret` `references/private.md:secret`
            """;
        const string instruction = "Use `npx -y @nomadamas/k-skill@0 read kosis-stats references/kosis-openapi-guide.md` for the public API guide.";
        var paths = new List<string>();
        using var clients = new TestHttpFactory(request =>
        {
            var uri = request.RequestUri!;
            paths.Add(uri.AbsolutePath);
            Assert.AreEqual("raw.githubusercontent.com", uri.Host);
            Assert.AreEqual(string.Empty, uri.Query);
            return Ok(uri.AbsolutePath.EndsWith("/SKILL.md", StringComparison.Ordinal) ? manifest
                : uri.AbsolutePath.EndsWith("/instruction.md", StringComparison.Ordinal) ? instruction : "Public reference text.");
        });

        var context = await InstalledSkillPromptContext.CreateAsync([PublicSkill("official-report-skillset")], clients, CancellationToken.None);

        Assert.HasCount(6, paths);
        Assert.IsFalse(paths.Any(x => x.Contains("private", StringComparison.Ordinal)));
        using var parsed = Parse(context);
        var references = parsed.RootElement.GetProperty("documents").EnumerateArray()
            .Select(x => x.GetProperty("file").GetString()!).Where(x => x.StartsWith("references/", StringComparison.Ordinal)).ToArray();
        CollectionAssert.AreEquivalent(new[] { "references/document-types.md", "references/quality-checklist.md",
            "references/plain-guide.md", "references/kosis-openapi-guide.md" }, references);
        Assert.IsFalse(parsed.RootElement.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public async Task CalculatorSizedPublicOperationReferenceIncludesItsFinalBmiContractWithoutTruncation()
    {
        const string bmi = "\n## bmi\nInput JSON uses operation=bmi, weight_kg and height_cm.\n";
        // The pinned calculator reference is 18,333 bytes, beyond the former 16 KiB cap.
        // The complete contract at its end must survive the public HTTP context path.
        var reference = new string('x', 18333 - Encoding.UTF8.GetByteCount(bmi)) + bmi;
        using var clients = new TestHttpFactory(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return Ok(path.EndsWith("/SKILL.md", StringComparison.Ordinal)
                ? "Use [operations](references/operations.md) for exact JSON fields."
                : path.EndsWith("/references/operations.md", StringComparison.Ordinal) ? reference : "Public instructions.");
        });

        var context = await InstalledSkillPromptContext.CreateAsync([PublicSkill("tablecloth-calculators")], clients, CancellationToken.None);

        using var parsed = Parse(context);
        var root = parsed.RootElement;
        Assert.IsFalse(root.GetProperty("truncated").GetBoolean());
        Assert.AreEqual(0, root.GetProperty("omittedDocuments").GetInt32());
        var operationReference = root.GetProperty("documents").EnumerateArray()
            .Single(x => x.GetProperty("file").GetString() == "references/operations.md");
        Assert.AreEqual(reference, operationReference.GetProperty("text").GetString());
        Assert.IsFalse(operationReference.GetProperty("truncated").GetBoolean());
        Assert.Contains(bmi, operationReference.GetProperty("text").GetString()!);
    }

    [TestMethod]
    public async Task BoundsSerializedContextAndIdentifiesTruncatedDocuments()
    {
        var large = new string('가', 4096);
        using var clients = new TestHttpFactory(_ => Ok(large));
        var context = await InstalledSkillPromptContext.CreateAsync([
            PublicSkill("one"), PublicSkill("two"), PublicSkill("three"), PublicSkill("four"),
            PublicSkill("five"), PublicSkill("six"), PublicSkill("seven"), PublicSkill("eight")
        ], clients, CancellationToken.None);
        var json = ContextJson(context);
        Assert.IsLessThanOrEqualTo(InstalledSkillPromptContext.MaximumDocumentBytes, Encoding.UTF8.GetByteCount(json));
        using var parsed = JsonDocument.Parse(json);
        Assert.IsTrue(parsed.RootElement.GetProperty("truncated").GetBoolean());
        var documents = parsed.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        Assert.IsTrue(documents.Any(x => x.GetProperty("truncated").GetBoolean()));
        foreach (var document in documents)
            Assert.IsLessThanOrEqualTo(InstalledSkillPromptContext.MaximumFileBytes,
                Encoding.UTF8.GetByteCount(document.GetProperty("text").GetString()!));
    }

    [TestMethod]
    public async Task CompleteSixSkillSizedPublicContextRetainsReportReferencesKosisGuideAndFinalBmiContract()
    {
        const string bmi = "\nFinal BMI contract: operation=bmi, height_cm, weight_kg.\n";
        var payloads = new Dictionary<(string Skill, string File), string>
        {
            [("bok-ecos-stats", "SKILL.md")] = Sized(1797, "Public BOK skill."),
            [("bok-ecos-stats", "instruction.md")] = Sized(4350, "Public BOK instructions."),
            [("kosis-stats", "SKILL.md")] = Sized(1748, "Public KOSIS skill."),
            [("kosis-stats", "instruction.md")] = Sized(15829, "Use `npx read kosis-stats references/kosis-openapi-guide.md` for the guide."),
            [("kosis-stats", "references/kosis-openapi-guide.md")] = Sized(8589, "Public KOSIS API guide."),
            [("official-report-skillset", "SKILL.md")] = Sized(5308,
                "Read `references/document-types.md` and `references/quality-checklist.md`."),
            [("official-report-skillset", "references/document-types.md")] = Sized(1896, "Public report types."),
            [("official-report-skillset", "references/quality-checklist.md")] = Sized(1479, "Public report checklist."),
            [("tablecloth-calculators", "SKILL.md")] = Sized(2595, "Read [operations](references/operations.md)."),
            [("tablecloth-calculators", "references/operations.md")] = Sized(18333, bmi),
            [("tablecloth-certificate-expiry", "SKILL.md")] = Sized(1683, "Explain only the supplied host certificate counts."),
            [("tablecloth-windows-sandbox", "SKILL.md")] = Sized(1482, "Explain only the supplied host Sandbox report.")
        };
        Assert.AreEqual(65089, payloads.Values.Sum(x => Encoding.UTF8.GetByteCount(x)));
        using var clients = new TestHttpFactory(request =>
        {
            var parts = request.RequestUri!.AbsolutePath.Split('/');
            return payloads.TryGetValue((parts[2], string.Join('/', parts.Skip(5))), out var text)
                ? Ok(text) : new(HttpStatusCode.NotFound);
        });
        var skills = payloads.Keys.Select(x => x.Skill).Distinct(StringComparer.Ordinal).Select(PublicSkill).ToArray();

        var context = await InstalledSkillPromptContext.CreateAsync(skills, clients, CancellationToken.None);

        using var parsed = Parse(context);
        var root = parsed.RootElement;
        Assert.IsFalse(root.GetProperty("truncated").GetBoolean());
        Assert.AreEqual(4, root.GetProperty("omittedDocuments").GetInt32(), "Four skills intentionally have no instruction.md.");
        var documents = root.GetProperty("documents").EnumerateArray().ToArray();
        Assert.HasCount(12, documents);
        foreach (var document in documents)
        {
            var key = (document.GetProperty("skillId").GetString()!, document.GetProperty("file").GetString()!);
            Assert.AreEqual(payloads[key], document.GetProperty("text").GetString());
            Assert.IsFalse(document.GetProperty("truncated").GetBoolean());
        }
        Assert.IsLessThanOrEqualTo(InstalledSkillPromptContext.MaximumDocumentBytes, Encoding.UTF8.GetByteCount(ContextJson(context)));
    }

    [TestMethod]
    public async Task OversizedInvalidUtf8AndUnavailableDocumentsDoNotFailTheChatContext()
    {
        var oversized = PublicSkill("oversized");
        var malformed = PublicSkill("malformed");
        var unavailable = PublicSkill("unavailable");
        using var clients = new TestHttpFactory(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/unavailable/", StringComparison.Ordinal)) return new(HttpStatusCode.NotFound);
            if (path.EndsWith("/instruction.md", StringComparison.Ordinal)) return Ok("AVAILABLE_PUBLIC_INSTRUCTIONS");
            return path.Contains("/oversized/", StringComparison.Ordinal)
                ? new(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[InstalledSkillPromptContext.MaximumFileBytes + 1]) }
                : new(HttpStatusCode.OK) { Content = new ByteArrayContent([0xc3, 0x28]) };
        });
        var context = await InstalledSkillPromptContext.CreateAsync([oversized, malformed, unavailable], clients, CancellationToken.None);
        Assert.Contains("AVAILABLE_PUBLIC_INSTRUCTIONS", context);
        using var parsed = Parse(context);
        Assert.IsTrue(parsed.RootElement.GetProperty("truncated").GetBoolean());
        Assert.AreEqual(4, parsed.RootElement.GetProperty("omittedDocuments").GetInt32());
        Assert.HasCount(2, parsed.RootElement.GetProperty("documents").EnumerateArray().ToArray());
    }

    [TestMethod]
    public async Task CancellationIsPreservedWithoutARequest()
    {
        using var clients = new TestHttpFactory(_ => throw new AssertFailedException("A canceled request must not fetch documents."));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            InstalledSkillPromptContext.CreateAsync([PublicSkill("cancel")], clients, cancellation.Token));
    }

    private static AiSkill PublicSkill(string name) => new(name, name, "public guide", "C:\\not-read", true)
    {
        Source = new(name, "https://github.com/owner/" + name, "skill", new string('a', 40))
    };

    private static HttpResponseMessage Ok(string text) => new(HttpStatusCode.OK)
    { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(text)) };

    private static string Sized(int bytes, string tail) => new string(' ', bytes - Encoding.UTF8.GetByteCount(tail)) + tail;

    private static string ContextJson(string context)
    {
        const string marker = "UNTRUSTED_PUBLIC_SKILL_DOCUMENTS:\n";
        return context[(context.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
    }

    private static JsonDocument Parse(string context) => JsonDocument.Parse(ContextJson(context));
}
