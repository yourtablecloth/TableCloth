using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using TableCloth.Components.Implementations;
using TableCloth.Models.Configuration;

namespace TableCloth.Test;

[TestClass]
public sealed class SafeModePowerShellTests
{
    [TestMethod]
    [DataRow("AMD64", "x64", false)]
    [DataRow("ARM64", "arm64", false)]
    [DataRow("AMD64", "x64", true)]
    [DataRow("ARM64", "arm64", true)]
    public async Task PrepareWorkerPreservesNativeArgumentsForCurrentAndLegacyReleases(string architecture, string expectedArch, bool legacy)
    {
        var directory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "safe-prepare-" + Guid.NewGuid().ToString("N")));
        try
        {
            var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "no-install", "tablecloth-prepare.ps1"));
            var worker = await File.ReadAllTextAsync(source);
            if (legacy) worker = worker.Replace("Start-Process $launcherPath -ArgumentList $quotedArgs", "Start-Process $launcherPath -ArgumentList $launcherArgs");
            var workerPath = Path.Combine(directory.FullName, "prepare.ps1");
            await File.WriteAllTextAsync(workerPath, worker);
            var arguments = Path.Combine(directory.FullName, "arguments.ps1");
            await File.WriteAllTextAsync(arguments, "ConvertTo-Json -InputObject @($args) -Compress");
            var harness = Path.Combine(directory.FullName, "harness.ps1");
            await File.WriteAllTextAsync(harness, """
                $ErrorActionPreference = 'Stop'
                function Invoke-WebRequest { param([switch]$UseBasicParsing, $Uri, $OutFile); $script:Download = $Uri }
                function Start-Sleep { param($Seconds) }
                function Start-Process {
                    param($FilePath, $ArgumentList)
                    $start = New-Object Diagnostics.ProcessStartInfo
                    $start.FileName = $env:TEST_EXECUTABLE
                    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $env:TEST_RECEIVER + '" ' + ($ArgumentList -join ' ')
                    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.RedirectStandardOutput = $true
                    $child = [Diagnostics.Process]::Start($start)
                    $output = $child.StandardOutput.ReadToEnd(); $child.WaitForExit()
                    if ($child.ExitCode) { throw 'Native argument receiver failed.' }
                    @{ Download=$script:Download; ArgumentsJson=$output } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $env:TEMP 'arguments.json')
                }
                & $env:TEST_WORKER
                """);
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", harness }) start.ArgumentList.Add(arg);
            start.Environment["TEMP"] = directory.FullName; start.Environment["PROCESSOR_ARCHITECTURE"] = architecture;
            start.Environment.Remove("PROCESSOR_ARCHITEW6432");
            start.Environment["TABLECLOTH_SITE_IDS"] = legacy ? "\"bank-a bank-b\"" : "bank-a bank-b";
            const string target = "https://www.example.com/?a=%27;&q=$(`x)&b=1";
            start.Environment["TABLECLOTH_TARGET_URL"] = target;
            start.Environment["TEST_EXECUTABLE"] = executable; start.Environment["TEST_RECEIVER"] = arguments; start.Environment["TEST_WORKER"] = workerPath;
            using var process = Process.Start(start)!;
            var outputTask = process.StandardOutput.ReadToEndAsync(); var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30)); await outputTask;
            Assert.AreEqual(0, process.ExitCode, await errorTask);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory.FullName, "arguments.json")));
            Assert.Contains("SporkBootstrap_" + expectedArch + ".exe", json.RootElement.GetProperty("Download").GetString()!);
            using var capturedArguments = JsonDocument.Parse(json.RootElement.GetProperty("ArgumentsJson").GetString()!);
            CollectionAssert.AreEqual(new[] { "--site-ids", "bank-a bank-b", "--target-url", target },
                capturedArguments.RootElement.EnumerateArray().Select(x => x.GetString()).ToArray());
        }
        finally { directory.Delete(true); }
    }

    [TestMethod]
    [DataRow("AMD64", "x64", 0)]
    [DataRow("ARM64", "arm64", 0)]
    [DataRow("AMD64", "x64", 1)]
    public async Task NativeCommandAndGuestScriptPreserveDataAndHandlePreparationFailure(string architecture, string expectedArch, int preparationExit)
    {
        var directory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "safe-shell-" + Guid.NewGuid().ToString("N")));
        try
        {
            const string target = "https://www.example.com/path?q=';&next=$(`x)&a=1";
            var spec = SafeModeSandboxSpec.Create(new TableClothConfiguration
            {
                Services = [new() { Id = "bank-a", Url = "https://www.example.com/" }, new() { Id = "bank-b", Url = "https://other.example.com/" }],
                TargetUrl = target,
            }, false);
            var command = XDocument.Parse(spec).Root!.Element("LogonCommand")!.Element("Command")!.Value;
            Assert.IsLessThan(32767, command.Length, "Native command-line length limit.");
            var receiver = Path.Combine(directory.FullName, "receiver.ps1");
            var innerReceiver = Path.Combine(directory.FullName, "inner.ps1");
            await File.WriteAllTextAsync(receiver, """
                param([string]$Outer)
                $ErrorActionPreference = 'Stop'
                function Start-Process { param($FilePath, $WindowStyle, $ArgumentList); $script:ChildArguments = $ArgumentList -join ' ' }
                Invoke-Expression $Outer
                $start = New-Object Diagnostics.ProcessStartInfo
                $start.FileName = $env:TEST_EXECUTABLE
                $tail = $script:ChildArguments.Substring($script:ChildArguments.IndexOf('-Command ') + 9)
                $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $env:TEST_INNER_RECEIVER + '" ' + $tail
                $start.UseShellExecute = $false; $start.CreateNoWindow = $true
                $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
                $child = New-Object Diagnostics.Process
                $child.StartInfo = $start; $null = $child.Start()
                $output = $child.StandardOutput.ReadToEndAsync(); $errorOutput = $child.StandardError.ReadToEndAsync()
                $child.WaitForExit()
                if ($child.ExitCode) { throw $errorOutput.GetAwaiter().GetResult() }
                [Console]::Write($output.GetAwaiter().GetResult())
                """);
            await File.WriteAllTextAsync(innerReceiver, """
                param([string]$Inner)
                $ErrorActionPreference = 'Stop'
                $tokens = $null; $parseErrors = $null
                $null = [Management.Automation.Language.Parser]::ParseInput($Inner, [ref]$tokens, [ref]$parseErrors)
                if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
                function powershell.exe { param([switch]$NoProfile, $ExecutionPolicy, $File); $global:LASTEXITCODE = [int]$env:TEST_PREPARE_EXIT }
                function Invoke-WebRequest { param([switch]$UseBasicParsing, $Uri, $OutFile); Copy-Item -LiteralPath $env:TEST_WORKER -Destination $OutFile }
                function Get-Process { [CmdletBinding()]param($Name); [pscustomobject]@{ Path = $env:TEST_EXECUTABLE } }
                function Out-File { param($LiteralPath, $Encoding); process { $_ | Set-Content -LiteralPath (Join-Path $env:TEMP 'log.txt') } }
                function Add-Content { param($LiteralPath); process { [IO.File]::AppendAllText((Join-Path $env:TEMP 'log.txt'), ($_ + [Environment]::NewLine)) } }
                function Read-Host { param($Prompt); 'closed' }
                Invoke-Expression $Inner | Out-Null
                $worker = [IO.File]::ReadAllText((Join-Path $env:TEMP 'tablecloth-prepare.ps1'))
                $null = [Management.Automation.Language.Parser]::ParseInput($worker, [ref]$tokens, [ref]$parseErrors)
                if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
                @{ SiteIds=$env:TABLECLOTH_SITE_IDS; TargetUrl=$env:TABLECLOTH_TARGET_URL; Mode=$env:TABLECLOTH_SAFE_MODE;
                   Log=[IO.File]::ReadAllText((Join-Path $env:TEMP 'log.txt')); Worker=$worker } | ConvertTo-Json -Compress
                """);
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo(executable)
            {
                // Feed the generated native argv tail to Windows PowerShell itself, without re-quoting it.
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{receiver}\" " + command[(command.IndexOf("-Command ", StringComparison.Ordinal) + 9)..],
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.Environment["TEMP"] = directory.FullName;
            start.Environment["PROCESSOR_ARCHITECTURE"] = architecture;
            start.Environment.Remove("PROCESSOR_ARCHITEW6432");
            start.Environment["TEST_PREPARE_EXIT"] = preparationExit.ToString();
            start.Environment["TEST_EXECUTABLE"] = executable;
            start.Environment["TEST_INNER_RECEIVER"] = innerReceiver;
            start.Environment["TEST_WORKER"] = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "no-install", "tablecloth-prepare.ps1"));
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.AreEqual(0, process.ExitCode, await error);
            var text = await output;
            using var json = JsonDocument.Parse(text[text.IndexOf('{')..]);
            Assert.AreEqual("bank-a bank-b", json.RootElement.GetProperty("SiteIds").GetString());
            Assert.AreEqual(target.Replace("'", "%27"), json.RootElement.GetProperty("TargetUrl").GetString());
            Assert.AreEqual("1", json.RootElement.GetProperty("Mode").GetString());
            var log = json.RootElement.GetProperty("Log").GetString()!;
            Assert.Contains("arch=" + expectedArch, log);
            Assert.Contains("SporkBootstrap_" + expectedArch + ".exe", log);
            Assert.Contains(preparationExit == 0 ? "guest executable=" : "The preparation script failed.", log);
            Assert.Contains("$quotedArgs", json.RootElement.GetProperty("Worker").GetString()!);
        }
        finally { directory.Delete(true); }
    }
}
