using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public sealed class PreferencesManager(
    ISharedLocations sharedLocations,
    ILogger<PreferencesManager> logger) : IPreferencesManager
{
    private readonly ILogger _logger = logger;

    public async Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var defaultSettings = GetDefaultPreferences();
        var prefFilePath = sharedLocations.PreferencesFilePath;

        if (!File.Exists(prefFilePath))
            return defaultSettings;

        PreferenceSettings? settings;

        try
        {
            using var stream = File.OpenRead(prefFilePath);
            settings = await JsonSerializer.DeserializeAsync<PreferenceSettings>(
                stream,
                new JsonSerializerOptions() { AllowTrailingCommas = true, },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot deserialize preferences.");
            settings = defaultSettings;
        }

        return settings;
    }

    public PreferenceSettings GetDefaultPreferences()
        => new PreferenceSettings();

    public async Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken cancellationToken = default)
    {
        var defaultPreferences = GetDefaultPreferences();

        if (preferences == null)
            preferences = defaultPreferences;

        var prefFilePath = sharedLocations.PreferencesFilePath;

        using var stream = File.OpenWrite(prefFilePath);
        await JsonSerializer.SerializeAsync(stream, preferences,
            new JsonSerializerOptions() { AllowTrailingCommas = true, WriteIndented = true, },
            cancellationToken).ConfigureAwait(false);
    }
}
