using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.Components.Implementations;

internal static class SafeModeSandboxSpec
{
    internal const string GuestReleaseUrl = "https://github.com/yourtablecloth/TableCloth/releases/latest";

    internal static string Create(TableClothConfiguration config, bool publicDnsFallback)
    {
        var services = config.Services.ToList();
        var ids = services.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (ids.Any(id => string.IsNullOrEmpty(id) || id.Length > 128 || id.StartsWith('-') || id.Contains("__SAFE_", StringComparison.Ordinal) ||
            id.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.'))))
            throw new ArgumentException("Invalid Safe Mode site ID.", nameof(config));

        var target = string.Empty;
        if (!string.IsNullOrWhiteSpace(config.TargetUrl))
        {
            var match = CatalogTargetUrlMatcher.Match(new CatalogDocument { Services = services }, config.TargetUrl, ids);
            if (!match.IsAccepted)
                throw new ArgumentException("Safe Mode target URL does not match the selected catalog services.", nameof(config));
            target = match.AcceptedUrl;
        }

        // Apply the existing no-install WSB quote/argv contract, then let XElement escape XML.
        target = target.Replace("'", "%27", StringComparison.Ordinal).Replace("\"", "%22", StringComparison.Ordinal);
        var script = string.Join(' ', ReadResource("SafeModeStartup.ps1").Split('\n')
            .Select(line => line.Trim()).Where(line => !line.StartsWith('#')))
            .Replace("__SAFE_DNS_FALLBACK__", publicDnsFallback ? "$true" : "$false", StringComparison.Ordinal)
            .Replace("__SAFE_SITE_IDS__", string.Join(' ', ids), StringComparison.Ordinal)
            .Replace("__SAFE_TARGET_URL__", target, StringComparison.Ordinal);
        var childArguments = "-NoProfile -ExecutionPolicy Bypass -Command " + QuoteWindowsArgument(script);
        var outer = "Start-Process powershell.exe -WindowStyle Normal -ArgumentList '" +
            childArguments.Replace("'", "''", StringComparison.Ordinal) + "'";
        var command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -Command " + QuoteWindowsArgument(outer);
        if (command.Length >= 32767)
            throw new ArgumentException("Too many Safe Mode sites for the native command-line limit.", nameof(config));

        return new XDocument(new XElement("Configuration",
            new XElement("Networking", "Enable"), new XElement("vGPU", "Disable"),
            new XElement("AudioInput", "Disable"), new XElement("VideoInput", "Disable"),
            new XElement("PrinterRedirection", "Disable"), new XElement("ClipboardRedirection", "Disable"),
            new XElement("LogonCommand", new XElement("Command", command)))).ToString();
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(SafeModeSandboxSpec).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Safe Mode resource is missing: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Escape the outer native argv layer without hiding the script in an EncodedCommand.
    private static string QuoteWindowsArgument(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\') { slashes++; continue; }
            result.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes);
            result.Append(ch);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
