using TableCloth.Components;

namespace TableCloth.Test;

public sealed class ResourceResolverTest : IClassFixture<ContainerFixture<ResourceResolverTest.Dependencies>>
{
    public sealed record Dependencies(
        IResourceResolver ResourceResolver);

    public ResourceResolverTest(ContainerFixture<Dependencies> fixture)
        => _dependencies = fixture.GetConsumer();

    private readonly Dependencies _dependencies;

    [Fact]
    public async Task TestGetLatestVersion()
    {
        // Given
        const string repoOwner = "yourtablecloth";
        const string repoName = "TableCloth";

        // When
        var result = await _dependencies.ResourceResolver.GetLatestVersion(repoOwner, repoName);

        // Then
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
