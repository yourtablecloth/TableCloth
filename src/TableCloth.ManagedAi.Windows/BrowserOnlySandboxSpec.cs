using System.Text;
using System.Xml.Linq;

namespace TableCloth.ManagedAi.Windows;

public static class BrowserOnlySandboxSpec
{
    public static string Create(Uri target)
    {
        var uri = PublicWebUrl.Validate(target.AbsoluteUri);
        // The selected URL is data, encoded separately from the fixed script, never shell syntax.
        var encodedUrl = Convert.ToBase64String(Encoding.UTF8.GetBytes(uri.AbsoluteUri));
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            $target = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{encodedUrl}}'))
            $edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
            if (!(Test-Path -LiteralPath $edge)) { $edge = Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe' }
            & $edge '--no-first-run' '--disable-gpu' '--inprivate' $target
            """;
        var command = "powershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand " +
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return new XDocument(new XElement("Configuration",
            new XElement("Networking", "Enable"), new XElement("vGPU", "Disable"),
            new XElement("AudioInput", "Disable"), new XElement("VideoInput", "Disable"),
            new XElement("PrinterRedirection", "Disable"), new XElement("ClipboardRedirection", "Disable"),
            new XElement("LogonCommand", new XElement("Command", command)))).ToString();
    }
}
