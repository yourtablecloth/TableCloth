using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TableCloth.App.DependencyInjection;
using TableCloth.AppContainerPoc;
using TableCloth.Components;
using TableCloth.ManagedAi;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;
using TableCloth.SkillSmokeDiagnostics;

// This managed diagnostic parent uses the product DI composition. MCP children are
// the actual TableCloth.exe resolved by its real ISharedLocations implementation.
if (args.Length is 2 or 4 && args[0] == "--verify")
{
    if (args.Length == 4 && args[2] != "--expected-product-sha256")
        throw new ArgumentException("Use --verify <evidence-directory> [--expected-product-sha256 <trusted-sha256>].");
    var verified = EvidenceVerifier.Verify(args[1], args.Length == 4 ? args[3] : null);
    Console.WriteLine(JsonSerializer.Serialize(verified, new JsonSerializerOptions { WriteIndented = true }));
    Environment.ExitCode = verified.Passed ? 0 : 1;
    return;
}
var options = ProbeOptions.Parse(args);
var evidence = Path.Combine(AppContext.BaseDirectory, "evidence", DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(evidence);
using var log = new EvidenceLog(Path.Combine(evidence, "probe.jsonl"));
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancel.Cancel(); };
var token = cancel.Token;
var capture = new CaptureSession(evidence, log);
var builder = Host.CreateApplicationBuilder();
builder.UseTableCloth();
builder.Services.AddSingleton(capture);
builder.Services.AddSingleton<IJsonlProcessRunner, CapturingRunner>();
using var host = builder.Build();
var services = host.Services;
var paths = services.GetRequiredService<ManagedAiPaths>();
var manager = services.GetRequiredService<OpenAiCodexSkillManager>();
var execution = services.GetRequiredService<IManagedAiSkillExecution>();
var locations = services.GetRequiredService<ISharedLocations>();
var stage = "composition";
var watch = Stopwatch.StartNew();
var probeCompleted = false;
try
{
    log.Record("start", new
    {
        parentProcess = Environment.ProcessPath,
        parentFramework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        appOwnedRoot = paths.Root,
        appOwnedProfile = paths.Profile,
        executable = locations.ExecutableFilePath,
        model = options.Model,
        options.Discover,
        options.Direct,
        options.NodeFixture,
        options.ChatSkill,
        options.InstallCatalog,
        options.InstallRuntimeVersion,
        options.CalculatorCases,
        authenticationCopied = false,
        globalConfigurationChanged = false,
        sharedRuntimeManager = ReferenceEquals(services.GetRequiredService<WindowsManagedAiScriptRuntimeManager>(),
            services.GetRequiredService<IManagedAiScriptRuntimeManager>()),
        executionType = execution.GetType().FullName
    });
    if (!File.Exists(locations.ExecutableFilePath) || !Path.GetFileName(locations.ExecutableFilePath).Equals("TableCloth.exe", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("The product DI host must resolve an existing TableCloth.exe beside the probe output.");
    ManagedAiPaths.RejectReparsePoints(locations.ExecutableFilePath);
    if (options.NativePath is not null && !Path.GetFullPath(options.NativePath).Equals(Path.GetFullPath(locations.ExecutableFilePath), StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("--nativepath must match the real product ISharedLocations executable. Copy the chosen native product payload beside this probe before execution; no host resolver is replaced.");
    var productVersion = FileVersionInfo.GetVersionInfo(locations.ExecutableFilePath);
    log.Record("product-host", new
    {
        executable = locations.ExecutableFilePath,
        sha256 = await HashFileAsync(locations.ExecutableFilePath, token),
        productVersion.FileVersion,
        productVersion.ProductVersion,
        explicitNativePathMatched = options.NativePath is not null,
        note = "Native/AOT provenance is supplied by the caller's chosen build, not inferred from this managed parent's framework."
    });
    if (options.InstallRuntimeVersion is not null)
    {
        stage = "explicit-app-owned-runtime-install";
        var installedRuntime = await services.GetRequiredService<IManagedRuntimeManager>().InstallAsync(
            options.InstallRuntimeVersion, null, token);
        log.Record("explicit-app-owned-runtime-installed", new { version = installedRuntime.Coordinate.Version,
            executable = installedRuntime.EntryPoint, profile = installedRuntime.ProfileDirectory });
    }
    stage = "native-skill-discovery";
    var skills = await manager.ListAsync(token);
    WriteSkills(log, "installed-before", skills);
    if (options.InstallCatalog)
    {
        stage = "normal-marketplace-install";
        var marketplace = services.GetRequiredService<IManagedAiSkillMarketplace>();
        var entries = await marketplace.ListAsync(token);
        var required = SkillCases.ActiveIds;
        if (required.Any(id => !entries.Any(entry => entry.Id == id && entry.CanInstall)))
            throw new InvalidOperationException("The published marketplace does not contain every expected active pinned skill.");
        foreach (var id in required)
        {
            if (skills.Any(skill => skill.Id == id))
            {
                log.Record("catalog-install-existing-preserved", new { id });
                continue;
            }
            skills = await marketplace.InstallAsync(id, token);
            log.Record("catalog-installed", new { id, method = "IManagedAiSkillMarketplace.InstallAsync", persistsAfterProbe = true });
        }
        WriteSkills(log, "installed-after-explicit-install", skills);
    }
    var runtime = await services.GetRequiredService<IManagedRuntimeManager>().GetActiveAsync(token);
    log.Record("codex-runtime", runtime is null ? new { installed = false } : new
    {
        installed = true,
        version = runtime.Coordinate.Version,
        executable = runtime.EntryPoint,
        profile = runtime.ProfileDirectory,
        existingAppOwnedProfile = runtime.ProfileDirectory.Equals(paths.Profile, StringComparison.OrdinalIgnoreCase)
    });
    var configFile = Path.Combine(paths.Profile, "config.toml");
    var config = File.Exists(configFile) ? await File.ReadAllTextAsync(configFile, token) : "";
    var policyValid = config.Contains("shell_tool = false", StringComparison.Ordinal) &&
        config.Contains("unified_exec = false", StringComparison.Ordinal);
    log.Record("profile-policy", new { shellToolEnabled = false, unifiedExecEnabled = false, actualConfigContainsDisabledFeatures = policyValid });
    if (!policyValid) throw new InvalidOperationException("The app-owned profile does not contain the expected disabled shell/unified settings.");

    if (options.Discover)
    {
        stage = "production-bridge-discovery";
        var discovery = await WithBridgeAsync(skills, async bridge =>
        {
            var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], token);
            await SaveToolsAsync(registry, "discovery", token);
            log.Record("production-bridge-discovery", new
            {
                bridge.Command,
                bridge.SkillIds,
                registrySkills = registry.Skills.Select(s => new { s.SkillId, s.Kind, s.ScriptPath, s.RequiresNetwork, s.Description }),
                sameHostAsProductDi = bridge.Command.Equals(locations.ExecutableFilePath, StringComparison.OrdinalIgnoreCase)
            });
            return true;
        });
        if (!discovery) throw new InvalidOperationException("No executable skill bridge was created.");
    }
    if (options.Direct)
    {
        stage = "direct-production-registry";
        var chosen = SkillCases.ScriptIds.Select(id => RequiredInstalled(skills, id)).ToArray();
        if (!await WithBridgeAsync(chosen, async bridge =>
        {
            var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], token);
            await SaveToolsAsync(registry, "direct", token);
            var failures = new List<string>();
            foreach (var id in SkillCases.ScriptIds)
            {
                var registration = FindRegistration(registry, id);
                var input = SkillCases.DirectInput(id, registration.Kind);
                var result = await registry.ExecuteAsync(registration.SkillId, input, token);
                var passed = SkillCases.ValidateScriptResult(id, result);
                await SaveJsonAsync(Path.Combine(evidence, id + ".direct.json"), new { skillId = id, registrationId = registration.SkillId, input, passed, result }, token);
                log.Record("direct-script", new { id, registrationId = registration.SkillId, passed, result.Status, result.ExitCode, result.WasAppContainer, result.Failure });
                if (!passed) failures.Add(id);
            }
            if (options.CalculatorCases is not null && !await RunCalculatorCasesAsync(registry, options.CalculatorCases, token))
                failures.Add("calculator-cases");
            if (failures.Count != 0) throw new InvalidOperationException("Direct contained upstream invocations failed: " + string.Join(", ", failures));
            return true;
        })) throw new InvalidOperationException("No script bridge was created for the three installed script skills.");
    }
    if (options.NodeFixture)
    {
        stage = "separate-node-fixture";
        var fixtureId = "node-runtime-fixture-" + Guid.NewGuid().ToString("N")[..12];
        var fixtureDirectory = paths.Under("profiles", "openai-codex", "codex-home", "skills", fixtureId);
        Directory.CreateDirectory(Path.Combine(fixtureDirectory, "scripts"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, "SKILL.md"),
                "---\nname: " + fixtureId + "\ndescription: Synthetic contained Node.js JSON stdin fixture; not a marketplace skill.\n---\nRun scripts/sum.cjs with JSON stdin.\n", new UTF8Encoding(false), token);
            await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, "scripts", "sum.cjs"),
                "'use strict';\nlet text=''; process.stdin.setEncoding('utf8'); process.stdin.on('data', x => { text += x; if (text.length > 4096) process.exit(2); }); process.stdin.on('end', () => { const x=JSON.parse(text); if (!Number.isFinite(x.a)||!Number.isFinite(x.b)) process.exit(2); console.log(JSON.stringify({sum:x.a+x.b,node:process.versions.node})); });\n", new UTF8Encoding(false), token);
            var fixture = new AiSkill(fixtureId, fixtureId, "Synthetic Node runtime fixture, not an upstream catalog skill.", fixtureDirectory, true);
            if (!await WithBridgeAsync([fixture], async bridge =>
            {
                var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], token);
                await SaveToolsAsync(registry, "node-fixture", token);
                var registration = registry.Skills.Single(s => Path.GetFileName(s.ScriptPath) == "sum.cjs");
                var input = JsonSerializer.SerializeToElement(new { arguments = Array.Empty<string>(), stdin = "{\"a\":12,\"b\":30}" });
                var result = await registry.ExecuteAsync(registration.SkillId, input, token);
                var passed = result.Status == "completed" && result.WasAppContainer && result.ExitCode == 0 &&
                    JsonDocument.Parse(result.Stdout).RootElement.GetProperty("sum").GetInt32() == 42;
                await SaveJsonAsync(Path.Combine(evidence, "node-fixture.direct.json"), new { fixtureId, registrationId = registration.SkillId, passed, result, isCatalogSkill = false }, token);
                log.Record("synthetic-node-fixture", new { fixtureId, registrationId = registration.SkillId, passed, result.Status, result.ExitCode, result.WasAppContainer, result.Failure, isCatalogSkill = false });
                if (!passed) throw new InvalidOperationException("The synthetic Node fixture failed.");
                return true;
            })) throw new InvalidOperationException("No bridge was created for the synthetic Node fixture.");
        }
        finally
        {
            paths.DeleteOwnedDirectory(fixtureDirectory);
            log.Record("synthetic-node-fixture-removed", new { fixtureId });
        }
    }
    if (options.ChatSkill is not null)
    {
        stage = "public-pinned-instruction-context";
        var publicContext = await InstalledSkillPromptContext.CreateAsync(skills,
            services.GetRequiredService<IHttpClientFactory>(), token);
        var marker = "UNTRUSTED_PUBLIC_SKILL_DOCUMENTS:\n";
        var markerOffset = publicContext.IndexOf(marker, StringComparison.Ordinal);
        if (markerOffset < 0) throw new InvalidOperationException("Public skill document context is missing.");
        using (var contextDocument = JsonDocument.Parse(publicContext[(markerOffset + marker.Length)..]))
        {
            var contextRoot = contextDocument.RootElement;
            log.Record("public-pinned-instructions", new
            {
                truncated = contextRoot.GetProperty("truncated").GetBoolean(),
                omittedDocuments = contextRoot.GetProperty("omittedDocuments").GetInt32(),
                documents = contextRoot.GetProperty("documents").EnumerateArray().Select(document => new
                {
                    skillId = document.GetProperty("skillId").GetString(),
                    file = document.GetProperty("file").GetString(),
                    bytes = Encoding.UTF8.GetByteCount(document.GetProperty("text").GetString()!),
                    truncated = document.GetProperty("truncated").GetBoolean(),
                    bmiContractPresent = document.GetProperty("text").GetString()!.Contains("height_cm", StringComparison.Ordinal)
                }).ToArray()
            });
        }
        var targets = options.ChatSkill == "all" ? SkillCases.ActiveIds : [options.ChatSkill];
        var chatFailures = new List<string>();
        foreach (var id in targets)
        {
            stage = "real-codex-chat:" + id;
            capture.Begin(id);
            var scriptRuntime = services.GetRequiredService<IManagedAiScriptExecutionRuntime>();
            using var processObserver = new ProcessEvidenceObserver(Environment.ProcessId,
                locations.ExecutableFilePath, scriptRuntime.PythonExecutable, scriptRuntime.NodeExecutable);
            processObserver.Start();
            var request = SkillCases.ChatRequest(id, options.Model);
            log.Record("chat-request", new
            {
                id,
                request.Model,
                request.Message,
                certificateReportIsSynthetic = request.LocalCertificateReport is not null,
                sandboxReportIsSynthetic = request.LocalWindowsSandboxReport is not null,
                realPrivateCertificateScan = false,
                realWindowsSandboxOperation = false
            });
            try
            {
                RequiredInstalled(skills, id);
                var response = await services.GetRequiredService<IManagedAiChatProvider>().ChatAsync(request, null, token);
                var proof = SkillCases.InspectCapture(id, capture.CurrentPath!);
                await SaveJsonAsync(Path.Combine(evidence, id + ".chat.json"), new { id, request, response, proof }, token);
                log.Record("chat-result", new { id, proof.Passed, proof.EvidenceType, proof.SuccessfulContainedCalls, proof.ExpectedScriptResultObserved, proof.TurnCompleted, response.SearchCalls });
                if (!proof.Passed) chatFailures.Add(id);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                chatFailures.Add(id);
                log.Record("chat-failure", new
                {
                    id,
                    exceptionType = error.GetType().FullName,
                    failureCode = error is ManagedAiException managed ? managed.Code.ToString() : null,
                    message = error is InvalidOperationException ? error.Message : null
                });
            }
            finally
            {
                var observations = processObserver.Stop();
                await SaveJsonAsync(Path.Combine(evidence, id + ".processes.json"), observations, token);
                capture.End();
            }
        }
        if (chatFailures.Count != 0) throw new InvalidOperationException("The required actual chat evidence failed: " + string.Join(", ", chatFailures));
    }
    WriteSkills(log, "installed-final", await manager.ListAsync(token));
    log.Record("completed", new { passed = true, elapsedMilliseconds = watch.ElapsedMilliseconds, evidenceDirectory = evidence });
    probeCompleted = true;
}
catch (Exception error)
{
    log.Record("failure", new
    {
        stage,
        exceptionType = error.GetType().FullName,
        failureCode = error is ManagedAiException managed ? managed.Code.ToString() : null,
        message = error is InvalidOperationException ? error.Message : null,
        error.HResult,
        elapsedMilliseconds = watch.ElapsedMilliseconds,
        evidenceDirectory = evidence
    });
    Environment.ExitCode = 1;
}

