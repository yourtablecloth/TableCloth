using Microsoft.Extensions.DependencyInjection;
using TableCloth.Components;

namespace TableCloth.Test;

public sealed class ResourceResolverTest : IClassFixture<ContainerFixture<ResourceResolverTest.Dependencies>>
{
    public sealed record Dependencies(
        ResourceResolver ResourceResolver);

    public ResourceResolverTest(ContainerFixture<Dependencies> fixture)
        => _dependencies = fixture.GetConsumer();

    private readonly Dependencies _dependencies;

    [Fact]
    public async Task TestGetLatestVersion()
    {
        // Arrange
        const string repoOwner = "yourtablecloth";
        const string repoName = "TableCloth";

        // Act
        var result = await _dependencies.ResourceResolver.GetLatestVersion(repoOwner, repoName);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
