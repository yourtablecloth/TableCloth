using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TableCloth.Bootstrap.Dialogs;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using TableCloth.Serialization;

namespace TableCloth.Bootstrap;

/// <summary>
/// 이슈 #296: WPF 시절 Program.cs 에서 호스트 빌드 전에 <c>new LicenseWindow().ShowDialog()</c> 로 처리하던
/// 라이선스 동의 게이트를, Avalonia 로 전환하며 App 라이프사이클(<c>OnFrameworkInitializationCompleted</c>) 안으로
/// 이관한 것. Avalonia 창은 앱 라이프타임이 시작된 뒤에만 띄울 수 있기 때문이다. 파일 기반 동의 여부 확인/저장은
/// 기존 로직을 그대로 옮겼다.
/// </summary>
public static class LicenseGate
{
    /// <summary>
    /// 이미 동의했으면 즉시 true. 아니면 라이선스 창을 모달로 띄우고, 동의 시 저장 후 true,
    /// 거부 시 안내 메시지를 띄우고 false 를 반환한다. 반드시 UI 스레드에서 호출한다.
    /// </summary>
    public static bool EnsureAgreed()
    {
        if (IsLicenseAgreed())
            return true;

        var window = new LicenseWindow();
        var agreed = DialogHost.ShowModal(window, null) == true && window.LicenseAccepted;

        if (agreed)
        {
            SaveLicenseAgreement();
            return true;
        }

        MessageBoxWindow.ShowModal(
            null,
            UIStringResources.License_RejectionMessage,
            UIStringResources.License_RejectionTitle,
            AppMessageBoxButton.OK,
            AppMessageBoxImage.Information,
            AppMessageBoxResult.OK);
        return false;
    }

    private static bool IsLicenseAgreed()
    {
        try
        {
            var preferencesPath = GetPreferencesFilePath();
            if (!File.Exists(preferencesPath))
                return false;

            var json = File.ReadAllText(preferencesPath);
            var preferences = JsonSerializer.Deserialize(json, TableClothJsonContext.Default.PreferenceSettings);
            return preferences?.LicenseAgreedTime != null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TableCloth] IsLicenseAgreed failed: {ex}");
            return false;
        }
    }

    private static void SaveLicenseAgreement()
    {
        try
        {
            var preferencesPath = GetPreferencesFilePath();
            var preferencesDir = Path.GetDirectoryName(preferencesPath);

            if (!string.IsNullOrEmpty(preferencesDir) && !Directory.Exists(preferencesDir))
                Directory.CreateDirectory(preferencesDir);

            PreferenceSettings preferences;

            if (File.Exists(preferencesPath))
            {
                var json = File.ReadAllText(preferencesPath);
                preferences = JsonSerializer.Deserialize(json, TableClothJsonContext.Default.PreferenceSettings) ?? new PreferenceSettings();
            }
            else
            {
                preferences = new PreferenceSettings();
            }

            preferences.LicenseAgreedTime = DateTime.UtcNow;
            preferences.LicenseAgreedVersion = typeof(LicenseGate).Assembly.GetName().Version?.ToString();

            var updatedJson = JsonSerializer.Serialize(preferences, TableClothJsonContext.Default.PreferenceSettings);
            File.WriteAllText(preferencesPath, updatedJson);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TableCloth] SaveLicenseAgreement failed: {ex}");
        }
    }

    private static string GetPreferencesFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "TableCloth.Data", "Preferences.json");
    }
}