if (probeCompleted && options.ChatSkill == "all")
{
    // Close the producer before the independent, read-only reader takes its snapshot.
    log.Dispose();
    var independentlyVerified = EvidenceVerifier.Verify(evidence, await HashFileAsync(locations.ExecutableFilePath, token));
    await SaveJsonAsync(Path.Combine(evidence, "verification.json"), independentlyVerified, token);
    Console.WriteLine(JsonSerializer.Serialize(new { independentVerificationPassed = independentlyVerified.Passed,
        independentlyVerified.Failures, evidenceDirectory = evidence }));
    if (!independentlyVerified.Passed) Environment.ExitCode = 1;
}

async Task<bool> WithBridgeAsync(IReadOnlyList<AiSkill> available, Func<AiSkillExecutionBridge, Task<bool>> operation)
{
    var run = paths.Under("runs", "all-skills-probe-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(run);
    try
    {
        using var bridge = await execution.PrepareAsync(available, run, token);
        if (bridge is not null)
            File.Copy(bridge.Arguments[1], Path.Combine(evidence, "runtime-seed-manifest.json"), overwrite: true);
        return bridge is not null && await operation(bridge);
    }
    finally { paths.DeleteOwnedDirectory(run); }
}

async Task<bool> RunCalculatorCasesAsync(SkillExecutionRegistry registry, string datasetPath, CancellationToken cancellationToken)
{
    stage = "contained-calculator-all-operations";
    datasetPath = Path.GetFullPath(datasetPath);
    ManagedAiPaths.RejectReparsePoints(datasetPath);
    if (!File.Exists(datasetPath) || new FileInfo(datasetPath).Length is < 1 or > 1048576)
        throw new InvalidOperationException("The calculator dataset must be an existing JSON file of at most 1 MiB.");
    var datasetBytes = await File.ReadAllBytesAsync(datasetPath, cancellationToken);
    if (datasetBytes.Length > 1048576) throw new InvalidOperationException("The calculator dataset exceeds 1 MiB.");
    var datasetHash = Convert.ToHexStringLower(SHA256.HashData(datasetBytes));
    using var document = JsonDocument.Parse(datasetBytes, new JsonDocumentOptions { MaxDepth = 32 });
    var root = document.RootElement;
    var cases = root.GetProperty("cases");
    if (cases.ValueKind != JsonValueKind.Array || cases.GetArrayLength() != 22)
        throw new InvalidOperationException("The calculator dataset must contain exactly the 22 documented operations.");
    var registration = FindRegistration(registry, "tablecloth-calculators");
    var expectedScriptHash = root.GetProperty("source").GetProperty("sha256").GetString();
    if (!registration.Sha256.Equals(expectedScriptHash, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("The actual registered calculator differs from the pinned source for the expected-value dataset.");
    string[] expectedOperations = ["percent.part", "percent.share", "percent.change", "percent.adjust", "percent.points",
        "discount.single", "discount.sequential", "discount.reverse", "unit_price", "bill_split", "interest.simple",
        "interest.compound", "savings.recurring", "savings.target", "loan", "date.diff", "date.add", "date.age", "date.weekday",
        "time.convert", "unit.convert", "bmi"];
    var names = cases.EnumerateArray().Select(item => item.GetProperty("id").GetString()!).ToArray();
    if (names.Distinct(StringComparer.Ordinal).Count() != names.Length ||
        names.Any(name => !expectedOperations.Contains(name, StringComparer.Ordinal)))
        throw new InvalidOperationException("The calculator dataset has duplicate, missing or undocumented operation IDs.");
    var caseDirectory = Path.Combine(evidence, "calculator-cases");
    Directory.CreateDirectory(caseDirectory);
    await File.WriteAllBytesAsync(Path.Combine(caseDirectory, "input-dataset.json"), datasetBytes, cancellationToken);
    log.Record("calculator-cases-dataset", new
    {
        path = datasetPath,
        sha256 = datasetHash,
        bytes = datasetBytes.Length,
        actualCaseCount = cases.GetArrayLength(),
        registrationId = registration.SkillId,
        registeredScript = registration.ScriptPath,
        registeredScriptSha256 = registration.Sha256,
        datasetScriptSha256 = expectedScriptHash
    });
    var summaries = new List<CalculatorCaseSummary>();
    var index = 0;
    foreach (var item in cases.EnumerateArray())
    {
        cancellationToken.ThrowIfCancellationRequested();
        index++;
        var id = item.GetProperty("id").GetString()!;
        var input = item.GetProperty("input");
        var expectedResult = item.GetProperty("expectedResult");
        if (input.ValueKind != JsonValueKind.Object || input.GetProperty("operation").GetString() != id || expectedResult.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The calculator dataset has an invalid request or expected result: " + id);
        var mcpInput = registration.Kind == "python-json-stdin" ? input.Clone()
            : JsonSerializer.SerializeToElement(new { arguments = Array.Empty<string>(), stdin = input.GetRawText() });
        SkillExecutionResult? result = null;
        JsonElement? actualScriptResponse = null;
        var validBroker = false;
        var validEnvelope = false;
        var exactResult = false;
        string? failure = null;
        try
        {
            result = await registry.ExecuteAsync(registration.SkillId, mcpInput, cancellationToken);
            validBroker = result.Status == "completed" && result.ExitCode == 0 && result.WasAppContainer &&
                !result.TimedOut && !result.OutputLimited && result.Failure is null;
            if (validBroker)
            {
                using var output = JsonDocument.Parse(result.Stdout, new JsonDocumentOptions { MaxDepth = 32 });
                var response = output.RootElement;
                actualScriptResponse = response.Clone();
                validEnvelope = response.ValueKind == JsonValueKind.Object && response.GetProperty("ok").ValueKind == JsonValueKind.True &&
                    response.GetProperty("operation").GetString() == id && response.TryGetProperty("assumptions", out var assumptions) &&
                    assumptions.ValueKind == JsonValueKind.Array && assumptions.GetArrayLength() > 0 &&
                    assumptions.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()));
                exactResult = validEnvelope && response.TryGetProperty("result", out var actualResult) && JsonElement.DeepEquals(actualResult, expectedResult);
                if (!validEnvelope) failure = "invalid-script-envelope";
                else if (!exactResult) failure = "expected-result-mismatch";
            }
            else failure = result.Failure ?? "contained-execution-failed";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            failure = error is JsonException ? "invalid-script-json" : error is KeyNotFoundException or InvalidOperationException
                ? "invalid-script-shape" : "execution-exception:" + error.GetType().Name;
        }
        var passed = validBroker && validEnvelope && exactResult;
        var summary = new CalculatorCaseSummary(id, passed, validBroker, validEnvelope, exactResult, failure,
            result?.Status, result?.ExitCode, result?.WasAppContainer ?? false);
        summaries.Add(summary);
        await SaveJsonAsync(Path.Combine(caseDirectory, index.ToString("D2") + "-" + id + ".json"), new
        {
            datasetPath,
            datasetSha256 = datasetHash,
            registrationId = registration.SkillId,
            input,
            mcpInput,
            expectedResult,
            actualScriptResponse,
            summary,
            result
        }, cancellationToken);
        log.Record("calculator-operation", summary);
    }
    var allPassed = summaries.Count == cases.GetArrayLength() && summaries.All(summary => summary.Passed);
    var aggregate = new
    {
        datasetPath,
        datasetSha256 = datasetHash,
        actualCaseCount = cases.GetArrayLength(),
        executedCaseCount = summaries.Count,
        passedCaseCount = summaries.Count(summary => summary.Passed),
        failedCaseCount = summaries.Count(summary => !summary.Passed),
        allPassed,
        registrationId = registration.SkillId,
        cases = summaries
    };
    await SaveJsonAsync(Path.Combine(caseDirectory, "aggregate.json"), aggregate, cancellationToken);
    log.Record("calculator-cases-completed", aggregate);
    return allPassed;
}

async Task SaveToolsAsync(SkillExecutionRegistry registry, string label, CancellationToken cancellationToken)
{
    var requests = """
        {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"all-skills-production-di-probe","version":"1"}}}
        {"jsonrpc":"2.0","method":"notifications/initialized"}
        {"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}

        """;
    await using var input = new MemoryStream(Encoding.UTF8.GetBytes(requests));
    await using var output = new MemoryStream();
    if (await SkillMcpBroker.RunAsync(registry, input, output, cancellationToken) != 0)
        throw new InvalidOperationException("In-process MCP discovery failed.");
    await File.WriteAllBytesAsync(Path.Combine(evidence, label + ".tools.jsonl"), output.ToArray(), cancellationToken);
    using var list = JsonDocument.Parse(Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Last());
    var tools = list.RootElement.GetProperty("result").GetProperty("tools");
    if (tools.GetArrayLength() != registry.Skills.Count) throw new InvalidOperationException("MCP discovery omitted a registered script.");
    log.Record("tools-list", new { label, count = tools.GetArrayLength(), names = tools.EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToArray() });
}

static AiSkill RequiredInstalled(IReadOnlyList<AiSkill> skills, string id)
    => skills.SingleOrDefault(skill => skill.Id == id && skill.Enabled && skill.IsValid)
        ?? throw new InvalidOperationException("A required skill is missing, disabled, or invalid: " + id + ". Existing enabled states are never changed by this probe.");

static SkillRegistration FindRegistration(SkillExecutionRegistry registry, string id)
{
    var script = id switch { "bok-ecos-stats" => "bok_ecos.py", "kosis-stats" => "run_kosis_stats.py", "tablecloth-calculators" => "calculate.py", _ => throw new ArgumentException(nameof(id)) };
    return registry.Skills.Single(skill => Path.GetFileName(skill.ScriptPath).Equals(script, StringComparison.OrdinalIgnoreCase));
}

static void WriteSkills(EvidenceLog target, string stageName, IReadOnlyList<AiSkill> available)
    => target.Record(stageName, available.Select(skill => new { skill.Id, skill.Name, skill.Directory, skill.Enabled, skill.IsValid, skill.Source }).ToArray());

static async Task<string> HashFileAsync(string path, CancellationToken token)
{
    await using var file = File.OpenRead(path);
    return Convert.ToHexStringLower(await SHA256.HashDataAsync(file, token));
}

static Task SaveJsonAsync(string path, object value, CancellationToken token)
    => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false), token);

