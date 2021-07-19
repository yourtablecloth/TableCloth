using System;
using System.IO;
using System.Linq;
using System.Text;
using TableCloth.Models;

namespace TableCloth
{
    public static class SandboxBuilder
    {
        public static string GenerateSandboxConfiguration(string outputDirectory, SandboxConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var assetsDirectory = Path.Combine(outputDirectory, "assets");
            if (!Directory.Exists(assetsDirectory))
                Directory.CreateDirectory(assetsDirectory);

            var batchFileContent = GenerateSandboxStartupScript(config);
            var batchFilePath = Path.Combine(assetsDirectory, "StartupScript.cmd");
            File.WriteAllText(batchFilePath, batchFileContent, Encoding.Default);

            config.AssetsDirectoryPath = assetsDirectory;

            var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
            File.WriteAllText(wsbFilePath, config.SerializeToXml());

            return wsbFilePath;
        }

        public static string GenerateSandboxStartupScript(SandboxConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var buffer = new StringBuilder();
            var services = config.SelectedServices;
            var packageTotalCount = services.Sum(x => x.Packages.Count());
            var siteNameList = string.Join(", ", services.Select(x => x.SiteName));

            var infoMessage = $"지금부터 {packageTotalCount}개 프로그램의 설치 과정이 시작됩니다. 모든 프로그램의 설치가 끝나면 자동으로 {siteNameList} 홈페이지가 열립니다.";
            var value = $@"PowerShell -Command ""Add-Type -AssemblyName System.Windows.Forms;[System.Windows.Forms.MessageBox]::Show('{infoMessage}', '안내', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)""";
            buffer.AppendLine(value);

            foreach (var eachPackage in services.SelectMany(service => service.Packages))
            {
                var localFileName = GetLocalFileName(eachPackage.PackageDownloadUrl.LocalPath);

                buffer
                    .AppendLine($@"REM Run {eachPackage.Name} Setup")
                    .AppendLine($@"curl -L ""{eachPackage.PackageDownloadUrl}"" --output ""%temp%\{localFileName}""")
                    .AppendLine(!string.IsNullOrWhiteSpace(eachPackage.Arguments)
                        ? $@"start /abovenormal /wait %temp%\{localFileName} {eachPackage.Arguments}"
                        : $@"start /abovenormal /wait %temp%\{localFileName}")
                    .AppendLine();
            }

            foreach (var eachHomePageUrl in services.Select(x => x.HomepageUrl.AbsoluteUri))
                buffer.AppendLine($@"start /max {eachHomePageUrl}");

            return buffer.ToString();

            static string GetLocalFileName(string localPath)
            {
                try { return Path.GetFileName(localPath); }
                catch { return $"{Guid.NewGuid():N}.exe"; }
            }
        }
    }
}
