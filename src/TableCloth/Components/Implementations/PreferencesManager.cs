using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Configuration;

namespace TableCloth.Components.Implementations;

public sealed class PreferencesManager(
    ISharedLocations sharedLocations,
    ILogger<PreferencesManager> logger) : IPreferencesManager
{
    private readonly ILogger _logger = logger;

    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

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
                stream, Options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot deserialize preferences.");
            settings = defaultSettings;
        }

        return settings;
    }

    public PreferenceSettings GetDefaultPreferences()
        => new();

    public async Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken cancellationToken = default)
    {
        preferences ??= GetDefaultPreferences();
        var prefFilePath = sharedLocations.PreferencesFilePath;

        using var stream = File.OpenWrite(prefFilePath);
        await JsonSerializer.SerializeAsync(stream, preferences,
            Options, cancellationToken).ConfigureAwait(false);
    }
}