sealed record ProbeOptions(bool Discover, bool Direct, bool NodeFixture, bool InstallCatalog, string? ChatSkill, string Model, string? NativePath, string? CalculatorCases, string? InstallRuntimeVersion)
{
    internal static ProbeOptions Parse(string[] input)
    {
        bool discover = false, direct = false, node = false, install = false;
        string? chat = null, native = null, calculatorCases = null, installRuntime = null;
        var model = "gpt-6-luna";
        for (var index = 0; index < input.Length; index++)
        {
            string Next() => ++index < input.Length ? input[index] : throw new ArgumentException("Missing value for " + input[index - 1]);
            switch (input[index])
            {
                case "--discover": discover = true; break;
                case "--direct": direct = true; break;
                case "--node-fixture": node = true; break;
                case "--install-catalog": install = true; break;
                case "--install-runtime": installRuntime = Next(); break;
                case "--chat": chat = Next(); break;
                case "--model": model = Next(); break;
                case "--nativepath" or "--native-path": native = Next(); break;
                case "--calculator-cases": calculatorCases = Next(); break;
                default: throw new ArgumentException("Unknown option: " + input[index]);
            }
        }
        if (chat is not null && chat != "all" && !SkillCases.ActiveIds.Contains(chat, StringComparer.Ordinal)) throw new ArgumentException("Unknown active skill: " + chat);
        if (calculatorCases is not null && (!direct || !Path.IsPathFullyQualified(calculatorCases)))
            throw new ArgumentException("--calculator-cases requires --direct and an absolute JSON dataset path.");
        AiModel.ValidateId(model);
        if (installRuntime is not null) OpenAiReleaseMetadataClient.ValidateVersion(installRuntime);
        return new(discover || !direct && !node && chat is null, direct, node, install, chat, model, native, calculatorCases, installRuntime);
    }
}

