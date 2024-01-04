using TableCloth.Components;

namespace TableCloth.Test;

/*
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.AutoMock;
using System.Windows;

using static Moq.It;

public class AutoMoqTester
{
    [Fact]
    public void Test()
    {
        var autoMocker = new AutoMocker();
        autoMocker.Use<IAppMessageBox>(x => x.DisplayError(IsAny<string>(), IsAny<bool>()) == MessageBoxResult.OK);

        var appMessageBox = autoMocker.Get<IAppMessageBox>();
        var result = appMessageBox.DisplayError("Hello, World!", true);

        autoMocker.Verify<IAppMessageBox, MessageBoxResult>(x => x.DisplayError(IsAny<string>(), true));
        Assert.Equal(MessageBoxResult.OK, result);
    }
}
*/

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
