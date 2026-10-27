using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TableCloth.App.DependencyInjection;
using TableCloth.Components;
using TableCloth.Components.Implementations;
using TableCloth.ManagedAi;
using TableCloth.Models.Configuration;

namespace TableCloth.Test;

[TestClass]
public sealed class ManagedAiNetworkSettingsTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExistingCompatibilityPreferenceControlsSystemProxyForSkills(bool enabled)
    {
        var preferences = new Preferences { Value = new() { EnableZScalerRootCertPropagation = enabled } };
        var settings = await new ManagedAiNetworkSettings(preferences).GetAsync(CancellationToken.None);

        Assert.AreEqual(enabled, settings.UseSystemProxyForSkills);
        Assert.AreEqual(1, preferences.Loads);
    }

    [TestMethod]
    public async Task MissingPreferencesKeepSystemProxyDisabled()
    {
        var preferences = new Preferences { Value = null };
        var settings = await new ManagedAiNetworkSettings(preferences).GetAsync(CancellationToken.None);

        Assert.IsFalse(settings.UseSystemProxyForSkills);
        Assert.AreEqual(0, preferences.Saves);
    }

    [TestMethod]
    public async Task EachRequestObservesUpdatedPreferencesWithoutMutatingThem()
    {
        var preferences = new Preferences { Value = new() };
        var adapter = new ManagedAiNetworkSettings(preferences);
        Assert.IsFalse((await adapter.GetAsync(CancellationToken.None)).UseSystemProxyForSkills);

        preferences.Value!.EnableZScalerRootCertPropagation = true;
        Assert.IsTrue((await adapter.GetAsync(CancellationToken.None)).UseSystemProxyForSkills);

        preferences.Value.EnableZScalerRootCertPropagation = false;
        Assert.IsFalse((await adapter.GetAsync(CancellationToken.None)).UseSystemProxyForSkills);
        Assert.AreEqual(3, preferences.Loads);
        Assert.AreEqual(0, preferences.Saves);
    }

    [TestMethod]
    public async Task AlreadyCancelledRequestDoesNotReadPreferences()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var preferences = new Preferences { Value = new() { EnableZScalerRootCertPropagation = true } };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new ManagedAiNetworkSettings(preferences).GetAsync(cancellation.Token));
        Assert.AreEqual(0, preferences.Loads);
    }

    [TestMethod]
    public async Task CancelledPreferenceReadCannotReturnAnEnabledFallback()
    {
        using var cancellation = new CancellationTokenSource();
        var preferences = new Preferences
        {
            Value = new() { EnableZScalerRootCertPropagation = true },
            OnLoad = cancellation.Cancel
        };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new ManagedAiNetworkSettings(preferences).GetAsync(cancellation.Token));
        Assert.AreEqual(cancellation.Token, preferences.ReceivedToken);
        Assert.AreEqual(1, preferences.Loads);
    }

    [TestMethod]
    public async Task ApplicationCompositionReplacesTheDefaultSettingsWithThePreferenceAdapter()
    {
        var preferences = new Preferences { Value = new() { EnableZScalerRootCertPropagation = true } };
        var builder = Host.CreateApplicationBuilder();
        builder.UseTableCloth();
        builder.Services.Replace(ServiceDescriptor.Singleton<IPreferencesManager>(preferences));
        var registrations = builder.Services.Where(x => x.ServiceType == typeof(IManagedAiNetworkSettings)).ToArray();
        Assert.HasCount(1, registrations);
        using var provider = builder.Services.BuildServiceProvider();

        var settings = provider.GetRequiredService<IManagedAiNetworkSettings>();
        Assert.IsInstanceOfType<ManagedAiNetworkSettings>(settings);
        Assert.AreSame(settings, provider.GetRequiredService<IManagedAiNetworkSettings>());
        Assert.IsTrue((await settings.GetAsync(CancellationToken.None)).UseSystemProxyForSkills);
        Assert.AreEqual(1, preferences.Loads);
    }

    private sealed class Preferences : IPreferencesManager
    {
        public PreferenceSettings? Value { get; set; }
        public Action? OnLoad { get; init; }
        public CancellationToken ReceivedToken { get; private set; }
        public int Loads { get; private set; }
        public int Saves { get; private set; }
        public PreferenceSettings GetDefaultPreferences() => new();
        public Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken cancellationToken = default)
        {
            Loads++;
            ReceivedToken = cancellationToken;
            OnLoad?.Invoke();
            return Task.FromResult(Value);
        }
        public Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken cancellationToken = default)
        {
            Saves++;
            throw new AssertFailedException("Network settings must never modify application preferences.");
        }
    }
}