sealed class EvidenceLog(string path) : IDisposable
{
    private readonly StreamWriter _writer = new(path, append: false, new UTF8Encoding(false)) { AutoFlush = true };
    internal void Record(string stage, object? details)
    {
        var text = JsonSerializer.Serialize(new { stage, timestampUtc = DateTimeOffset.UtcNow, details });
        _writer.WriteLine(text);
        Console.WriteLine(text);
    }
    public void Dispose() => _writer.Dispose();
}

sealed class CaptureSession(string directory, EvidenceLog log)
{
    internal string? CurrentPath { get; private set; }
    internal string? SkillId { get; private set; }
    internal void Begin(string id)
    {
        SkillId = id;
        CurrentPath = Path.Combine(directory, id + ".provider.events.jsonl");
        File.WriteAllText(CurrentPath, "", new UTF8Encoding(false));
    }
    internal void End() { CurrentPath = null; SkillId = null; }
    internal void RecordInvocation(ProcessRunSpec spec)
    {
        spec.StartInfo.Environment.TryGetValue("CODEX_HOME", out var codexHome);
        log.Record("captured-real-codex-exec", new
        {
            skillId = SkillId,
            executable = spec.StartInfo.FileName,
            workingDirectory = spec.StartInfo.WorkingDirectory,
            codexHome,
            exec = spec.StartInfo.ArgumentList.Contains("exec"),
            json = spec.StartInfo.ArgumentList.Contains("--json"),
            readOnlySandbox = spec.StartInfo.ArgumentList.Contains("read-only"),
            rawAuthOutputCaptured = false,
            arguments = spec.StartInfo.ArgumentList.ToArray(),
            eventPath = CurrentPath
        });
    }

