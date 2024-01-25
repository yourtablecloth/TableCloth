using TableCloth.Components;

namespace TableCloth.Test;

public class CatalogTest
{
    public CatalogTest()
    {
        _testHost = TableClothApp.CreateHostBuilder(
            servicesBuilderOverride: services => services
                .ProvideMockupApplication()
                .ReplaceWithMock<IMessageBoxService>()
            ).Build();
    }

    private readonly IHost _testHost;

    [Fact]
    public void ValidateCatalog()
    {
        /*
        // given
        var sut = _testHost.Services.GetRequiredService<ICatalogDeserializer>();
        var targetFilePath = @"E:\Projects\TableClothCatalog\docs\Catalog.xml";

        // when
        var catalog = sut.Deserialize(File.OpenText(targetFilePath));

        // then
        Assert.NotNull(catalog);
        */
    }
}
