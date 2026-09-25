using Microsoft.Extensions.DependencyInjection;

namespace TableCloth.ManagedAi.OpenAi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddManagedOpenAi(this IServiceCollection services, ManagedAiPaths? paths = null)
    {
        services.AddSingleton(_ => paths ?? ManagedAiPaths.ForCurrentUser()).AddSingleton<ManagedAiOptions>();
        services.AddHttpClient(OpenAiReleaseMetadataClient.HttpClientName, client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TableCloth-ManagedAi-Preview/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
          .RemoveAllLoggers();
        services.AddSingleton<OpenAiReleaseMetadataClient>();
        services.AddSingleton<IManagedRuntimeManager, OpenAiCodexPackageInstaller>();
        services.AddSingleton<OpenAiCodexAuthManager>();
        services.AddSingleton<IProviderAuthentication>(sp => sp.GetRequiredService<OpenAiCodexAuthManager>());
        services.AddSingleton<OpenAiCodexProvider>();
        services.AddSingleton<IManagedAiProvider>(sp => sp.GetRequiredService<OpenAiCodexProvider>());
        services.AddSingleton<IManagedAiChatProvider>(sp => sp.GetRequiredService<OpenAiCodexProvider>());
        services.AddSingleton<IManagedAiModelCatalog, OpenAiCodexModelCatalog>();
        services.AddSingleton<DiagnosticsSink>();
        services.AddTransient<ManagedAiOrchestrator>();
        services.AddTransient<ManagedAiChatSession>();
        return services;
    }
}
