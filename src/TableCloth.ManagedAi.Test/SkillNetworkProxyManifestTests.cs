using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillNetworkProxyManifestTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task TrustedManifestAcceptsOnlyTheExplicitProxyBoolean(bool enabled)
    {
        using var fixture = new ManifestFixture();
        fixture.Json["useSystemProxy"] = enabled;
        fixture.Save();

        var manifest = await BrokerFiles.ReadManifestAsync(fixture.Path, CancellationToken.None);

        Assert.AreEqual(enabled, manifest.UseSystemProxy);
    }

    [TestMethod]
    public async Task OlderManifestKeepsDirectNetworkPolicy()
    {
        using var fixture = new ManifestFixture();
        fixture.Save();

        var manifest = await BrokerFiles.ReadManifestAsync(fixture.Path, CancellationToken.None);

        Assert.IsFalse(manifest.UseSystemProxy);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("1")]
    [DataRow("\"true\"")]
    [DataRow("{}")]
    [DataRow("[]")]
    public async Task ManifestRejectsNonBooleanProxyValues(string json)
    {
        using var fixture = new ManifestFixture();
        fixture.Json["useSystemProxy"] = JsonNode.Parse(json);
        fixture.Save();

        await Assert.ThrowsAsync<BrokerValidationException>(() => BrokerFiles.ReadManifestAsync(fixture.Path, CancellationToken.None));
    }

    [TestMethod]
    [DataRow("proxyUrl")]
    [DataRow("proxyCredentials")]
    [DataRow("certificateChainPolicy")]
    [DataRow("resolve")]
    public async Task ManifestCannotSupplyTransportEndpointsCredentialsOrTestHooks(string property)
    {
        using var fixture = new ManifestFixture();
        fixture.Json[property] = "worker-selected-value";
        fixture.Save();

        await Assert.ThrowsAsync<BrokerValidationException>(() => BrokerFiles.ReadManifestAsync(fixture.Path, CancellationToken.None));
    }

    private sealed class ManifestFixture : IDisposable
    {
        private readonly InstallFixture _fixture = new();
        internal string Path { get; }
        internal JsonObject Json { get; }

        internal ManifestFixture()
        {
            var root = _fixture.Paths.Under("skill-execution");
            Directory.CreateDirectory(root);
            var python = System.IO.Path.Combine(root, "python.exe");
            var script = System.IO.Path.Combine(root, "fixture.py");
            File.WriteAllText(python, "fixture interpreter, never executed");
            File.WriteAllText(script, "fixture source, never executed");
            static string Hash(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
            Path = System.IO.Path.Combine(root, "manifest.json");
            Json = JsonSerializer.SerializeToNode(new
            {
                ownedRoot = root, pythonExecutable = python, pythonSha256 = Hash(python),
                skills = new[] { new { skillId = "fixture", kind = "python-json-stdin", scriptPath = script, sha256 = Hash(script) } }
            })!.AsObject();
        }

        internal void Save() => File.WriteAllText(Path, Json.ToJsonString());
        public void Dispose() => _fixture.Dispose();
    }
}