    internal void CopyManifest(ProcessRunSpec spec, ManagedAiPaths paths)
    {
        var configuration = spec.StartInfo.ArgumentList.SingleOrDefault(value => value.StartsWith("mcp_servers.tablecloth_skills=", StringComparison.Ordinal));
        if (configuration is null) return;
        var match = Regex.Match(configuration, "args=\\[\"--skill-mcp\",(?<path>\"(?:\\\\.|[^\"\\\\])*\")\\]",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
        if (!match.Success) throw new InvalidOperationException("The actual product MCP manifest could not be captured.");
        using var encodedPath = JsonDocument.Parse(match.Groups["path"].Value);
        var manifestPath = Path.GetFullPath(encodedPath.RootElement.GetString()!);
        var expectedDirectory = paths.Under("skill-execution");
        if (!string.Equals(Path.GetDirectoryName(manifestPath), expectedDirectory, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(manifestPath).StartsWith("manifest-", StringComparison.Ordinal) ||
            new FileInfo(manifestPath).Length is < 1 or > 4194304)
            throw new InvalidOperationException("The captured MCP manifest is outside the application-owned registration directory.");
        ManagedAiPaths.RejectReparsePoints(manifestPath);
        File.Copy(manifestPath, Path.Combine(directory, SkillId + ".manifest.json"), overwrite: false);
        File.Copy(manifestPath, Path.Combine(directory, "runtime-seed-manifest.json"), overwrite: true);
    }
}

sealed class CapturingRunner(CaptureSession session, ManagedAiPaths paths) : IJsonlProcessRunner
{
    private readonly JsonlProcessRunner _inner = new();
    public async Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> output, Action<string>? error, CancellationToken token)
    {
        var path = session.CurrentPath;
        var capture = path is not null && spec.StartInfo.ArgumentList.Contains("exec") && spec.StartInfo.ArgumentList.Contains("--json");
        if (capture)
        {
            session.CopyManifest(spec, paths);
            session.RecordInvocation(spec);
        }
        using var writer = capture ? new StreamWriter(path!, append: true, new UTF8Encoding(false)) { AutoFlush = true } : null;
        return await _inner.RunAsync(spec, line =>
        {
            writer?.WriteLine(line);
            output(line);
        }, error, token);
    }
}

sealed record ChatProof(bool Passed, string EvidenceType, int SuccessfulContainedCalls, bool ExpectedScriptResultObserved, bool TurnCompleted, string[] ToolNames);

sealed record CalculatorCaseSummary(string Operation, bool Passed, bool ValidBrokerResult, bool ValidScriptEnvelope,
    bool ExactResult, string? Failure, string? Status, int? ExitCode, bool WasAppContainer);

static class SkillCases
{
    internal static readonly string[] ActiveIds = ["bok-ecos-stats", "kosis-stats", "official-report-skillset", "tablecloth-certificate-expiry", "tablecloth-windows-sandbox", "tablecloth-calculators"];
    internal static readonly string[] ScriptIds = ["bok-ecos-stats", "kosis-stats", "tablecloth-calculators"];

