using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public interface IPreferencesManager
{
    PreferenceSettings GetDefaultPreferences();
    Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken cancellationToken = default);
    Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken cancellationToken = default);
}