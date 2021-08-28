using System;
using System.Linq;
using TableCloth.Resources;
using WixSharp;

namespace TableCloth.SetupBuilder
{
    internal static class Program
    {
        private static Project DefineCoreFeature(Project targetProject, string inputDirectory)
        {
            var mainDirectory = new Dir(@"%LocalAppDataFolder%\Program\TableCloth");

            var mainFiles = System.IO.Directory
                .GetFiles(inputDirectory, "*.*", System.IO.SearchOption.AllDirectories)
                .Select(x => new File(System.IO.Path.GetFullPath(x)))
                .ToArray();

            var mainExecFile = mainFiles.Where(x => string.Equals(System.IO.Path.GetFileName(x.Name), "TableCloth.exe", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
            mainExecFile.Shortcuts = new FileShortcut[]
            {
                new FileShortcut("식탁보", @"%ProgramMenu%") { },
            };

            mainDirectory.Files = mainFiles;

            targetProject.Dirs = new Dir[]
            {
                mainDirectory,
            };

            return targetProject;
        }

        private static void Main(string[] args)
        {
            var inputDirectory = args.ElementAtOrDefault(0);
            var pfxFilePath = args.ElementAtOrDefault(1);
            var pfxPassword = args.ElementAtOrDefault(2);
            var licenseRtfFilePath = args.ElementAtOrDefault(3);

            if (string.IsNullOrWhiteSpace(inputDirectory) ||
                !System.IO.Directory.Exists(inputDirectory))
            {
                Console.Error.WriteLine("Please specify input directory to create setup package.");
                Environment.Exit(1);
                return;
            }

            var project = new Project("TableCloth");
            project.Package.AttributesDefinition = "Platform=x64";
            project.UpgradeCode = Guid.Parse("C63DF133-51A9-4139-BD31-EDC025C7EB51");
            project.Encoding = System.Text.Encoding.UTF8;
            project.OutFileName = "TableCloth";

            if (!string.IsNullOrWhiteSpace(licenseRtfFilePath) &&
                System.IO.File.Exists(licenseRtfFilePath))
            {
                project.LicenceFile = licenseRtfFilePath;
            }

            project.Language = "ko-KR";
            project.UI = WUI.WixUI_Minimal;
            project.InstallScope = InstallScope.perUser;
            project.Name = StringResources.AppName;

            if (!string.IsNullOrWhiteSpace(pfxFilePath) &&
                System.IO.File.Exists(pfxFilePath))
            {
                project.DigitalSignature = new DigitalSignature()
                {
                    PfxFilePath = pfxFilePath,
                    Password = pfxPassword,
                    MaxTimeUrlRetry = 3,
                    TimeUrl = new Uri("http://timestamp.digicert.com", UriKind.Absolute),
                    UrlRetrySleep = 1,
                    Description = StringResources.AppCopyright,
                    HashAlgorithm = HashAlgorithmType.sha256,
                };
            }

            project = DefineCoreFeature(project, inputDirectory);

            Compiler.BuildMsi(project);
        }
    }
}
