using System;
using System.Linq;
using WixSharp;

namespace TableCloth.SetupBuilder
{
    internal static class Program
    {
        private static Project DefineCoreFeature(Project targetProject, string inputDirectory)
        {
            var coreFeature = new Feature("CoreFeature", true)
            {
                AllowChange = false,
                Display = FeatureDisplay.hidden,
            };

            var mainDirectory = new Dir(coreFeature, @"%ProgramFiles%\TableCloth");

            var mainFiles = System.IO.Directory
                .GetFiles(inputDirectory, "*.*", System.IO.SearchOption.AllDirectories)
                .Select(x => new File(coreFeature, System.IO.Path.GetFullPath(x)))
                .ToArray();

            var mainExecFile = mainFiles.Where(x => string.Equals(System.IO.Path.GetFileName(x.Name), "TableCloth.exe", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
            mainExecFile.Shortcuts = new FileShortcut[]
            {
                new FileShortcut("TableCloth", @"%ProgramMenu%") { AttributesDefinition = "Advertise=yes", },
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
            project.GUID = Guid.Parse("1D7A2F0E-550D-452B-B69C-585F87C23A5B");
            project.OutFileName = "TableCloth";

            project = DefineCoreFeature(project, inputDirectory);

            Compiler.BuildMsi(project);
        }
    }
}
