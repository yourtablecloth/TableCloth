using Microsoft.Extensions.DependencyInjection;
using TableCloth.Components;

namespace TableCloth.Test;

public sealed class ResourceResolverTest : IClassFixture<ContainerFixture>
{
    public ResourceResolverTest(ContainerFixture fixture)
    {
        _serviceProvider = fixture.ServiceProvider;
        _resourceResolver = _serviceProvider.GetRequiredService<ResourceResolver>();
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly ResourceResolver _resourceResolver;

    [Fact]
    public async Task TestGetLatestVersion()
    {
        // Arrange
        const string repoOwner = "yourtablecloth";
        const string repoName = "TableCloth";

        // Act
        var result = await _resourceResolver.GetLatestVersion(repoOwner, repoName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
