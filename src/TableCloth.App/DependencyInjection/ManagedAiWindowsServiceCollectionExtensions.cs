using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Net.Http;
using TableCloth.Components;
using TableCloth.ManagedAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.App.DependencyInjection;

internal static class ManagedAiWindowsServiceCollectionExtensions
{
    internal static IServiceCollection AddManagedAiWindows(this IServiceCollection services)
    {
        services.AddSingleton<IJsonlProcessRunner, JsonlProcessRunner>();
        services.AddSingleton<IManagedAiProfile, WindowsManagedAiProfile>();
        services.TryAddSingleton<IManagedAiNetworkSettings, DefaultManagedAiNetworkSettings>();
        // Host downloads keep Windows certificate trust and the existing default proxy.
        // No certificate bypass or additional credential/environment propagation to workers.
        services.AddHttpClient(WindowsManagedAiScriptRuntimeManager.DownloadHttpClientName,
            client => client.Timeout = TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false, UseCookies = false,
                UseDefaultCredentials = false, Credentials = null
            }).RemoveAllLoggers();
        services.AddSingleton<WindowsManagedAiScriptRuntimeManager>();
        services.AddSingleton<IManagedAiScriptRuntimeManager>(service =>
            service.GetRequiredService<WindowsManagedAiScriptRuntimeManager>());
        services.AddSingleton<IManagedAiScriptExecutionRuntime>(service =>
            service.GetRequiredService<WindowsManagedAiScriptRuntimeManager>());
        services.AddSingleton<IManagedAiSkillExecution>(service => new WindowsManagedAiSkillExecution(
            service.GetRequiredService<ManagedAiPaths>(),
            service.GetRequiredService<IManagedAiScriptExecutionRuntime>(),
            service.GetRequiredService<ISharedLocations>().ExecutableFilePath,
            service.GetService<IManagedAiSkillMarketplace>(),
            service.GetRequiredService<IManagedAiNetworkSettings>()));
        return services;
    }
}
