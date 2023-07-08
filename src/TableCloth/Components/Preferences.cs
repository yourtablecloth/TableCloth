using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using TableCloth.Models.Configuration;

namespace TableCloth.Components
{
    public sealed class Preferences
    {
        public Preferences(
            SharedLocations sharedLocations,
            ILogger<Preferences> logger)
        {
            _sharedLocations = sharedLocations;
            _logger = logger;
        }

        private readonly SharedLocations _sharedLocations;
        private readonly ILogger _logger;

        public PreferenceSettings LoadPreferences()
        {
            var defaultSettings = GetDefaultPreferences();
            var prefFilePath = _sharedLocations.PreferencesFilePath;

            if (!File.Exists(prefFilePath))
                return defaultSettings;

            PreferenceSettings settings;

            try
            {
                settings = JsonSerializer.Deserialize<PreferenceSettings>(
                    File.ReadAllText(prefFilePath),
                    new JsonSerializerOptions() { AllowTrailingCommas = true, });
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

        public void SavePreferences(PreferenceSettings preferences)
        {
            var defaultPreferences = GetDefaultPreferences();

            if (preferences == null)
                preferences = defaultPreferences;

            var prefFilePath = _sharedLocations.PreferencesFilePath;
            var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions() { AllowTrailingCommas = true, WriteIndented = true, });
            File.WriteAllText(prefFilePath, json, new UTF8Encoding(false));
        }
    }
}
