using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using TableCloth.Resources;
using WixSharp;

using WixSharpFile = WixSharp.File;

namespace TableCloth.SetupBuilder
{
    internal static class Program
    {
        private static ManagedProject DefineCoreFeature(ManagedProject targetProject, string inputDirectory)
        {
            FileShortcut mainShortcut;
            Files filesCollection;
            WixSharpFile mainExecutableFile;
            Dir tableClothDirectory;

            InstallDir mainDirectory = new InstallDir(@"%LocalAppDataFolder%\Programs",
            (
                tableClothDirectory = new Dir("TableCloth",
                (
                    filesCollection = new Files($@"{inputDirectory}\*.*", new Predicate<string>(x => !x.EndsWith("TableCloth.exe", StringComparison.OrdinalIgnoreCase)))
                ),
                (
                    mainExecutableFile = new WixSharpFile($@"{inputDirectory}\TableCloth.exe",
                    (
                        mainShortcut = new FileShortcut(StringResources.AppName, @"%ProgramMenu%")
                    ))
                ))
            ));

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

            var ignorePfx =
                string.Equals(StringResources.TableCloth_Switch_IgnoreSwitch, pfxFilePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(StringResources.TableCloth_Switch_IgnoreSwitch, pfxPassword, StringComparison.OrdinalIgnoreCase);

            var iconFilePath = args.ElementAtOrDefault(3);

            if (string.IsNullOrWhiteSpace(inputDirectory) ||
                !Directory.Exists(inputDirectory))
            {
                Console.Error.WriteLine("Please specify input directory to create setup package.");
                Environment.Exit(1);
                return;
            }

            var project = new ManagedProject("TableCloth");

            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.Arm64:
                    project.Package.AttributesDefinition = "Platform=arm64";
                    break;

                case Architecture.Arm:
                    project.Package.AttributesDefinition = "Platform=arm";
                    break;

                case Architecture.X64:
                    project.Package.AttributesDefinition = "Platform=x64";
                    break;

                case Architecture.X86:
                    project.Package.AttributesDefinition = "Platform=x86";
                    break;

                default:
                    break;
            }

            project.Encoding = System.Text.Encoding.UTF8;
            project.OutFileName = "TableCloth";
            project.LicenceFile = "License.rtf";
            project.SourceBaseDir = Path.GetDirectoryName(new Uri(typeof(Program).Assembly.Location, UriKind.Absolute).LocalPath);

            project.Language = "ko-KR";
            project.UI = WUI.WixUI_Minimal;
            project.InstallScope = InstallScope.perUser;
#if DEBUG
            project.Name = $"{StringResources.AppName} (Debug Version)";
            project.GUID = Guid.Parse("21AD9E55-CC53-4B64-9770-BFA3432DD7D3");
            project.UpgradeCode = Guid.Parse("93316BF0-E965-49AD-A6A1-049AFD551459");
#else
            project.Name = StringResources.AppNameForWixAndStore;
            project.GUID = Guid.Parse("1D7A2F0E-550D-452B-B69C-585F87C23A5B");
            project.UpgradeCode = Guid.Parse("C63DF133-51A9-4139-BD31-EDC025C7EB51");
#endif // DEBUG
            project.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            if (!string.IsNullOrWhiteSpace(iconFilePath) &&
                System.IO.File.Exists(iconFilePath))
            {
                project.ControlPanelInfo.ProductIcon = iconFilePath;
            }

            project.ControlPanelInfo.UrlInfoAbout = StringResources.AppInfoUrl;
            project.ControlPanelInfo.UrlUpdateInfo = StringResources.AppUpdateInfoUrl;
            project.ControlPanelInfo.HelpLink = StringResources.AppInfoUrl;
            project.ControlPanelInfo.Comments = StringResources.AppCommentTextForWixAndStore;
            project.ControlPanelInfo.Manufacturer = StringResources.AppPublisherForWixAndStore;
            project.ControlPanelInfo.Contact = StringResources.AppContactForWixAndStore;
            project.ControlPanelInfo.NoModify = true;

            if (!ignorePfx)
            {
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
            }

            project = DefineCoreFeature(project, inputDirectory);

            Compiler.BuildMsi(project);
        }
    }
}
