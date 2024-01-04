using Microsoft.Extensions.DependencyInjection;
using TableCloth.Components;
using TableCloth.Test.Fixtures;

namespace TableCloth.Test;

public sealed class ResourceResolverTest : IClassFixture<DefaultContainerFixture>
{
    public ResourceResolverTest(DefaultContainerFixture fixture)
    {
        _serviceProvider = fixture.Services;
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
