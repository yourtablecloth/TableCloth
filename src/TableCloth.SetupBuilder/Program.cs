using System;
using System.Linq;
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
            var inputDirectory = args.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(inputDirectory) ||
                !System.IO.Directory.Exists(inputDirectory))
            {
                Console.Error.WriteLine("Please specify input directory to create setup package.");
                Environment.Exit(1);
                return;
            }

            var project = new Project("TableCloth");
            project.Package.AttributesDefinition = "Platform=x64";
            project.GUID = Guid.Parse("1D7A2F0E-550D-452B-B69C-585F87C23A5B");
            project.UpgradeCode = Guid.Parse("C63DF133-51A9-4139-BD31-EDC025C7EB51");
            project.Encoding = System.Text.Encoding.UTF8;
            project.OutFileName = "TableCloth";
            project.LicenceFile = "License.rtf";
            project.Language = "ko-KR";
            project.UI = WUI.WixUI_Minimal;
            project.InstallScope = InstallScope.perUser;
            project.Name = "식탁보";

            project = DefineCoreFeature(project, inputDirectory);

            Compiler.BuildMsi(project);
        }
    }
}
