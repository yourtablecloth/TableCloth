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

        private readonly PreferenceSettings _defaultSettings = new PreferenceSettings();

        public PreferenceSettings GetCurrentConfig()
        {
            var prefFilePath = _sharedLocations.GetDataPath("Preferences.json");

            if (!File.Exists(prefFilePath))
                return _defaultSettings;

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
                settings = _defaultSettings;
            }

            return settings;
        }

        public PreferenceSettings GetDefaultConfig()
            => _defaultSettings;

        public void SetConfig(PreferenceSettings config)
        {
            if (config == null)
                config = _defaultSettings;

            var prefFilePath = _sharedLocations.GetDataPath("Preferences.json");
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions() { AllowTrailingCommas = true, WriteIndented = true, });
            File.WriteAllText(prefFilePath, json, new UTF8Encoding(false));
        }
    }
}
