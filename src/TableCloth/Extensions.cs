using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    public static IServiceCollection AddPage<TPage, TViewModel>(this IServiceCollection services,
        bool addPageAsSingleton = false,
        Func<IServiceProvider, TPage>? pageImplementationFactory = default,
        Func<IServiceProvider, TViewModel>? viewModelImplementationFactory = default)
        where TPage : Page
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

    // https://stackoverflow.com/a/2914599
    public static bool TryLeaveFocus(this FrameworkElement? targetElement)
    {
        if (targetElement == null)
            return false;

        var parent = (FrameworkElement)targetElement.Parent;
        while (parent != null && parent is IInputElement element && !element.Focusable)
            parent = (FrameworkElement)parent.Parent;

        var scope = FocusManager.GetFocusScope(targetElement);
        FocusManager.SetFocusedElement(scope, parent as IInputElement);
        return true;
    }

    public static IServiceProvider GetServiceProvider(this Application application)
    {
        var serviceProvider = application.Properties[nameof(IServiceProvider)] as IServiceProvider;
        ArgumentNullException.ThrowIfNull(serviceProvider);
        return serviceProvider;
    }

    public static void InitServiceProvider(this Application application, IServiceProvider serviceProvider)
    {
        const string key = nameof(IServiceProvider);

        if (application.Properties.Contains(key) &&
            application.Properties[key] != null)
            TableClothAppException.Throw("Already service provider has been initialized.");

        application.Properties[key] = serviceProvider;
    }

    public static T? FindChildControl<T>(this DependencyObject depObj)
        where T : DependencyObject
    {
        if (depObj != null)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                if (child is T targetChild)
                    return targetChild;

                var childItem = FindChildControl<T>(child);
                if (childItem != null)
                    return childItem;
            }
        }

        return null;
    }

    public static bool IsControlVisibleToUser(this FrameworkElement element, FrameworkElement container)
    {
        if (!element.IsVisible)
            return false;

        // 컨트롤의 상대적인 위치를 화면에서의 위치로 변환
        Rect bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
        Rect rect = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);

        // 컨트롤의 영역이 컨테이너의 보이는 영역과 겹치는지 확인
        return rect.IntersectsWith(bounds);
    }
}
