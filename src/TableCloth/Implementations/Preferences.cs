using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using TableCloth.Contracts;
using TableCloth.Models.Configuration;

namespace TableCloth.Implementations
{
    public sealed class Preferences : IPreferences
    {
        public Preferences(
            ISharedLocations sharedLocations,
            ILogger<Preferences> logger)
        {
            _sharedLocations = sharedLocations;
            _logger = logger;
        }

        private readonly ISharedLocations _sharedLocations;
        private readonly ILogger _logger;

        public PreferenceSettings LoadConfig()
        {
            var defaultSettings = GetDefaultConfig();
            var prefFilePath = _sharedLocations.GetDataPath("Preferences.json");

            if (!File.Exists(prefFilePath))
                return defaultSettings;

            PreferenceSettings settings = null;

            try
            {
                settings = JsonSerializer.Deserialize<PreferenceSettings>(
                    File.ReadAllText(prefFilePath),
                    new JsonSerializerOptions() { AllowTrailingCommas = true, });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot deserialize configuration settings.");
                settings = defaultSettings;
            }

            return settings;
        }

        public PreferenceSettings GetDefaultConfig()
            => new PreferenceSettings();

        public void SaveConfig(PreferenceSettings config)
        {
            var defaultSettings = GetDefaultConfig();

            if (config == null)
                config = defaultSettings;

            var prefFilePath = _sharedLocations.GetDataPath("Preferences.json");
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions() { AllowTrailingCommas = true, WriteIndented = true, });
            File.WriteAllText(prefFilePath, json, new UTF8Encoding(false));
        }
    }
}
