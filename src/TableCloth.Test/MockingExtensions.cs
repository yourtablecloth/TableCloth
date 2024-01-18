using TableCloth.Components;

namespace TableCloth.Test;

internal static class MockingExtensions
{
    public static IServiceCollection ProvideMockupApplication(
        this IServiceCollection services)
    {
        return services.ReplaceWithMock<IApplicationService>(mock =>
        {
            mock
                .Setup(x => x.DispatchInvoke(IsAny<Delegate>(), IsAny<object?[]>()))
                .Returns<Delegate, object[]>((_delegate, _args) => _delegate.DynamicInvoke(_args));
        });
    }

    public static IServiceCollection ReplaceWithMock<TService>(
        this IServiceCollection services,
        Action<Mock<TService>>? mockSetup = default)
        where TService : class
    {
        services.RemoveAll<Mock<TService>>();
        services.RemoveAll<TService>();

        var mock = new Mock<TService>();
        mockSetup?.Invoke(mock);

        services.AddSingleton(mock.Object);
        services.AddSingleton(mock);

        return services;
    }

    public static Mock<TService> GetRequiredMock<TService>(
        this IServiceProvider serviceProvider)
        where TService : class
    {
        return serviceProvider.GetRequiredService<Mock<TService>>();
    }
}
