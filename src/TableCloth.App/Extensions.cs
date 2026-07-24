using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using TableCloth.Resources;

namespace TableCloth;

internal static class Extensions
{
    public static HttpClient CreateTableClothHttpClient(this IHttpClientFactory httpClientFactory)
        => httpClientFactory
            .EnsureNotNull("HTTP client factory cannot be null reference.")
            .CreateClient(nameof(ConstantStrings.UserAgentText));

    public static HttpClient CreateGitHubRestApiClient(this IHttpClientFactory httpClientFactory)
        => httpClientFactory
            .EnsureNotNull("HTTP client factory cannot be null reference.")
            .CreateClient(nameof(StringResources.TableCloth_GitHubRestUAString));

    public static void AddCommands(this IServiceCollection services, params Type[] commandTypes)
    {
        foreach (var eachCommandType in commandTypes)
            services.AddSingleton(eachCommandType);
    }

    public static IServiceCollection AddWindow<TWindow, TViewModel>(this IServiceCollection services,
        Func<IServiceProvider, TWindow>? windowImplementationFactory = default,
        Func<IServiceProvider, TViewModel>? viewModelImplementationFactory = default)
        where TWindow : Window
        where TViewModel : class
    {
        if (windowImplementationFactory != null)
            services.AddTransient(windowImplementationFactory);
        else
            services.AddTransient<TWindow>();

        if (viewModelImplementationFactory != null)
            services.AddTransient(viewModelImplementationFactory);
        else
            services.AddTransient<TViewModel>();

        return services;
    }

    public static IServiceCollection AddWindow<TWindow>(this IServiceCollection services,
        Func<IServiceProvider, TWindow>? windowImplementationFactory = default)
        where TWindow : Window
    {
        if (windowImplementationFactory != null)
            services.AddTransient(windowImplementationFactory);
        else
            services.AddTransient<TWindow>();

        return services;
    }

    // 이슈 #296: WPF Page → Avalonia UserControl(Control). 나머지 등록 로직은 동일.
    public static IServiceCollection AddPage<TPage, TViewModel>(this IServiceCollection services,
        bool addPageAsSingleton = false,
        Func<IServiceProvider, TPage>? pageImplementationFactory = default,
        Func<IServiceProvider, TViewModel>? viewModelImplementationFactory = default)
        where TPage : Control
        where TViewModel : class
    {
        if (addPageAsSingleton)
        {
            if (pageImplementationFactory != null)
                services.AddSingleton(pageImplementationFactory);
            else
                services.AddSingleton<TPage>();
        }
        else
        {
            if (pageImplementationFactory != null)
                services.AddTransient(pageImplementationFactory);
            else
                services.AddTransient<TPage>();
        }

        if (viewModelImplementationFactory != null)
            services.AddTransient(viewModelImplementationFactory);
        else
            services.AddTransient<TViewModel>();

        return services;
    }

    // 이슈 #296: WPF 전용 헬퍼(TryLeaveFocus/FindChildControl/IsControlVisibleToUser)와 Application.Properties
    // 기반 Init/GetServiceProvider 는 폐기. 서비스 프로바이더는 TableClothApplication.ServiceProvider 정적 홀더로 노출.
}
