namespace TableCloth.Test;

internal static class MockingExtensions
{
    private static readonly Lazy<Application> _appFactory =
        new Lazy<Application>(() => new Application(), false);

    public static IServiceCollection ProvideApplication(
        this IServiceCollection services)
        => services.AddSingleton(_ => _appFactory.Value);

    public static IServiceCollection ReplaceWithMock<TService>(
        this IServiceCollection services,
        Action<Mock>? mockSetup = default)
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
