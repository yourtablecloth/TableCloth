using System.Collections.Generic;
using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface IPreferences
    {
        PreferenceSettings GetCurrentConfig();

        PreferenceSettings GetDefaultConfig();

        void SetConfig(PreferenceSettings config);
    }
}
