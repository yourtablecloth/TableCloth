using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TableCloth.Components;

namespace TableCloth.Test
{
    public class ResourceResolverTest : IClassFixture<ContainerFixture>
    {
        public ResourceResolverTest(ContainerFixture fixture)
        {
            serviceProvider = fixture.ServiceProvider;
            resourceResolver = serviceProvider.GetRequiredService<ResourceResolver>() ??
                throw new Exception("Cannot obtain resource resolver due to configuration");
        }

        private IServiceProvider serviceProvider;
        private ResourceResolver resourceResolver;

        [Fact]
        public async Task TestGetLatestVersion()
        {
            // Arrange
            const string repoOwner = "yourtablecloth";
            const string repoName = "TableCloth";

            // Act
            var result = await resourceResolver.GetLatestVersion(repoOwner, repoName);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
    }
}
