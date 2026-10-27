using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.Json;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Test;

// The executor is injected: these checks establish request validation, not native isolation.
[TestClass]
public sealed class GenericSkillExecutionTests
{
    [TestMethod]
    [DataRow("https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/", true)]
    [DataRow("https://k-skill-proxy.nomadamas.org/v1/kosis/search?searchNm=test", true)]
    [DataRow("http://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/", false)]
    [DataRow("https://ecos.bok.or.kr:444/api/KeyStatisticList/sample/json/kr/1/1/", false)]
    [DataRow("https://ecos.bok.or.kr/api/KeyStatisticList/private-key/json/kr/1/1/", false)]
    [DataRow("https://ecos.bok.or.kr/api/OtherService/sample/json/kr/1/1/", false)]
    [DataRow("https://ecos.bok.or.kr/other", false)]
    [DataRow("https://ecos.bok.or.kr@127.0.0.1/api/KeyStatisticList/sample/json/kr/1/1/", false)]
    [DataRow("https://ecos.bok.or.kr.evil.example/api/KeyStatisticList/sample/json/kr/1/1/", false)]
    [DataRow("https://k-skill-proxy.nomadamas.org/v1/kosis/admin", false)]
    [DataRow("https://k-skill-proxy.nomadamas.org/v1/kosis/%2fsearch", false)]
    [DataRow("https://k-skill-proxy.nomadamas.org/v1/kosis/search#fragment", false)]
    public void PublicApiBridgeAcceptsOnlyReviewedHostsRoutesAndProtocols(string url, bool accepted)
        => Assert.AreEqual(accepted, SkillNetworkBridge.TryValidateUrl(url, SkillNetworkBridge.KnownHosts, out _));

    [TestMethod]
    [DataRow("127.0.0.1", false)]
    [DataRow("10.2.3.4", false)]
    [DataRow("172.16.0.1", false)]
    [DataRow("192.168.1.1", false)]
    [DataRow("169.254.169.254", false)]
    [DataRow("100.64.0.1", false)]
    [DataRow("192.0.2.1", false)]
    [DataRow("198.18.0.1", false)]
    [DataRow("203.0.113.1", false)]
    [DataRow("::1", false)]
    [DataRow("::ffff:192.168.1.1", false)]
    [DataRow("fd00::1", false)]
    [DataRow("fe80::1", false)]
    [DataRow("2001:db8::1", false)]
    [DataRow("2002:c0a8:0101::", false)]
    [DataRow("1.1.1.1", true)]
    [DataRow("2606:4700:4700::1111", true)]
    public void PublicApiBridgeRejectsPrivateLoopbackLinkLocalAndReservedDnsAddresses(string address, bool accepted)
        => Assert.AreEqual(accepted, SkillNetworkBridge.IsPublicAddress(IPAddress.Parse(address)));

