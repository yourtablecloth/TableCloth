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

        private void MigrateDesktopDataDirectory()
        {
            try
            {
                var oldPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TableCloth");
                var newPath = _sharedLocations.AppDataDirectoryPath;

                if (!Directory.Exists(oldPath))
                    return;

                var oldConfigFile = Path.Combine(oldPath, "Preferences.json");

                if (!File.Exists(oldConfigFile))
                    return;

                if (!Directory.Exists(newPath))
                    Directory.CreateDirectory(newPath);

                File.Copy(oldConfigFile, Path.Combine(newPath, "Preferences.json"), true);
            }
            catch { /* 마이그레이션에 실패하면 무시 */ }
        }

        public PreferenceSettings LoadConfig()
        {
            var defaultSettings = GetDefaultConfig();
            var prefFilePath = _sharedLocations.PreferencesFilePath;

            if (!File.Exists(prefFilePath))
                MigrateDesktopDataDirectory();

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

            var prefFilePath = _sharedLocations.PreferencesFilePath;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions() { AllowTrailingCommas = true, WriteIndented = true, });
            File.WriteAllText(prefFilePath, json, new UTF8Encoding(false));
        }
    }
}
