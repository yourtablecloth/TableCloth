#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.Xml.Linq;

// Configuration
var appId = "TableCloth";
var solutionFile = "TableCloth.slnx";
var mainProject = Path.Combine("src", "TableCloth", "TableCloth.csproj");
// Resources 폴더는 Phase 1.2에서 TableCloth.App으로 이전됨.
var iconPath = Path.Combine("src", "TableCloth.App", "Resources", "SandboxIcon.ico");
var directoryBuildProps = "Directory.Build.Props";
// 프로젝트 수준은 AnyCPU이고 publish 시점에 RID로 결정. 여기서는 RID 접미사로 사용.
var platforms = new[] { "x64", "arm64" };

// Parse command line arguments
var includeDebug = args.Contains("--debug") || args.Contains("-d");
var skipBuild = args.Contains("--skip-build") || args.Contains("-s");
var showHelp = args.Contains("--help") || args.Contains("-h") || args.Contains("-?");

if (showHelp)
{
    Console.WriteLine("""
        TableCloth Build Script (.NET 10)
        
        Usage: dotnet run --file build.cs [options]
        
        Options:
          -d, --debug       Include Debug configuration
          -s, --skip-build  Skip build step (use existing publish output)
          -h, --help        Show this help message
        """);
    return 0;
}

var configurations = includeDebug ? ["Debug", "Release"] : new[] { "Release" };

await RunBuildAsync(configurations, platforms, skipBuild);
return 0;

async Task RunBuildAsync(string[] configs, string[] plats, bool skip)
{
    var scriptDir = GetScriptDirectory();
    Directory.SetCurrentDirectory(scriptDir);

    Console.WriteLine("=======================");
    Console.WriteLine("TableCloth Build Script");
    Console.WriteLine("=======================");
    Console.WriteLine();

    // Install vpk CLI
    _ = await RunCommandAsync("dotnet", "tool install -g vpk");

    // Get Git commit hash
    var gitCommit = await RunCommandAsync("git", "rev-parse --short HEAD");
    Console.WriteLine($"Git Commit: {gitCommit}");

    // Extract version from Directory.Build.props
    var projectVersion = GetProjectVersion(directoryBuildProps);
    Console.WriteLine($"Project Version: {projectVersion}");

    // Convert to SemVer (Major.Minor.Patch)
    var versionParts = projectVersion.Split('.');
    var version = $"{versionParts[0]}.{versionParts[1]}.{versionParts[2]}";
    Console.WriteLine($"Build Version: {version}");
    Console.WriteLine();

    if (!skip)
    {
        // Restore packages
        Console.WriteLine("Restoring packages...");
        await RunCommandAsync("dotnet", $"restore {solutionFile}");
        Console.WriteLine();
    }

    foreach (var platform in plats)
    {
        foreach (var config in configs)
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine($"Building {config} - {platform} [v{version}+{gitCommit}]");
            Console.WriteLine("========================================");

            var publishDir = Path.Combine("publish", config, $"win-{platform}");
            var releasesDir = Path.Combine("Releases", config, platform);

            if (!skip)
            {
                // Build solution (AnyCPU; csproj는 RID/SelfContained를 publish 시점에만 활성화)
                Console.WriteLine();
                Console.WriteLine("Building solution...");
                await RunCommandAsync("dotnet",
                    $"build {solutionFile} -c {config}");

                // Publish TableCloth — TableCloth.csproj의 조건부 PropertyGroup이 RID 명시 시
                // PublishSingleFile=true / PublishReadyToRun=true / EnableCompressionInSingleFile=true 등을
                // 자동 활성화한다. 여기서는 RID와 SelfContained만 명시.
                Console.WriteLine();
                Console.WriteLine("Publishing TableCloth...");
                await RunCommandAsync("dotnet",
                    $"publish {mainProject} -c {config} -r win-{platform} -p:SelfContained=true " +
                    $"-o {publishDir}");
            }

            // Create Velopack package
            Console.WriteLine();
            Console.WriteLine("Creating Velopack package...");
            await RunCommandAsync("vpk",
                $"--yes pack -u {appId} -v {version} -p {publishDir} -e {appId}.exe " +
                $"-o {releasesDir} --packTitle {appId} --packAuthors \"TableCloth Project\" " +
                $"--icon {iconPath}");
        }
    }

    // Open Releases folder
    var releasesPath = Path.Combine(scriptDir, "Releases");
    if (Directory.Exists(releasesPath))
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = releasesPath,
            UseShellExecute = true
        });
    }

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine($"Build completed. Version: {version} (Commit: {gitCommit})");
    Console.WriteLine("========================================");
}

string GetScriptDirectory()
{
    // Get the directory where this script is located
    var scriptPath = Environment.GetCommandLineArgs()
        .FirstOrDefault(arg => arg.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
    
    if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
        return Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? Directory.GetCurrentDirectory();
    
    return Directory.GetCurrentDirectory();
}

string GetProjectVersion(string propsPath)
{
    var doc = XDocument.Load(propsPath);
    var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
    
    // Directory.Build.props에서 개별 버전 컴포넌트 읽기
    var major = doc.Descendants(ns + "TableClothVersionMajor").FirstOrDefault()?.Value ?? "1";
    var minor = doc.Descendants(ns + "TableClothVersionMinor").FirstOrDefault()?.Value ?? "0";
    var patch = doc.Descendants(ns + "TableClothVersionPatch").FirstOrDefault()?.Value ?? "0";
    var revision = doc.Descendants(ns + "TableClothVersionRevision").FirstOrDefault()?.Value ?? "0";
    
    return $"{major}.{minor}.{patch}.{revision}";
}


async Task<string> RunCommandAsync(string command, string arguments)
{
    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };

    var outputBuilder = new System.Text.StringBuilder();

    process.OutputDataReceived += (sender, e) =>
    {
        if (e.Data != null)
        {
            Console.WriteLine(e.Data);
            outputBuilder.AppendLine(e.Data);
        }
    };

    process.ErrorDataReceived += (sender, e) =>
    {
        if (e.Data != null)
        {
            Console.Error.WriteLine(e.Data);
        }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        Console.WriteLine($"Warning: {command} exited with code {process.ExitCode}");
    }

    return outputBuilder.ToString().Trim();
}
