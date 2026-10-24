using System.Diagnostics;
using System.IO.Compression;
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
            var source = FindPrepareWorker();
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
                # Apply the fixture architecture after native PowerShell initializes its environment.
                $env:PROCESSOR_ARCHITECTURE = $env:TEST_ARCHITECTURE
                $env:PROCESSOR_ARCHITEW6432 = $null
                & $env:TEST_WORKER
                """);
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", harness }) start.ArgumentList.Add(arg);
            start.Environment["TEMP"] = directory.FullName; start.Environment["TEST_ARCHITECTURE"] = architecture;
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
    [DataRow("AMD64", "x64", "success")]
    [DataRow("ARM64", "arm64", "success")]
    [DataRow("AMD64", "x64", "download-failure")]
    [DataRow("AMD64", "x64", "unsafe-archive")]
    [DataRow("AMD64", "x64", "missing-executable")]
    [DataRow("AMD64", "x64", "images-failure")]
    [DataRow("AMD64", "x64", "empty-images")]
    [DataRow("AMD64", "x64", "early-exit")]
    public async Task NativeCommandPreparesGuestImagesBeforeLaunchAndHandlesFailures(string architecture, string expectedArch, string scenario)
    {
        var directory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "safe-shell-" + Guid.NewGuid().ToString("N")));
        try
        {
            const string target = "https://www.example.com/path?q=';&next=$(`x)&a=1";
            var spec = SafeModeSandboxSpec.Create(new TableClothConfiguration
            {
                Services = [new() { Id = "bank-a", Url = "https://www.example.com/" }, new() { Id = "bank-b", Url = "https://other.example.com/" }],
                TargetUrl = target,
            });
            var command = XDocument.Parse(spec).Root!.Element("LogonCommand")!.Element("Command")!.Value;
            Assert.IsLessThan(32767, command.Length, "Native command-line length limit.");
            var receiver = Path.Combine(directory.FullName, "receiver.ps1");
            var innerReceiver = Path.Combine(directory.FullName, "inner.ps1");
            var argumentsReceiver = Path.Combine(directory.FullName, "arguments.ps1");
            await File.WriteAllTextAsync(argumentsReceiver, "ConvertTo-Json -InputObject @($args) -Compress");
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            var packageFixture = Path.Combine(directory.FullName, "package.zip");
            using (var archive = ZipFile.Open(packageFixture, ZipArchiveMode.Create))
            {
                if (scenario == "unsafe-archive") archive.CreateEntry("../escaped.exe");
                else if (scenario == "missing-executable") archive.CreateEntry("current/readme.txt");
                else archive.CreateEntryFromFile(executable, "current/Spork.exe");
            }
            var imagesFixture = Path.Combine(directory.FullName, "images.zip");
            using (var archive = ZipFile.Open(imagesFixture, ZipArchiveMode.Create))
            {
                archive.CreateEntry("bank-a.ico");
                if (scenario != "empty-images")
                {
                    var entry = archive.CreateEntry("nested/bank-a.png");
                    using (var image = entry.Open())
                        image.Write(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/KkQAAAAASUVORK5CYII="));
                    // Only PNG filenames are copied into images; these entries are skipped.
                    archive.CreateEntry("../escape.txt");
                    archive.CreateEntry("bad name.png");
                }
            }
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
                function Resolve-DnsName { [CmdletBinding()]param($Name, [switch]$QuickTimeout); [pscustomobject]@{ Name=$Name } }
                function Invoke-WebRequest {
                    param([switch]$UseBasicParsing, [switch]$PassThru, $Uri, $OutFile)
                    $isImages = $Uri.EndsWith('/Images.zip')
                    if (($isImages -and $env:TEST_SCENARIO -eq 'images-failure') -or (-not $isImages -and $env:TEST_SCENARIO -eq 'download-failure')) { throw 'Fixture download failed.' }
                    $source = if ($isImages) { $env:TEST_IMAGES } else { $env:TEST_PACKAGE }
                    Copy-Item -LiteralPath $source -Destination $OutFile
                    [pscustomobject]@{ BaseResponse=[pscustomobject]@{ ResponseUri=[Uri]($Uri.Replace('/latest/download/', '/download/v1.21.1/')) } }
                }
                function Start-Process {
                    param($FilePath, $WorkingDirectory, $ArgumentList, [switch]$PassThru)
                    if (-not $PassThru) { throw 'Startup must request its process handle.' }
                    $script:LogosPresentAtLaunch = Test-Path -LiteralPath (Join-Path $WorkingDirectory 'images\bank-a.png')
                    $script:ImageFiles = @(Get-ChildItem -LiteralPath (Join-Path $WorkingDirectory 'images') -File -ErrorAction SilentlyContinue | ForEach-Object Name)
                    $start = New-Object Diagnostics.ProcessStartInfo
                    $start.FileName = $env:TEST_EXECUTABLE
                    $start.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $env:TEST_ARGUMENTS_RECEIVER + '" ' + ($ArgumentList -join ' ')
                    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.RedirectStandardOutput = $true
                    $child = [Diagnostics.Process]::Start($start)
                    $script:ArgumentsJson = $child.StandardOutput.ReadToEnd(); $child.WaitForExit()
                    if ($child.ExitCode) { throw 'Native argument receiver failed.' }
                    $process = [pscustomobject]@{ Id=12345; ExitCode=42 }
                    $process | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value {
                        param($milliseconds)
                        if ($milliseconds -ne 5000) { throw 'Unexpected initial startup interval.' }
                        $env:TEST_SCENARIO -eq 'early-exit'
                    }
                    $process
                }
                function Out-File { param($LiteralPath, $Encoding); process { $_ | Set-Content -LiteralPath (Join-Path $env:TEMP 'log.txt') } }
                function Add-Content { param($LiteralPath); process { [IO.File]::AppendAllText((Join-Path $env:TEMP 'log.txt'), ($_ + [Environment]::NewLine)) } }
                function Read-Host { param($Prompt); 'closed' }
                # This process runs the architecture-sensitive command, after the outer receiver starts it.
                $env:PROCESSOR_ARCHITECTURE = $env:TEST_ARCHITECTURE
                $env:PROCESSOR_ARCHITEW6432 = $null
                Invoke-Expression $Inner | Out-Null
                @{ SiteIds=$env:TABLECLOTH_SITE_IDS; TargetUrl=$env:TABLECLOTH_TARGET_URL; Mode=$env:TABLECLOTH_SAFE_MODE;
                   Log=[IO.File]::ReadAllText((Join-Path $env:TEMP 'log.txt')); LogosPresentAtLaunch=$script:LogosPresentAtLaunch;
                   ImageFiles=$script:ImageFiles; ArgumentsJson=$script:ArgumentsJson } | ConvertTo-Json -Compress
                """);
            var start = new ProcessStartInfo(executable)
            {
                // Feed the generated native argv tail to Windows PowerShell itself, without re-quoting it.
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{receiver}\" " + command[(command.IndexOf("-Command ", StringComparison.Ordinal) + 9)..],
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            start.Environment["TEMP"] = directory.FullName;
            start.Environment["TEST_ARCHITECTURE"] = architecture;
            start.Environment["TEST_SCENARIO"] = scenario;
            start.Environment["TEST_EXECUTABLE"] = executable;
            start.Environment["TEST_INNER_RECEIVER"] = innerReceiver;
            start.Environment["TEST_ARGUMENTS_RECEIVER"] = argumentsReceiver;
            start.Environment["TEST_PACKAGE"] = packageFixture;
            start.Environment["TEST_IMAGES"] = imagesFixture;
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
            Assert.Contains("Spork_" + expectedArch + "_Portable.zip", log);
            Assert.Contains("https://yourtablecloth.app/TableClothCatalog/Images.zip", log);
            var launched = scenario is "success" or "images-failure" or "empty-images" or "early-exit";
            if (launched)
            {
                Assert.Contains("guest executable=", log);
                Assert.Contains("/download/v1.21.1/Spork_", log);
                Assert.Contains("Spork launch requested.", log);
                using var capturedArguments = JsonDocument.Parse(json.RootElement.GetProperty("ArgumentsJson").GetString()!);
                CollectionAssert.AreEqual(new[] { "bank-a", "bank-b", "--target-url", target.Replace("'", "%27") },
                    capturedArguments.RootElement.EnumerateArray().Select(x => x.GetString()).ToArray());
                if (scenario is "success" or "early-exit")
                {
                    Assert.IsTrue(json.RootElement.GetProperty("LogosPresentAtLaunch").GetBoolean());
                    Assert.Contains("site logos=1", log);
                    CollectionAssert.AreEqual(new[] { "bank-a.png" },
                        json.RootElement.GetProperty("ImageFiles").EnumerateArray().Select(x => x.GetString()).ToArray());
                }
                else Assert.Contains("Site logos unavailable:", log);
                if (scenario == "early-exit")
                {
                    Assert.Contains("Spork closed during startup; exit code=42.", log);
                    Assert.DoesNotContain("Spork initial startup alive;", log);
                }
                else Assert.Contains("Spork initial startup alive; pid=12345", log);
            }
            else
            {
                Assert.AreEqual(JsonValueKind.Null, json.RootElement.GetProperty("ArgumentsJson").ValueKind);
                var expectedFailure = scenario switch
                {
                    "unsafe-archive" => "The application archive contains an invalid path.",
                    "missing-executable" => "Spork.exe was not found in the application archive.",
                    _ => "Fixture download failed.",
                };
                Assert.Contains(expectedFailure, log);
            }
            Assert.IsEmpty(Directory.GetFiles(directory.FullName, "escaped.exe", SearchOption.AllDirectories));
        }
        finally { directory.Delete(true); }
    }

    private static string FindPrepareWorker()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "tools", "no-install", "tablecloth-prepare.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("The repository's tablecloth-prepare.ps1 fixture was not found.");
    }
}
