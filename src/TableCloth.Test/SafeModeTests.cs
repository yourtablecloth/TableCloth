using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using TableCloth.Components;
using TableCloth.Components.Implementations;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.Serialization;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class SafeModeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SafeModeSkipsAllHostStagingAndSavedPreferences(bool savedDnsFallback)
    {
        var settings = AllSharingEnabled();
        settings.EnableSandboxPublicDnsFallback = savedDnsFallback;
        var preferences = new Preferences(settings);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Safe Mode Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        var config = new TableClothConfiguration
        {
            EnableSafeMode = true,
            EnableMicrophone = true, EnableWebCam = true, EnablePrinters = true, EnableSandboxGpuAcceleration = true,
            HostDotnetRootPath = "C:\\host-runtime", AssetsDirectoryPath = "C:\\host-app",
            MappedFolders = settings.MappedFolders,
            CertPair = new X509CertPair(certificate.Export(X509ContentType.Cert), key.ExportPkcs8PrivateKey()),
        };
        var output = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "safe-mode-" + Guid.NewGuid().ToString("N")));
        try
        {
            // Null collaborators deliberately fail if image expansion, install/cache copies or UI staging are reached.
            var builder = new SandboxBuilder(null!, null!, null!, preferences);
            var excluded = new List<SandboxMappedFolder>();
            var path = await builder.GenerateSandboxConfigurationAsync(output.FullName, config, excluded);
            Assert.AreEqual("SafeMode.wsb", Path.GetFileName(path));
            AssertPolicy(XDocument.Load(path!).Root!);
            Assert.HasCount(1, output.GetFileSystemInfos());
            var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts");
            Directory.CreateDirectory(artifactDirectory);
            File.Copy(path!, Path.Combine(artifactDirectory, "SafeMode.wsb"), true);
            Assert.AreEqual(0, preferences.LoadCount);
            Assert.Contains("if ($true -and", XDocument.Load(path!).Root!.Element("LogonCommand")!.Value);
            Assert.IsEmpty(excluded);
            Assert.IsTrue(config.EnableSafeMode);
            Assert.IsTrue(settings.ShareNpkiFolder);
            Assert.IsTrue(settings.EnableZScalerRootCertPropagation);
            Assert.IsTrue(settings.EnableSandboxGpuAcceleration);
            Assert.HasCount(1, settings.MappedFolders);
        }
        finally { output.Delete(true); }
    }

    [TestMethod]
    [DataRow("site")]
    [DataRow("target")]
    [DataRow("browser")]
    public async Task SafeModeCannotBeCombinedWithOtherLaunchRoutes(string route)
    {
        var config = new TableClothConfiguration { EnableSafeMode = true };
        if (route == "site") config.Services = [new() { Id = "bank", Url = "https://www.example.com/" }];
        if (route == "target") config.TargetUrl = "https://www.example.com/";
        if (route == "browser") config.BrowserOnlyUrl = "https://www.example.com/";
        var output = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "safe-route-" + Guid.NewGuid().ToString("N")));
        try
        {
            var builder = new SandboxBuilder(null!, null!, null!, null!);
            await Assert.ThrowsExactlyAsync<ArgumentException>(() =>
                builder.GenerateSandboxConfigurationAsync(output.FullName, config, new List<SandboxMappedFolder>()));
            Assert.IsEmpty(output.GetFileSystemInfos());
        }
        finally { output.Delete(true); }
    }

    [TestMethod]
    public void LegacySafeModePreferenceIsIgnoredAndNotSavedAgain()
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize(
            """{"EnableSafeMode":true,"ShareNpkiFolder":true,"MappedFolders":[{"HostFolder":"C:\\\\Data"}]}""",
            TableClothJsonContext.Default.PreferenceSettings)!;
        var json = System.Text.Json.JsonSerializer.Serialize(settings, TableClothJsonContext.Default.PreferenceSettings);
        var restored = System.Text.Json.JsonSerializer.Deserialize(json, TableClothJsonContext.Default.PreferenceSettings)!;
        Assert.DoesNotContain("EnableSafeMode", json);
        Assert.IsTrue(restored.ShareNpkiFolder);
        Assert.HasCount(1, restored.MappedFolders);
    }

    [TestMethod]
    public void SiteSelectionAndUrlUseTheExistingCatalogAndQuoteContract()
    {
        var config = new TableClothConfiguration
        {
            Services = [new() { Id = "bank-a", Url = "https://www.example.com/" }, new() { Id = "bank-b", Url = "https://other.example.com/" }],
            TargetUrl = "https://www.example.com/path?q=';&next=$(`x)&a=1",
        };
        var spec = SafeModeSandboxSpec.Create(config);
        var root = XDocument.Parse(spec).Root!;
        AssertPolicy(root);
        var command = root.Element("LogonCommand")!.Element("Command")!.Value;
        Assert.Contains("bank-a bank-b", command);
        Assert.Contains("q=%27;&next=$(`x)&a=1", command);
        Assert.Contains("if ($true -and", command);
        Assert.Contains("guest executable=", command);
        Assert.DoesNotContain("__SAFE_", command);
        Assert.DoesNotContain("-EncodedCommand", command);
        Assert.Contains("&amp;", spec);
    }

    [TestMethod]
    [DataRow("https://evil.example.net/")]
    [DataRow("https://user@www.example.com/")]
    [DataRow("file:///C:/Windows/system.ini")]
    [DataRow("https://www.example.com/\";boom")]
    public void UntrustedTargetUrlsAreRejected(string target)
    {
        var config = new TableClothConfiguration { Services = [new() { Id = "bank", Url = "https://www.example.com/" }], TargetUrl = target };
        Assert.ThrowsExactly<ArgumentException>(() => SafeModeSandboxSpec.Create(config));
    }

    [TestMethod]
    [DataRow("bank';boom")]
    [DataRow("bank other")]
    [DataRow("--help")]
    public void UntrustedSiteIdsAreRejected(string id)
    {
        Assert.ThrowsExactly<ArgumentException>(() => SafeModeSandboxSpec.Create(
            new TableClothConfiguration { Services = [new() { Id = id }] }));
    }

    [TestMethod]
    public async Task SafeModeButtonSkipsInputSelectionPreferencesAndHostFolders()
    {
        var launcher = new Launcher();
        var locations = new Locations();
        // The dedicated button must not read preferences, check host folders or route the normal input.
        var vm = new QuickStartPageViewModel(null!, null!, locations, launcher, null!,
            new TaskFactory(), null!, null!);
        var service = new CatalogInternetService { Id = "bank", Url = "https://www.example.com/" };
        vm.PreselectedServices = [service];
        vm.PreselectedTargetUrl = "https://www.example.com/path";
        vm.StartInput = "은행 사이트를 찾아 주세요.";
        await vm.LaunchSafeModeCommand.ExecuteAsync(null);
        var config = launcher.Configuration!;
        Assert.IsTrue(config.EnableSafeMode);
        Assert.IsEmpty(config.MappedFolders);
        Assert.IsNull(config.CertPair);
        Assert.IsEmpty(config.Services);
        Assert.IsNull(config.TargetUrl);
        Assert.AreEqual("은행 사이트를 찾아 주세요.", vm.StartInput);
        Assert.IsFalse(vm.IsStarting);
        Assert.AreEqual(0, locations.MountChecks);
    }

    [TestMethod]
    public async Task LegacySafeModeSettingDoesNotChangeNormalDeepLinkLaunch()
    {
        var data = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "normal-mode-" + Guid.NewGuid().ToString("N")));
        try
        {
            var settings = System.Text.Json.JsonSerializer.Deserialize(
                """{"EnableSafeMode":true,"UseAudioRedirection":true,"ShareNpkiFolder":false}""",
                TableClothJsonContext.Default.PreferenceSettings)!;
            settings.LastDisclaimerAgreedTime = DateTime.UtcNow;
            settings.DataDirectoryHostPath = data.FullName;
            var launcher = new Launcher();
            var locations = new Locations { Mountable = true };
            var vm = new QuickStartPageViewModel(new Preferences(settings), null!, locations, launcher, null!,
                new TaskFactory(), null!, null!);
            var service = new CatalogInternetService { Id = "bank", Url = "https://www.example.com/" };
            Assert.IsTrue(await vm.LaunchForDeepLinkAsync([service], "https://www.example.com/path"));
            var config = launcher.Configuration!;
            Assert.IsFalse(config.EnableSafeMode);
            Assert.IsTrue(config.EnableMicrophone);
            Assert.AreEqual(data.FullName, config.MappedFolders.Single().HostFolder);
            Assert.AreSame(service, config.Services.Single());
            Assert.AreEqual("https://www.example.com/path", config.TargetUrl);
            Assert.AreEqual(1, locations.MountChecks);
        }
        finally { data.Delete(true); }
    }

    [TestMethod]
    [DataRow("ko-KR", "샌드박스를 시작하는데 문제가 있으신가요?")]
    [DataRow("en-US", "Having trouble starting the sandbox?")]
    public async Task TroubleshootingLinkLaunchesSafeModeAndSettingsHaveNoToggle(string culture, string label)
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var preferences = new Preferences(AllSharingEnabled());
            var vm = new OptionsWindowViewModel(preferences, null!, null!, new Locations(), new TaskFactory(), null!);
            vm.SetInitialTab(OptionsTabKeys.Session);
            await vm.OptionsWindowLoadedCommand.ExecuteAsync(null);
            var window = new OptionsWindow { DataContext = vm, Width = 600, Height = 480 };
            var launcher = new Launcher();
            var quickStartVm = new QuickStartPageViewModel(null!, null!, null!, launcher, null!, new TaskFactory(), null!, null!);
            var page = new QuickStartPage { DataContext = quickStartVm };
            var mainWindow = new Window { Content = page, Width = 800, Height = 680 };
            var confirmation = new MessageBoxWindow(SafeModeText.Confirmation, UIStringResources.TitleText_Info,
                AppMessageBoxButton.YesNo, AppMessageBoxImage.Question, AppMessageBoxResult.No);
            try
            {
                window.Show(); await Task.Yield();
                Assert.IsNull(window.FindControl<CheckBox>("SafeModeToggle"));
                mainWindow.Show(); await Task.Yield();
                var button = page.FindControl<Button>("SafeModeStartLink")!;
                Assert.AreEqual(label, button.Content);
                Assert.IsTrue(button.Classes.Contains("link"));
                Assert.IsFalse(button.IsDefault);
                Assert.IsTrue(button.IsEnabled);
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), mainWindow);
                Assert.IsNotNull(point);
                mainWindow.MouseDown(point.Value, Avalonia.Input.MouseButton.Left);
                mainWindow.MouseUp(point.Value, Avalonia.Input.MouseButton.Left);
                await Task.Yield();
                Assert.IsTrue(launcher.Configuration!.EnableSafeMode);
                quickStartVm.IsStarting = true; Dispatcher.UIThread.RunJobs();
                Assert.IsFalse(button.IsEnabled);
                quickStartVm.IsStarting = false;
                Assert.IsTrue(preferences.Value.UseAudioRedirection);
                Assert.IsTrue(preferences.Value.ShareNpkiFolder);
                Assert.IsTrue(preferences.Value.EnableSandboxGpuAcceleration);
                Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = mainWindow.CaptureRenderedFrame(); Assert.IsNotNull(frame);
                var directory = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts");
                Directory.CreateDirectory(directory); frame.Save(Path.Combine(directory, $"safe-mode-start-{culture}.png"));
                confirmation.Show(); await Task.Yield();
                Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var confirmationFrame = confirmation.CaptureRenderedFrame();
                Assert.IsNotNull(confirmationFrame);
                confirmationFrame.Save(Path.Combine(directory, $"safe-mode-confirmation-{culture}.png"));
                Assert.HasCount(1, preferences.Value.MappedFolders);
            }
            finally { confirmation.Close(); mainWindow.Close(); window.Close(); CultureInfo.CurrentUICulture = previous; }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    internal static void AssertPolicy(XElement root)
    {
        Assert.IsNull(root.Element("MappedFolders"));
        Assert.IsEmpty(root.Descendants("MappedFolder"));
        Assert.AreEqual("Enable", root.Element("Networking")?.Value);
        foreach (var name in new[] { "ClipboardRedirection", "PrinterRedirection", "VideoInput", "AudioInput", "vGPU" })
            Assert.AreEqual("Disable", root.Element(name)?.Value, name);
    }

    private static PreferenceSettings AllSharingEnabled() => new()
    {
        UseAudioRedirection = true, UseVideoRedirection = true, UsePrinterRedirection = true,
        ShareNpkiFolder = true, EnableSandboxGpuAcceleration = true, EnableZScalerRootCertPropagation = true,
        EnableIdleAutoLogout = true, MappedFolders = [new() { HostFolder = "C:\\private-host-data" }],
        DataDirectoryHostPath = "\\\\server\\Data",
    };

    internal sealed class Preferences(PreferenceSettings value) : IPreferencesManager
    {
        public PreferenceSettings Value = value;
        public int LoadCount;
        public PreferenceSettings GetDefaultPreferences() => new();
        public Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken cancellationToken = default)
        { LoadCount++; return Task.FromResult<PreferenceSettings?>(Value); }
        public Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken cancellationToken = default)
        { Value = preferences; return Task.CompletedTask; }
    }

    private sealed class Launcher : ISandboxLauncher
    {
        public TableClothConfiguration? Configuration;
        public Task<bool> RunSandboxAsync(TableClothConfiguration config, CancellationToken cancellationToken = default)
        { Configuration = config; return Task.FromResult(true); }
        public bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason) { reason = null; return true; }
    }

    private sealed class Locations : ISharedLocations
    {
        public int MountChecks;
        public bool Mountable;
        public string GetEffectiveDataDirectoryPath(string? configuredPath) => configuredPath ?? "C:\\Data";
        public bool IsMountableDataDirectory(string? path) { MountChecks++; return Mountable; }
        public string ExecutableDirectoryPath => throw new InvalidOperationException();
        public string ImagesZipFilePath => throw new InvalidOperationException();
        public string CatalogCacheFilePath => throw new InvalidOperationException();
        public string AppDataDirectoryPath => throw new InvalidOperationException();
        public string ApplicationLogPath => throw new InvalidOperationException();
        public string ExecutableFilePath => throw new InvalidOperationException();
        public string PreferencesFilePath => throw new InvalidOperationException();
        public string DefaultDataDirectoryPath => "C:\\Data";
        public string GetImageDirectoryPath() => throw new InvalidOperationException();
        public string GetImageFilePath(string serviceId) => throw new InvalidOperationException();
        public string GetIconFilePath(string serviceId) => throw new InvalidOperationException();
        public string GetTempPath() => throw new InvalidOperationException();
        public string GetHelpFilePath() => throw new InvalidOperationException();
        public string GetSandboxAppStagingPath(string sandboxStagingDirectory) => throw new InvalidOperationException();
    }
}
