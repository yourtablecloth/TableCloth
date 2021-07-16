using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using TableCloth.Models;

namespace TableCloth
{
    static class SandboxBuilder
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

			var sandboxWallpaperContent = Convert.FromBase64String(GraphicResources.SandboxWallpaperImage);
			var sandboxWallpaperPath = Path.Combine(assetsDirectory, "WallpaperImage.jpg");
			File.WriteAllBytes(sandboxWallpaperPath, sandboxWallpaperContent);

			var batchFileContent = GenerateSandboxStartupScript(config);
			var batchFilePath = Path.Combine(assetsDirectory, "StartupScript.cmd");
			File.WriteAllText(batchFilePath, batchFileContent, Encoding.Default);

			var wsbFileContent = GenerateSandboxSpecDocument(config, assetsDirectory);
			var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
			wsbFileContent.Save(wsbFilePath);

			return wsbFilePath;
		}

		public static string GenerateSandboxStartupScript(SandboxConfiguration config)
		{
			if (config == null)
				throw new ArgumentNullException(nameof(config));

			var buffer = new StringBuilder();
			var service = config.SelectedService;

			_ = buffer.AppendLine($@"%windir%\system32\reg.exe add ""HKCU\control panel\desktop"" /v wallpaper /t REG_SZ /d """" /f");
			_ = buffer.AppendLine($@"%windir%\system32\reg.exe add ""HKCU\control panel\desktop"" /v wallpaper /t REG_SZ /d ""C:\assets\WallpaperImage.jpg"" /f ");
			_ = buffer.AppendLine($@"%windir%\system32\reg.exe delete ""HKCU\Software\Microsoft\Internet Explorer\Desktop\General"" /v WallpaperStyle /f");
			_ = buffer.AppendLine($@"%windir%\system32\reg.exe add ""HKCU\control panel\desktop"" /v WallpaperStyle /t REG_SZ /d 2 /f");
			_ = buffer.AppendLine($@"%windir%\system32\rundll32.exe %windir%\system32\user32.dll,UpdatePerUserSystemParameters");

			var infoMessage = $"지금부터 {service.Packages.Count()}개 프로그램의 설치 과정이 시작됩니다. 모든 프로그램의 설치가 끝나면 자동으로 {service.SiteName} 홈페이지가 열립니다.";
			string value = $@"PowerShell -Command ""Add-Type -AssemblyName System.Windows.Forms;[System.Windows.Forms.MessageBox]::Show('{infoMessage}', '안내', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)""";
			_ = buffer.AppendLine(value);

			if (service != null)
            {
				foreach (var eachPackage in service.Packages)
                {
					string localFileName;
					try { localFileName = Path.GetFileName(eachPackage.PackageDownloadUrl.LocalPath); }
					catch { localFileName = Guid.NewGuid().ToString("n") + ".exe"; }

                    _ = buffer.AppendLine($@"REM Run {eachPackage.Name} Setup");
                    _ = buffer.AppendLine($@"curl -L ""{eachPackage.PackageDownloadUrl}"" --output ""%temp%\{localFileName}""");

					if (!string.IsNullOrWhiteSpace(eachPackage.Arguments))
						_ = buffer.AppendLine($@"start /abovenormal /wait %temp%\{localFileName} {eachPackage.Arguments}");
					else
						_ = buffer.AppendLine($@"start /abovenormal /wait %temp%\{localFileName}");

					_ = buffer.AppendLine();
				}

				_ = buffer.AppendLine($@"start {service.HomepageUrl}");
			}

			return buffer.ToString();
		}

		public static XmlDocument GenerateSandboxSpecDocument(SandboxConfiguration config, string assetsDirectoryPath)
		{
			if (config == null)
				throw new ArgumentNullException(nameof(config));

			var doc = new XmlDocument();
			doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
			var configurationElem = doc.CreateElement("Configuration");
			{
				var audioInputElem = doc.CreateElement("AudioInput");
				if (config.EnableMicrophone)
					audioInputElem.InnerText = "Enable";
				else
					audioInputElem.InnerText = "Disable";
				configurationElem.AppendChild(audioInputElem);
				
				var videoInputElem = doc.CreateElement("VideoInput");
				if (config.EnablePrinters)
					videoInputElem.InnerText = "Enable";
				else
					videoInputElem.InnerText = "Disable";
				configurationElem.AppendChild(videoInputElem);

				var printerRedirectionElem = doc.CreateElement("PrinterRedirection");
				if (config.EnablePrinters)
					printerRedirectionElem.InnerText = "Enable";
				else
					printerRedirectionElem.InnerText = "Disable";
				configurationElem.AppendChild(printerRedirectionElem);

				var mappedFoldersElem = doc.CreateElement("MappedFolders");
				{
					var mappedFolderElem = doc.CreateElement("MappedFolder");
					{
						var hostFolderElem = doc.CreateElement("HostFolder");
						hostFolderElem.InnerText = assetsDirectoryPath;
						mappedFolderElem.AppendChild(hostFolderElem);

						var sandboxFolderElem = doc.CreateElement("SandboxFolder");
						sandboxFolderElem.InnerText = @"C:\assets";
						mappedFolderElem.AppendChild(sandboxFolderElem);

						var readOnlyElem = doc.CreateElement("ReadOnly");
						readOnlyElem.InnerText = Boolean.TrueString.ToString();
						mappedFolderElem.AppendChild(readOnlyElem);
					}
					mappedFoldersElem.AppendChild(mappedFolderElem);

					if (config.CertPair != null)
					{
						var mappedNpkiFolderElem = doc.CreateElement("MappedFolder");
						{
							var certAssetsDirectoryPath = Path.Combine(assetsDirectoryPath, "certs");
							if (!Directory.Exists(certAssetsDirectoryPath))
								Directory.CreateDirectory(certAssetsDirectoryPath);

							var destDerFilePath = Path.Combine(
								certAssetsDirectoryPath,
								Path.GetFileName(config.CertPair.DerFilePath));
							File.Copy(config.CertPair.DerFilePath, destDerFilePath, true);

							var destKeyFileName = Path.Combine(
								certAssetsDirectoryPath,
								Path.GetFileName(config.CertPair.KeyFilePath));
							File.Copy(config.CertPair.KeyFilePath, destKeyFileName, true);

							var hostFolderElem = doc.CreateElement("HostFolder");
							hostFolderElem.InnerText = certAssetsDirectoryPath;
							mappedNpkiFolderElem.AppendChild(hostFolderElem);

							var candidatePath = Path.Join("AppData", "LocalLow", "NPKI", config.CertPair.SubjectOrganization);
							if (config.CertPair.IsPersonalCert)
								candidatePath = Path.Join(candidatePath, "USER", config.CertPair.SubjectNameForNpkiApp);
							candidatePath = Path.Join(@"C:\Users\WDAGUtilityAccount", candidatePath);

							var sandboxFolderElem = doc.CreateElement("SandboxFolder");
							sandboxFolderElem.InnerText = candidatePath;

							mappedNpkiFolderElem.AppendChild(sandboxFolderElem);

							var readOnlyElem = doc.CreateElement("ReadOnly");
							readOnlyElem.InnerText = Boolean.FalseString.ToString();
							mappedNpkiFolderElem.AppendChild(readOnlyElem);
						}
						mappedFoldersElem.AppendChild(mappedNpkiFolderElem);
					}
				}
				configurationElem.AppendChild(mappedFoldersElem);

				var logonCommandElem = doc.CreateElement("LogonCommand");
				{
					var commandElem = doc.CreateElement("Command");
					commandElem.InnerText = @"C:\assets\StartupScript.cmd";
					logonCommandElem.AppendChild(commandElem);
				}
				configurationElem.AppendChild(logonCommandElem);
			}
			doc.AppendChild(configurationElem);

			return doc;
		}
	}
}
