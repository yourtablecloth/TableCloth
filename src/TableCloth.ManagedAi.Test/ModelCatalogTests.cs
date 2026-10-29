using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ModelCatalogTests
{
    [TestMethod]
    public void PerformsHandshakePagesAndFiltersHiddenAndNonTextModels()
    {
        var exchange = new CodexModelListExchange();
        var handshake = exchange.Accept("{\"id\":1,\"result\":{}}")!.Text!;
        Assert.Contains("initialized", handshake);
        Assert.Contains("model/list", handshake);
        Assert.IsNull(exchange.Accept("{\"method\":\"notice\",\"params\":{}}"));
        var next = exchange.Accept(Page(2, "cursor\"value", Entry("first", false), Entry("hidden", true),
            Entry("image", false, ["image"])))!;
        using var json = JsonDocument.Parse(next.Text!);
        Assert.AreEqual("cursor\"value", json.RootElement.GetProperty("params").GetProperty("cursor").GetString());
        Assert.AreEqual(3, json.RootElement.GetProperty("id").GetInt32());
        Assert.IsTrue(exchange.Accept(Page(3, null, Entry("first", false), Entry("second", false, isDefault: true)))!.Close);
        var models = exchange.Complete(0);
        CollectionAssert.AreEqual(new[] { "first", "second" }, models.Select(x => x.Id).ToArray());
        Assert.IsTrue(models[1].IsDefault);
        Assert.ThrowsExactly<ManagedAiException>(() => exchange.Complete(1));
    }

    [TestMethod]
    [DataRow("{}")] // Incomplete handshake/catalog.
    [DataRow("{\"id\":1,\"error\":{\"message\":\"private diagnostic\"}}")]
    [DataRow("{\"id\":1,\"result\":{}}\n{\"id\":2,\"result\":{\"data\":[]}}")]
    [DataRow("[]")]
    [DataRow("invalid")]
    public void InvalidResponsesUseFixedFailureWithoutLeakingPayload(string lines)
    {
        var exchange = new CodexModelListExchange();
        var error = Assert.ThrowsExactly<ManagedAiException>(() =>
        {
            foreach (var line in lines.Split('\n')) exchange.Accept(line);
            exchange.Complete(0);
        });
        Assert.AreEqual(AiFailureCode.ModelListUnavailable, error.Code);
        Assert.AreEqual("ModelListUnavailable", error.Message);
    }

    [TestMethod]
    public void RejectsRepeatedPaginationAndUnsafeModelIdentifiers()
    {
        var exchange = new CodexModelListExchange();
        exchange.Accept("{\"id\":1,\"result\":{}}");
        exchange.Accept(Page(2, "repeat", Entry("first", false)));
        Assert.ThrowsExactly<ManagedAiException>(() => exchange.Accept(Page(3, "repeat", Entry("second", false))));
        foreach (var id in new[] { "--model", "model\nsecret", "a b", "", new string('x', 129) })
            Assert.AreEqual(AiFailureCode.InvalidModel, Assert.ThrowsExactly<ManagedAiException>(() => AiModel.ValidateId(id)).Code);
    }

    [TestMethod]
    public async Task ModelCatalogUsesIsolatedBoundedReadOnlyExchangeAndCleansItsDirectory()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new ModelRunner();
        var catalog = new OpenAiCodexModelCatalog(fixture.Paths, fixture.Installer, new WindowsManagedAiProfile(fixture.Paths), runner);
        Assert.AreEqual("fixture-model", (await catalog.ListAsync(CancellationToken.None)).Single().Id);
        var spec = runner.Invocation!;
        CollectionAssert.AreEqual(new[] { "--strict-config", "app-server", "--listen", "stdio://" }, spec.StartInfo.ArgumentList.ToArray());
        Assert.AreEqual(fixture.Paths.Profile, spec.StartInfo.Environment["CODEX_HOME"]);
        Assert.IsTrue(spec.Timeout <= TimeSpan.FromSeconds(30));
        Assert.IsFalse(Directory.Exists(spec.StartInfo.WorkingDirectory));
        runner.Fail = true;
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => catalog.ListAsync(CancellationToken.None));
        Assert.HasCount(0, Directory.GetDirectories(fixture.Paths.Runs));
    }

    private static object Entry(string model, bool hidden, string[]? modalities = null, bool isDefault = false)
        => new { model, displayName = model, hidden, inputModalities = modalities ?? ["text"], isDefault };
    private static string Page(int id, string? cursor, params object[] entries)
        => JsonSerializer.Serialize(new { id, result = new { data = entries, nextCursor = cursor } });
    private sealed class ModelRunner : IJsonlProcessRunner
    {
        public ProcessRunSpec? Invocation;
        public bool Fail;
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken token)
        {
            Invocation = spec;
            Assert.AreEqual(CodexModelListExchange.Initialize, spec.Input);
            Assert.Contains("model/list", spec.Respond!("{\"id\":1,\"result\":{}}")!.Text!);
            if (Fail) throw new ManagedAiException(AiFailureCode.ProviderTimeout);
            Assert.IsTrue(spec.Respond(Page(2, null, Entry("fixture-model", false, isDefault: true)))!.Close);
            return Task.FromResult(new ProcessOutcome(0, 100, 0));
        }
    }
}