    internal static JsonElement DirectInput(string id, string kind)
    {
        var stdin = id == "tablecloth-calculators" ? "{\"operation\":\"bmi\",\"height_cm\":\"171\",\"weight_kg\":\"80\"}" : "";
        if (kind == "python-json-stdin") return JsonSerializer.Deserialize<JsonElement>(stdin);
        var arguments = id switch
        {
            "bok-ecos-stats" => new[] { "key", "--limit", "1", "--timeout", "15", "--secrets-path", "__tablecloth_no_credentials__" },
            "kosis-stats" => new[] { "search", "--query", "인구", "--result-count", "1", "--json", "--timeout", "15" },
            "tablecloth-calculators" => Array.Empty<string>(),
            _ => throw new ArgumentException(nameof(id))
        };
        return JsonSerializer.SerializeToElement(new { arguments, stdin });
    }

    internal static AiChatRequest ChatRequest(string id, string model) => id switch
    {
        "bok-ecos-stats" => new("bok-ecos-stats 스킬의 등록된 실행 도구로 ECOS 100대 핵심지표를 1개만 조회해주세요. key, limit 1, timeout 15초를 사용하고 지표 이름, 값, 단위와 출처를 알려주세요. 사용자 API 키는 사용하지 않습니다.", [], model),
        "kosis-stats" => new("kosis-stats 스킬의 등록된 실행 도구로 검색어 인구를 검색해주세요. search, query 인구, result-count 1, timeout 15초를 사용하고 통계표 한 개의 기관, 표 이름, ORG_ID와 TBL_ID를 알려주세요. 키 없는 기본 프록시를 사용하고 외부 웹 검색으로 대체하지 마세요.", [], model),
        "tablecloth-calculators" => new("키 171cm, 몸무게 80kg인 사람의 BMI는 얼마인가요? tablecloth-calculators 스킬을 활용해서 계산해주세요.", [], model),
        "official-report-skillset" => new("official-report-skillset 스킬로 다음 가상 메모를 내부 결재 보고서 초안으로 정리해주세요. 2026년 10월 20일 교육장 대관 계약을 체결했습니다. 11월 2일부터 30일까지 시범 운영하며 예산은 100만 원입니다. 담당 부서와 붙임은 아직 정하지 않았습니다. 미확인 정보는 만들어내지 마세요. 가상 입력에 대한 문서 초안만 작성하며 외부 조회나 스크립트 실행은 필요하지 않습니다.", [], model),
        "tablecloth-certificate-expiry" => new("이 요청은 합성 보고서 해석 시험이며 실제 인증서 검색은 수행하지 않았습니다. tablecloth-certificate-expiry 스킬로 현재 턴에 첨부한 합성 만료 건수 보고서만 설명해주세요. 인증서가 이미 만료된 건수와 30일 이내 만료될 건수를 구분하고 실제 컴퓨터를 검사했다고 표현하지 마세요.", [], model,
            LocalCertificateReport: "{\"rootFound\":true,\"withinDays\":30,\"expiredCount\":1,\"expiringCount\":2,\"scanIncomplete\":false}"),
        "tablecloth-windows-sandbox" => new("이 요청은 합성 보고서 해석 시험이며 실제 Sandbox 조회나 제어는 수행하지 않았습니다. tablecloth-windows-sandbox 스킬로 현재 턴에 첨부한 합성 list 보고서를 설명해주세요. 합성 결과가 세션 0개를 나타내는지 알려주고 새 세션을 시작하거나 실제 컴퓨터를 조회했다고 표현하지 마세요.", [], model,
            LocalWindowsSandboxReport: "{\"Action\":\"list\",\"Status\":\"succeeded\",\"ExitCode\":0,\"Output\":\"[]\",\"Synthetic\":true}"),
        _ => throw new ArgumentException(nameof(id))
    };

