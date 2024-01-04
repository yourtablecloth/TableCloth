using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public interface IPreferencesManager
{
    PreferenceSettings GetDefaultPreferences();
    PreferenceSettings? LoadPreferences();
    void SavePreferences(PreferenceSettings preferences);
}