using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface ICanComposeConfiguration
    {
        TableClothConfiguration GetTableClothConfiguration();
    }
}