    internal static bool ValidateScriptResult(string id, SkillExecutionResult result)
    {
        if (result.Status != "completed" || !result.WasAppContainer || result.ExitCode != 0 || result.TimedOut || result.OutputLimited || result.Failure is not null) return false;
        // The pinned BOK helper supports --text as a legitimate CLI output mode.
        // Validate its exact one-row key-indicator format, never arbitrary nonempty text.
        if (id == "bok-ecos-stats" && ValidBokKeyIndicatorText(result.Stdout)) return true;
        try
        {
            using var json = JsonDocument.Parse(result.Stdout);
            var root = json.RootElement;
            return id switch
            {
                "bok-ecos-stats" => root.GetProperty("result").GetString() == "ok" && root.GetProperty("rows").GetArrayLength() == 1,
                "kosis-stats" => EvidenceVerifier.IsValidKosisSearchData(result.Stdout),
                "tablecloth-calculators" => root.GetProperty("ok").GetBoolean() && root.GetProperty("operation").GetString() == "bmi" &&
                    root.GetProperty("result").GetProperty("bmi").GetString() == "27.36",
                _ => false
            };
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
    }

    private static bool ValidBokKeyIndicatorText(string stdout)
    {
        // render_text("key", rows) writes: [class_name] name: value unit (cycle).
        // This smoke asks for one daily headline indicator, so require one complete
        // numeric row with a valid yyyyMMdd date. The JSON path stays independently strict.
        var line = stdout.TrimEnd('\r', '\n');
        if (line.Length is < 1 or > 4096 || line.Any(char.IsControl)) return false;
        var match = Regex.Match(line,
            @"\A\[(?<category>[^\]\r\n]+)\] (?<indicator>[^\r\n]+): (?<value>[+-]?(?:[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?:\.[0-9]+)?) (?<unit>[^\r\n]+) \((?<cycle>[0-9]{8})\)\z",
            RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100));
        return match.Success && !string.IsNullOrWhiteSpace(match.Groups["category"].Value) &&
            !string.IsNullOrWhiteSpace(match.Groups["indicator"].Value) && !string.IsNullOrWhiteSpace(match.Groups["unit"].Value) &&
            decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) &&
            DateOnly.TryParseExact(match.Groups["cycle"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    internal static ChatProof InspectCapture(string id, string path)
    {
        var contained = 0;
        var expected = false;
        var completed = false;
        var answer = new StringBuilder();
        var tools = new List<string>();
        foreach (var line in File.ReadLines(path))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type == "turn.completed") completed = true;
            if (type != "item.completed" || !root.TryGetProperty("item", out var item)) continue;
            var itemType = item.GetProperty("type").GetString();
            if (itemType == "agent_message" && item.TryGetProperty("text", out var text)) answer.AppendLine(text.GetString());
            if (itemType != "mcp_tool_call") continue;
            var toolName = item.TryGetProperty("tool", out var tool) ? tool.GetString() ?? "" : "";
            tools.Add(toolName);
            if (!item.TryGetProperty("server", out var server) || server.GetString() != "tablecloth_skills" ||
                (ScriptIds.Contains(id, StringComparer.Ordinal) && toolName != id) ||
                !item.TryGetProperty("status", out var callStatus) || callStatus.GetString() != "completed" ||
                (item.TryGetProperty("error", out var callError) && callError.ValueKind != JsonValueKind.Null) ||
                !item.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var block in content.EnumerateArray())
            {
                if (!block.TryGetProperty("text", out var payloadText)) continue;
                try
                {
                    using var payload = JsonDocument.Parse(payloadText.GetString()!);
                    var value = payload.RootElement;
                    var execution = new SkillExecutionResult(value.GetProperty("status").GetString()!,
                        value.GetProperty("exitCode").ValueKind == JsonValueKind.Number ? value.GetProperty("exitCode").GetInt32() : null,
                        value.GetProperty("stdout").GetString()!, value.GetProperty("stderr").GetString()!,
                        value.GetProperty("wasAppContainer").GetBoolean(), value.GetProperty("timedOut").GetBoolean(),
                        value.GetProperty("outputLimited").GetBoolean(), value.GetProperty("failure").GetString());
                    if (execution.Status == "completed" && execution.WasAppContainer && execution.ExitCode == 0 &&
                        !execution.TimedOut && !execution.OutputLimited && execution.Failure is null) contained++;
                    if (ValidateScriptResult(id, execution)) expected = true;
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { }
            }
        }
        if (ScriptIds.Contains(id, StringComparer.Ordinal))
            return new(completed && contained > 0 && expected, "actual-codex-mcp-contained-script", contained, expected, completed, tools.ToArray());
        var response = answer.ToString();
        var interpretation = id switch
        {
            "official-report-skillset" => new[] { "추진 배경", "추진 경과", "주요 내용", "향후 추진 계획", "붙임" }.All(response.Contains),
            "tablecloth-certificate-expiry" => response.Contains('1') && response.Contains('2') && response.Contains("30", StringComparison.Ordinal) && response.Contains("합성", StringComparison.Ordinal),
            "tablecloth-windows-sandbox" => (response.Contains("0개", StringComparison.Ordinal) || response.Contains("없", StringComparison.Ordinal)) && response.Contains("합성", StringComparison.Ordinal),
            _ => false
        };
        // This is response/fixture evidence only. It cannot prove that a model read a
        // particular SKILL.md; a self-reported skill name is deliberately insufficient.
        return new(completed && interpretation && contained == 0,
            id == "official-report-skillset" ? "instruction-compatible-response-not-file-read-proof" : "synthetic-host-report-interpretation-no-live-host-operation",
            contained, false, completed, tools.ToArray());
    }
}
