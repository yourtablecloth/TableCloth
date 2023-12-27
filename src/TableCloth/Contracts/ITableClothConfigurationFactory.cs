using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface ITableClothConfigurationFactory
    {
        TableClothConfiguration GetTableClothConfiguration(ITableClothArgumentModel argumentModel);
    }
}