    [TestMethod]
    [DataRow("GET", "user-agent", true)]
    [DataRow("POST", "user-agent", false)]
    [DataRow("GET", "authorization", false)]
    [DataRow("GET", "cookie", false)]
    public async Task PublicApiRpcRejectsMethodsAndCredentialsBeforeTheOfflineBackend(string method, string header, bool accepted)
    {
        using var fixture = new InstallFixture();
        Directory.CreateDirectory(fixture.Paths.Root);
        var calls = 0;
        await using var bridge = new SkillNetworkBridge(fixture.Paths.Root, ["ecos.bok.or.kr"], (_, _) =>
        {
            calls++;
            return Task.FromResult(new SkillNetworkBridge.Reply(200, new Dictionary<string, string> { ["Content-Type"] = "application/json" }, Encoding.UTF8.GetBytes("{\"sample\":true}")));
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var service = bridge.RunAsync(cancellation.Token);
        try
        {
            using var response = await RpcAsync(bridge, new { id = 1, method,
                url = "https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/",
                headers = new Dictionary<string, string> { [header] = "test" } }, cancellation.Token);
            Assert.AreEqual(accepted ? 1 : 0, calls);
            Assert.AreEqual(!accepted, response.RootElement.TryGetProperty("error", out _));
            if (accepted)
                Assert.AreEqual("{\"sample\":true}", Encoding.UTF8.GetString(Convert.FromBase64String(response.RootElement.GetProperty("body").GetString()!)));
        }
        finally
        {
            cancellation.Cancel();
            try { await service; } catch (OperationCanceledException) { }
        }
    }

    [TestMethod]
    public async Task PublicApiRpcAllowsAtMostTwentyRequests()
    {
        using var fixture = new InstallFixture();
        Directory.CreateDirectory(fixture.Paths.Root);
        var calls = 0;
        await using var bridge = new SkillNetworkBridge(fixture.Paths.Root, ["ecos.bok.or.kr"], (_, _) =>
        {
            calls++;
            return Task.FromResult(new SkillNetworkBridge.Reply(200, new Dictionary<string, string>(), []));
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var service = bridge.RunAsync(cancellation.Token);
        try
        {
            for (var id = 1; id <= 21; id++)
            {
                using var response = await RpcAsync(bridge, new { id, method = "GET",
                    url = "https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/" }, cancellation.Token);
                Assert.AreEqual(id == 21, response.RootElement.TryGetProperty("error", out _));
            }
            Assert.AreEqual(20, calls);
        }
        finally
        {
            cancellation.Cancel();
            try { await service; } catch (OperationCanceledException) { }
        }
    }

    private static async Task<JsonDocument> RpcAsync(SkillNetworkBridge bridge, object payload, CancellationToken token)
    {
        await using (var request = new FileStream(Path.Combine(bridge.DirectoryPath, "request.json"), FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            await request.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(payload), token);
        var id = JsonSerializer.SerializeToElement(payload).GetProperty("id").GetInt32();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            await using var response = new FileStream(Path.Combine(bridge.DirectoryPath, "response.json"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var bytes = new MemoryStream();
            await response.CopyToAsync(bytes, token);
            try
            {
                var result = JsonDocument.Parse(bytes.ToArray());
                if (result.RootElement.GetProperty("id").GetInt32() == id) return result;
                result.Dispose();
            }
            catch (JsonException) { }
            await Task.Delay(10, token);
        }
    }

    [TestMethod]
    public async Task PythonPackageUsesFixedBootstrapAndSeparateArgumentsWithReadOnlyDependencies()
    {
        using var fixture = new PackageFixture();
        SandboxRunRequest? request = null;
        var registry = await fixture.Registry((supplied, _) =>
        {
            request = supplied;
            Assert.ThrowsExactly<IOException>(() => File.WriteAllText(fixture.Helper, "changed"));
            Assert.ThrowsExactly<IOException>(() => File.WriteAllText(fixture.Resource, "changed"));
            return Task.FromResult(Completed());
        });
        using var input = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            arguments = new[] { "search", "--query", "금리 and spaces", "-c", "literal argument", "" },
            stdin = "{\"operation\":\"bmi\"}\n"
        }));
        var result = await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None);
        Assert.AreEqual("completed", result.Status);
        Assert.IsNotNull(request);
        CollectionAssert.AreEqual(new[] { "-I", "-S", "-B", "-X", "utf8", "-c",
            SkillExecutionRegistry.PythonPackageBootstrap, fixture.Entry, fixture.Package,
            "search", "--query", "금리 and spaces", "-c", "literal argument", "" }, request.Arguments.ToArray());
        Assert.AreEqual("{\"operation\":\"bmi\"}\n", request.Stdin);
        CollectionAssert.AreEqual(new[] { fixture.Runtime, fixture.Package }, request.ReadOnlyDirectories.ToArray());
        CollectionAssert.AreEqual(new[] { request.WorkingDirectory }, request.ReadWriteDirectories.ToArray());
        Assert.IsFalse(request.WorkingDirectory.StartsWith(fixture.Package + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(request.AllowNetwork);
        Assert.AreEqual(1, request.MaxProcesses);
        Assert.AreEqual(60000, request.TimeoutMilliseconds);
        Assert.IsFalse(Directory.Exists(request.WorkingDirectory));
        File.WriteAllText(fixture.Helper, "locks released after execution");
    }

    [TestMethod]
    public async Task NodePackageUsesFixedEntryAndNoShellOrEvalOptions()
    {
        using var fixture = new PackageFixture("node-script");
        SandboxRunRequest? request = null;
        var registry = await fixture.Registry((supplied, _) => { request = supplied; return Task.FromResult(Completed()); });
        using var input = JsonDocument.Parse("{\"arguments\":[\"--eval\",\"literal\"]}");
        Assert.AreEqual("completed", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Status);
        Assert.IsNotNull(request);
        Assert.AreEqual(fixture.Node, request.Executable);
        CollectionAssert.AreEqual(new[] { "--preserve-symlinks-main", "--preserve-symlinks", fixture.Entry, "--eval", "literal" }, request.Arguments.ToArray());
        CollectionAssert.AreEqual(new[] { fixture.Runtime, fixture.Package }, request.ReadOnlyDirectories.ToArray());
        CollectionAssert.AreEqual(new[] { request.WorkingDirectory }, request.ReadWriteDirectories.ToArray());
        Assert.AreEqual("", request.Stdin);
        Assert.IsFalse(request.AllowNetwork);
    }

    [TestMethod]
    public async Task EmptyPythonPackageMarkerAndResourceAreVerifiedAndLockedAsDataFiles()
    {
        using var fixture = new PackageFixture();
        var module = Path.Combine(fixture.Package, "scripts", "local_package");
        Directory.CreateDirectory(module);
        var marker = Path.Combine(module, "__init__.py");
        File.WriteAllText(marker, "");
        File.WriteAllText(Path.Combine(module, "helper.py"), "VALUE = 17\n");
        File.WriteAllText(fixture.Entry, "from local_package import helper\nprint(helper.VALUE)\n");
        var registry = await fixture.Registry((request, _) =>
        {
            Assert.AreEqual(SkillExecutionRegistry.PythonPackageBootstrap, request.Arguments[6]);
            Assert.ThrowsExactly<IOException>(() => File.WriteAllText(marker, "changed package marker"));
            Assert.ThrowsExactly<IOException>(() => File.WriteAllText(fixture.Resource, "changed data"));
            return Task.FromResult(Completed());
        });
        using var input = JsonDocument.Parse("{}");
        Assert.AreEqual("completed", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Status);
        Assert.AreEqual(0L, new FileInfo(marker).Length);
        Assert.AreEqual(0L, new FileInfo(fixture.Resource).Length);
    }

    [TestMethod]
    [DataRow("entry")]
    [DataRow("python")]
    [DataRow("node")]
    public async Task EmptyEntryPointsAndInterpreterExecutablesRemainRejected(string component)
    {
        using var fixture = new PackageFixture();
        File.WriteAllText(component switch { "entry" => fixture.Entry, "python" => fixture.Python, _ => fixture.Node }, "");
        fixture.WriteManifest();
        await Assert.ThrowsExactlyAsync<BrokerValidationException>(() => SkillExecutionRegistry.LoadAsync(fixture.Manifest, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReviewedPythonNetworkRegistrationStillLaunchesWithoutInternetCapability()
    {
        using var fixture = new PackageFixture();
        fixture.WriteManifest(network: true, hosts: ["ecos.bok.or.kr"]);
        var registry = await SkillExecutionRegistry.LoadAsync(fixture.Manifest, (request, _) =>
        {
            Assert.IsFalse(request.AllowNetwork);
            Assert.AreEqual(SkillExecutionRegistry.PythonNetworkPackageBootstrap, request.Arguments[6]);
            Assert.AreEqual(fixture.Entry, request.Arguments[7]);
            Assert.AreEqual(fixture.Package, request.Arguments[8]);
            Assert.IsTrue(Directory.Exists(request.Arguments[9]));
            Assert.IsTrue(File.Exists(Path.Combine(request.Arguments[9], "request.json")));
            Assert.ThrowsExactly<IOException>(() => File.Delete(Path.Combine(request.Arguments[9], "request.json")));
            return Task.FromResult(Completed());
        }, CancellationToken.None);
        using var input = JsonDocument.Parse("{}");
        Assert.AreEqual("completed", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Status);
    }

    [TestMethod]
    public async Task ReviewedNodeNetworkRegistrationKeepsEntryAndArgumentsSeparateWithoutInternetCapability()
    {
        using var fixture = new PackageFixture("node-script");
        fixture.WriteManifest(network: true, hosts: ["ecos.bok.or.kr"]);
        var registry = await SkillExecutionRegistry.LoadAsync(fixture.Manifest, (request, _) =>
        {
            Assert.IsFalse(request.AllowNetwork);
            Assert.AreEqual(fixture.Node, request.Executable);
            CollectionAssert.AreEqual(new[] { "--preserve-symlinks-main", "--preserve-symlinks", "--eval",
                SkillExecutionRegistry.NodeNetworkPackageBootstrap, "--", fixture.Entry,
                Path.Combine(request.WorkingDirectory, "network"), "--eval", "literal entry argument" }, request.Arguments.ToArray());
            Assert.IsLessThanOrEqualTo(8192, SkillExecutionRegistry.NodeNetworkPackageBootstrap.Length);
            return Task.FromResult(Completed());
        }, CancellationToken.None);
        using var input = JsonDocument.Parse("{\"arguments\":[\"--eval\",\"literal entry argument\"]}");
        Assert.AreEqual("completed", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Status);
    }

    [TestMethod]
    [DataRow("python-script")]
    [DataRow("node-script")]
    public async Task NetworkDeclarationDoesNotGrantUnrestrictedInternetAccess(string kind)
    {
        using var fixture = new PackageFixture(kind);
        var calls = 0;
        var registry = await fixture.Registry((_, _) => { calls++; return Task.FromResult(Completed()); }, network: true);
        using var input = JsonDocument.Parse("{}");
        Assert.AreEqual("network-policy", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Failure);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow("{\"command\":\"python other.py\"}")]
    [DataRow("{\"scriptPath\":\"other.py\"}")]
    [DataRow("{\"requiresNetwork\":true}")]
    [DataRow("{\"arguments\":\"--help\"}")]
    [DataRow("{\"arguments\":[17]}")]
    [DataRow("{\"arguments\":[\"bad\\u0000argument\"]}")]
    [DataRow("{\"stdin\":{\"operation\":\"bmi\"}}")]
    [DataRow("{\"arguments\":[],\"arguments\":[\"--help\"]}")]
    public async Task CallerCannotReplaceScriptInterpreterOrPolicy(string text)
    {
        using var fixture = new PackageFixture();
        var calls = 0;
        var registry = await fixture.Registry((_, _) => { calls++; return Task.FromResult(Completed()); });
        using var input = JsonDocument.Parse(text);
        Assert.AreEqual("invalid-input", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Failure);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow("count")]
    [DataRow("length")]
    [DataRow("total")]
    [DataRow("stdin")]
    public async Task ArgumentAndInputBudgetsApplyBeforeExecution(string budget)
    {
        using var fixture = new PackageFixture();
        var calls = 0;
        var registry = await fixture.Registry((_, _) => { calls++; return Task.FromResult(Completed()); });
        var arguments = budget switch
        {
            "count" => Enumerable.Repeat("x", 65).ToArray(),
            "length" => new[] { new string('x', 4097) },
            "total" => Enumerable.Repeat(new string('x', 4096), 6).ToArray(),
            _ => Array.Empty<string>()
        };
        using var input = JsonDocument.Parse(JsonSerializer.Serialize(new { arguments, stdin = budget == "stdin" ? new string('가', 22000) : "" }));
        Assert.AreEqual("invalid-input", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Failure);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    [DataRow("changed")]
    [DataRow("added")]
    [DataRow("removed")]
    public async Task WholePackageIntegrityIsRecheckedBeforeExecution(string change)
    {
        using var fixture = new PackageFixture();
        var calls = 0;
        var registry = await fixture.Registry((_, _) => { calls++; return Task.FromResult(Completed()); });
        if (change == "changed") File.WriteAllText(fixture.Helper, "changed after manifest load");
        else if (change == "added") File.WriteAllText(Path.Combine(fixture.Package, "unregistered.py"), "new import source");
        else File.Delete(fixture.Resource);
        using var input = JsonDocument.Parse("{}");
        Assert.AreEqual("registration-integrity", (await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None)).Failure);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task EntryCannotEscapeItsReadOnlyPackage()
    {
        using var fixture = new PackageFixture();
        var outside = fixture.Paths.Under("outside.py");
        File.WriteAllText(outside, "# outside package");
        fixture.WriteManifest(entry: outside);
        await Assert.ThrowsExactlyAsync<BrokerValidationException>(() => SkillExecutionRegistry.LoadAsync(fixture.Manifest, CancellationToken.None));
    }

    [TestMethod]
    public async Task UnregisteredPackageFilesAreRejectedBeforeAdvertisingTools()
    {
        using var fixture = new PackageFixture();
        fixture.WriteManifest();
        File.WriteAllText(Path.Combine(fixture.Package, "extra.py"), "# not in manifest");
        await Assert.ThrowsExactlyAsync<BrokerValidationException>(() => SkillExecutionRegistry.LoadAsync(fixture.Manifest, CancellationToken.None));
    }

    [TestMethod]
    public async Task GenericExecutionStillRequiresNativeAppContainerEvidence()
    {
        using var fixture = new PackageFixture();
        var registry = await fixture.Registry((_, _) => Task.FromResult(new SandboxRunResult(0, "untrusted", "", false, false, false)));
        using var input = JsonDocument.Parse("{}");
        var result = await registry.ExecuteAsync("fixture", input.RootElement, CancellationToken.None);
        Assert.AreEqual("appcontainer-required", result.Failure);
        Assert.AreEqual("", result.Stdout);
    }

    [TestMethod]
    public async Task GenericToolSchemaDescribesBoundedScriptArgumentsAndStdin()
    {
        using var fixture = new PackageFixture();
        var registry = await fixture.Registry((_, _) => Task.FromResult(Completed()));
        var messages = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}}\n" +
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}\n" +
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}\n";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(messages));
        using var output = new MemoryStream();
        Assert.AreEqual(0, await SkillMcpBroker.RunAsync(registry, input, output, CancellationToken.None));
        var responses = Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        using var response = JsonDocument.Parse(responses[1]);
        var tool = response.RootElement.GetProperty("result").GetProperty("tools")[0];
        Assert.AreEqual("fixture", tool.GetProperty("name").GetString());
        Assert.Contains("Fixture registered script", tool.GetProperty("description").GetString()!);
        var schema = tool.GetProperty("inputSchema");
        Assert.IsFalse(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.AreEqual(64, schema.GetProperty("properties").GetProperty("arguments").GetProperty("maxItems").GetInt32());
        Assert.AreEqual("string", schema.GetProperty("properties").GetProperty("stdin").GetProperty("type").GetString());
    }

    private static SandboxRunResult Completed() => new(0, "script output", "", false, false, true);

    private sealed class PackageFixture : IDisposable
    {
        private readonly InstallFixture _fixture = new();
        private readonly string _kind;
        internal ManagedAiPaths Paths => _fixture.Paths;
        internal string Package { get; }
        internal string Runtime { get; }
        internal string Entry { get; }
        internal string Helper { get; }
        internal string Resource { get; }
        internal string Python { get; }
        internal string Node { get; }
        internal string Manifest { get; }

        internal PackageFixture(string kind = "python-script")
        {
            _kind = kind;
            Package = Paths.Under("package");
            Runtime = Paths.Under("runtime");
            Directory.CreateDirectory(Path.Combine(Package, "scripts"));
            Directory.CreateDirectory(Runtime);
            Entry = Path.Combine(Package, "scripts", kind == "node-script" ? "entry.cjs" : "entry.py");
            Helper = Path.Combine(Package, "scripts", "helper.py");
            Resource = Path.Combine(Package, "empty-resource.txt");
            Python = Path.Combine(Runtime, "python.exe");
            Node = Path.Combine(Runtime, "node.exe");
            Manifest = Paths.Under("manifest.json");
            File.WriteAllText(Entry, "fixture entry, never executed");
            File.WriteAllText(Helper, "fixture imported helper, never executed");
            File.WriteAllText(Resource, "");
            File.WriteAllText(Python, "fixture interpreter, never launched");
            File.WriteAllText(Node, "fixture interpreter, never launched");
        }

        internal async Task<SkillExecutionRegistry> Registry(Func<SandboxRunRequest, CancellationToken, Task<SandboxRunResult>> executor,
            bool network = false)
        {
            WriteManifest(network);
            return await SkillExecutionRegistry.LoadAsync(Manifest, executor, CancellationToken.None);
        }

        internal void WriteManifest(bool network = false, string? entry = null, string[]? hosts = null)
        {
            static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            var script = entry ?? Entry;
            File.WriteAllText(Manifest, JsonSerializer.Serialize(new
            {
                ownedRoot = Paths.Root, pythonExecutable = Python, pythonSha256 = Hash(Python),
                nodeExecutable = Node, nodeSha256 = Hash(Node),
                skills = new[] { new { skillId = "fixture", kind = _kind, scriptPath = script, sha256 = Hash(script),
                    packageRoot = Package, requiresNetwork = network, description = "Fixture registered script",
                    networkHosts = hosts ?? [],
                    packageFiles = Directory.EnumerateFiles(Package, "*", SearchOption.AllDirectories)
                        .Select(path => new { path, sha256 = Hash(path) }).ToArray() } }
            }));
        }

        public void Dispose() => _fixture.Dispose();
    }
}
