using Microsoft.Extensions.DependencyInjection;
using TableCloth.ManagedAi;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

// Diagnostic host. Actual browser opening lives in the TableCloth AI Preview window.
if (args.Length == 0 || args[0] is "help" or "--help")
{
    Console.WriteLine("TableCloth AI Preview diagnostics\nCommands: metadata, models, install --accept-download [version], status, login [--browser], logout, rollback, search --accept-usage, chat --accept-usage");
    Console.WriteLine("Search/chat reads one message from stdin. Use the TableCloth AI chat window to open links in Windows Sandbox.");
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
var services = new ServiceCollection();
services.AddManagedOpenAi(ManagedAiPaths.ForCurrentUser());
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
