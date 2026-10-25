using Microsoft.Extensions.DependencyInjection;
using TableCloth.ManagedAi;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

// Diagnostic host. Actual browser opening lives in the TableCloth AI Preview window.
if (args.Length == 0 || args[0] is "help" or "--help")
{
    Console.WriteLine("TableCloth AI Preview diagnostics\nCommands: metadata, models, install --accept-download [version], status, login [--browser], logout, rollback, search --accept-usage, chat --accept-usage, skill-runtime-status, skill-runtime-install --accept-download, skill-runtime-smoke, skill-runtime-diagnose [--expect-enabled | --expect-disabled]");
    Console.WriteLine("Search/chat reads one message from stdin. Use the TableCloth AI chat window to open links in Windows Sandbox.");
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var services = new ServiceCollection();
var diagnosticPaths = ManagedAiPaths.ForCurrentUser();
if (args.Length >= 3 && args[0].StartsWith("skill-runtime-", StringComparison.Ordinal) && args[^2] == "--root")
{
    diagnosticPaths = new ManagedAiPaths(args[^1]);
    args = args[..^2];
}
services.AddManagedOpenAi(diagnosticPaths);
services.AddSingleton<IManagedAiProfile, WindowsManagedAiProfile>();
services.AddSingleton<IJsonlProcessRunner, JsonlProcessRunner>();
await using var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IManagedRuntimeManager>();
var auth = provider.GetRequiredService<IProviderAuthentication>();
var token = cancellation.Token;
try
{
    switch (args[0])
    {
        case "skill-runtime-status" when args.Length == 1:
            var tools = await provider.GetRequiredService<IManagedAiSkillRuntimeManager>().GetActiveAsync(token);
            Console.WriteLine(tools is null ? "Skill runtimes not installed" : $"Node.js {tools.NodeJs.Version}; Python {tools.Python.Version}");
            break;
        case "skill-runtime-install" when args is ["skill-runtime-install", "--accept-download"]:
            var prepared = await provider.GetRequiredService<IManagedAiSkillRuntimeManager>().InstallAsync(null, token);
            Console.WriteLine($"Installed Node.js {prepared.NodeJs.Version}; Python {prepared.Python.Version}");
            break;
        case "skill-runtime-diagnose" when args.Length == 1 || args is ["skill-runtime-diagnose", "--expect-enabled" or "--expect-disabled"]:
            var diagnostics = await provider.GetRequiredService<IManagedAiSkillRuntimeDiagnostics>().CheckAsync(token);
            Console.WriteLine($"Windows long-path policy: {diagnostics.WindowsPolicy}");
            Console.WriteLine($"Managed file I/O: {diagnostics.ManagedIo}; Node.js file I/O: {diagnostics.NodeJs}; Python file I/O: {diagnostics.Python}");
            if (args.Length == 2)
            {
                var expectedPolicy = args[1] == "--expect-enabled" ? WindowsLongPathPolicy.Enabled : WindowsLongPathPolicy.Disabled;
                var expectedPython = expectedPolicy == WindowsLongPathPolicy.Enabled ? LongPathSupport.Supported : LongPathSupport.Limited;
                if (diagnostics.WindowsPolicy != expectedPolicy || diagnostics.ManagedIo != LongPathSupport.Supported ||
                    diagnostics.NodeJs != LongPathSupport.Supported || diagnostics.Python != expectedPython)
                    throw new ManagedAiException(AiFailureCode.ProviderConfigurationInvalid);
            }
            Console.WriteLine("Diagnostics completed. Windows policy was read without modification; no model or network call was made.");
            break;
        case "skill-runtime-smoke" when args.Length == 1:
            var environment = await provider.GetRequiredService<IManagedAiSkillRuntimeManager>().GetActiveAsync(token)
                ?? throw new ManagedAiException(AiFailureCode.RuntimeNotInstalled);
            var paths = provider.GetRequiredService<ManagedAiPaths>();
            var smokeRunner = provider.GetRequiredService<IJsonlProcessRunner>();
            await Smoke(environment.NodeJs.EntryPoint, ["--version"]);
            await Smoke(environment.NodeJs.EntryPoint, [Path.Combine(environment.NodeJs.RootDirectory, "node_modules", "npm", "bin", "npm-cli.js"), "--version"]);
            await Smoke(environment.NodeJs.EntryPoint, [Path.Combine(environment.NodeJs.RootDirectory, "node_modules", "npm", "bin", "npx-cli.js"), "--version"]);
            await Smoke(environment.Python.EntryPoint, ["-I", "-S", "-c",
                "import sys,json,ssl,urllib.request,sqlite3,dataclasses; assert sys.flags.isolated and sys.flags.no_user_site; print(json.dumps({'version':sys.version.split()[0],'isolated':bool(sys.flags.isolated),'no_user_site':bool(sys.flags.no_user_site)}))"]);
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            await Smoke(Path.Combine(system, "cmd.exe"), ["/d", "/c", "where /q node && where /q python && where /q python3 && node --version && python --version && python3 --version && npm --version && npx --version"]);
            await Smoke(Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe"),
                ["-NoProfile", "-NonInteractive", "-Command", "[Console]::OutputEncoding=[System.Text.UTF8Encoding]::new($false); $ErrorActionPreference='Stop'; (Get-Command npm).Source; (Get-Command npx).Source; npm --version; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; npx --version; exit $LASTEXITCODE"]);
            Console.WriteLine("Skill runtime smoke checks passed. No model, API key, or external npm package was used.");
            break;

            async Task Smoke(string executable, string[] arguments)
            {
                var info = new System.Diagnostics.ProcessStartInfo(executable)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = paths.Under("profiles", "skill-tools")
                };
                info.Environment.Clear();
                foreach (var argument in arguments) info.ArgumentList.Add(argument);
                SkillRuntimeProcessEnvironment.Apply(info, paths, environment);
                var result = await smokeRunner.RunAsync(new(info, null, TimeSpan.FromSeconds(30), 65536, 65536, 8192),
                    Console.WriteLine, Console.Error.WriteLine, token);
                if (result.ExitCode != 0) throw new ManagedAiException(AiFailureCode.RuntimeVersionMismatch);
            }
        case "models":
            var models = await provider.GetRequiredService<IManagedAiModelCatalog>().ListAsync(token);
            foreach (var model in models)
                Console.WriteLine($"{model.Id}\t{model.DisplayName}\tDefault={model.IsDefault}");
            Console.WriteLine($"InitialSelection={AiModelSelection.Select(models, null, DateOnly.FromDateTime(DateTime.UtcNow)).Id}");
            break;
        case "metadata":
            var release = await provider.GetRequiredService<OpenAiReleaseMetadataClient>().GetAsync(null, token);
            Console.WriteLine($"{release.Version} {release.Target} {release.Package.Sha256}");
            break;
        case "install" when args.Length >= 2 && args[1] == "--accept-download":
            var installed = await runtime.InstallAsync(args.Length > 2 ? args[2] : null, null, token);
            Console.WriteLine($"Installed {installed.Coordinate.Version}");
            break;
        case "status":
            Console.WriteLine(await runtime.GetActiveAsync(token) is { } active ? $"Runtime {active.Coordinate.Version}" : "Runtime not installed");
            Console.WriteLine(await auth.IsLoggedInAsync(token) ? "ChatGPT authenticated" : "Authentication required");
            break;
        case "login" when args.Length == 1 || args is ["login", "--browser"]:
            await auth.LoginAsync(args.Length == 2 ? AiLoginMethod.Browser : AiLoginMethod.DeviceCode,
                new ConsoleLoginProgress(), token);
            Console.WriteLine("ChatGPT authenticated");
            break;
        case "logout": await auth.LogoutAsync(token); Console.WriteLine("Logged out"); break;
        case "rollback": Console.WriteLine($"Active {(await runtime.RollbackAsync(token)).Coordinate.Version}"); break;
        case "chat" when args is ["chat", "--accept-usage"]:
            Console.WriteLine("메시지를 입력합니다. OpenAI에 대화를 전송하고 구독 사용량을 사용합니다.");
            var response = await provider.GetRequiredService<IManagedAiChatProvider>().ChatAsync(
                new(Console.ReadLine() ?? string.Empty, []), null, token);
            Console.WriteLine(response.Text);
            Console.WriteLine($"Web searches: {response.SearchCalls}");
            break;
        case "search" when args.Length == 2 && args[1] == "--accept-usage":
            Console.WriteLine("검색어를 입력합니다. OpenAI에 질의를 전송하고 구독 사용량을 사용합니다.");
            var query = Console.ReadLine() ?? string.Empty;
            var result = await provider.GetRequiredService<IManagedAiProvider>().SearchAsync(new(query), null, token);
            Console.WriteLine(result.Answer);
            foreach (var candidate in result.Results) Console.WriteLine($"{candidate.Title}\n{candidate.TargetUrl}\n출처: {candidate.SourceUrl}");
            foreach (var warning in result.Warnings) Console.WriteLine(warning);
            break;
        default: Console.Error.WriteLine("Unknown command or missing explicit consent flag. Use --help."); return 2;
    }
    return 0;
}
catch (OperationCanceledException) { Console.Error.WriteLine("Cancelled"); return 130; }
catch (ManagedAiException ex) { Console.Error.WriteLine(ex.Code); return 1; }
catch { Console.Error.WriteLine("OperationFailed"); return 1; }

// Interactive authentication details are displayed only for the active login, never persisted as diagnostics.
sealed class ConsoleLoginProgress : IProgress<AiLoginUpdate>
{
    public void Report(AiLoginUpdate value)
    {
        if (value.VerificationUri is { } uri) Console.WriteLine(uri.AbsoluteUri);
        if (value.UserCode is { } code) Console.WriteLine($"Device code: {code}");
    }
}
