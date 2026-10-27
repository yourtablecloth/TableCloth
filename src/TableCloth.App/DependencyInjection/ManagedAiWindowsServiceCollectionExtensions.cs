using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<WindowsManagedAiScriptRuntimeManager>();
        services.AddSingleton<IManagedAiScriptRuntimeManager>(service =>
            service.GetRequiredService<WindowsManagedAiScriptRuntimeManager>());
        services.AddSingleton<IManagedAiScriptExecutionRuntime>(service =>
            service.GetRequiredService<WindowsManagedAiScriptRuntimeManager>());
        services.AddSingleton<IManagedAiSkillExecution>(service => new WindowsManagedAiSkillExecution(
            service.GetRequiredService<ManagedAiPaths>(),
            service.GetRequiredService<IManagedAiScriptExecutionRuntime>(),
            service.GetRequiredService<ISharedLocations>().ExecutableFilePath,
            service.GetService<IManagedAiSkillMarketplace>()));
        return services;
    }
}
