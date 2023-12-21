using Microsoft.Extensions.DependencyInjection;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Test;

public class ContainerFixture
{
    public ContainerFixture()
    {
        var svcCollection = new ServiceCollection();
        svcCollection
            .AddLogging()
            .AddHttpClient(nameof(TableCloth), c =>
            {
                c.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText);
            });

        svcCollection.AddSingleton<ResourceResolver>();
        ServiceProvider = svcCollection.BuildServiceProvider();
    }

    public ServiceProvider ServiceProvider { get; private set; }
}
