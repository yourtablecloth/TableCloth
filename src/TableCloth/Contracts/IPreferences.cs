using System.Collections.Generic;
using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface IPreferences
    {
        PreferenceSettings LoadConfig();

        PreferenceSettings GetDefaultConfig();

        void SaveConfig(PreferenceSettings config);
    }
}
