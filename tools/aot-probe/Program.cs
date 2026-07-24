using System.Management;
using System.Text.Json;
using System.Text.Json.Serialization;
using Velopack;
using Velopack.Sources;

Console.WriteLine("=== AOT PROBE START ===");

// (1) WMI — System.Management. Mirrors AppStartup.cs hypervisor probe.
try
{
    using var searcher = new ManagementObjectSearcher("select HyperVisorPresent from Win32_ComputerSystem");
    foreach (var o in searcher.Get())
        Console.WriteLine($"[WMI] HyperVisorPresent = {o.GetPropertyValue("HyperVisorPresent")}");
}
catch (Exception ex)
{
    Console.WriteLine($"[WMI] runtime exception: {ex.GetType().Name}: {ex.Message}");
}

// (2) System.Text.Json — source-generated context (the AOT-safe path we would migrate to).
var settings = new ProbeSettings { Name = "probe", Count = 42 };
var json = JsonSerializer.Serialize(settings, ProbeJsonContext.Default.ProbeSettings);
var round = JsonSerializer.Deserialize(json, ProbeJsonContext.Default.ProbeSettings);
Console.WriteLine($"[JSON] serialized = {json}; roundtrip.Count = {round?.Count}");

// (2b) System.Text.Json — reflection path (what most stores use TODAY). Should raise IL2026/IL3050.
try
{
#pragma warning disable IL2026, IL3050
    var reflectionJson = JsonSerializer.Serialize(settings);
    Console.WriteLine($"[JSON-reflection] {reflectionJson}");
#pragma warning restore IL2026, IL3050
}
catch (Exception ex)
{
    Console.WriteLine($"[JSON-reflection] runtime exception: {ex.GetType().Name}: {ex.Message}");
}

// (3) Velopack — real API used by AppUpdateManager.cs
try
{
    var mgr = new UpdateManager(new GithubSource("https://github.com/yourtablecloth/TableCloth", null, false));
    Console.WriteLine($"[Velopack] UpdateManager created; IsInstalled = {mgr.IsInstalled}");
}
catch (Exception ex)
{
    Console.WriteLine($"[Velopack] runtime exception: {ex.GetType().Name}: {ex.Message}");
}

// (4) Sentry — real init pattern from UseSporkExtensions.cs / SplashScreenViewModel.cs
try
{
    using var _ = SentrySdk.Init(o =>
    {
        o.Dsn = string.Empty;         // empty DSN → disabled, but still exercises the init path
        o.AutoSessionTracking = false;
    });
    Console.WriteLine("[Sentry] SentrySdk.Init completed");
}
catch (Exception ex)
{
    Console.WriteLine($"[Sentry] runtime exception: {ex.GetType().Name}: {ex.Message}");
}

// (5) HyperVisorPresent replacement candidate — CPUID leaf 1, ECX bit 31 (AOT-safe intrinsic).
//     This is the proposed non-WMI substitute for AppStartup.cs's Win32_ComputerSystem.HyperVisorPresent.
try
{
    if (System.Runtime.Intrinsics.X86.X86Base.IsSupported)
    {
        var (_, _, ecx, _) = System.Runtime.Intrinsics.X86.X86Base.CpuId(1, 0);
        bool hyperVisorPresent = (ecx & (1 << 31)) != 0;
        Console.WriteLine($"[CPUID] X86Base supported; hypervisor-present bit (ECX[31]) = {hyperVisorPresent}");
    }
    else
    {
        Console.WriteLine("[CPUID] X86Base not supported on this arch (e.g. ARM64) — fall back to conservative 'unknown'.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[CPUID] runtime exception: {ex.GetType().Name}: {ex.Message}");
}

Console.WriteLine("=== AOT PROBE END ===");

public sealed class ProbeSettings
{
    public string? Name { get; set; }
    public int Count { get; set; }
}

[JsonSerializable(typeof(ProbeSettings))]
public partial class ProbeJsonContext : JsonSerializerContext { }
