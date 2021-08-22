<Query Kind="Program">
  <IncludeUncapsulator>false</IncludeUncapsulator>
</Query>

public static class Program
{
	[STAThread]
	static void Main(string[] args)
	{
		if (!Version.TryParse(args?.FirstOrDefault(), out Version targetVersion))
		{
			Console.Error.WriteLine("First argument should be a semantic-version string.");
			Environment.Exit(1);
			return;
		}

		var currentDirectory = Path.GetDirectoryName(Util.CurrentQueryPath);

		// app.manifest
		{
			var manifestFiles = Directory.GetFiles(currentDirectory, "app.manifest", SearchOption.AllDirectories);

			foreach (var eachManifestFile in manifestFiles)
			{
				try
				{
					var doc = new XmlDocument();
					doc.Load(eachManifestFile);

					var nsmgr = new XmlNamespaceManager(doc.NameTable);
					nsmgr.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1");

					var version = doc.SelectSingleNode("/asm:assembly/asm:assemblyIdentity/@version", nsmgr);
					if (!string.Equals(targetVersion.ToString(), version.Value, StringComparison.Ordinal))
						version.Value = targetVersion.ToString();

					doc.Save(eachManifestFile);
				}
				catch (Exception ex)
				{
					ex.Dump();
				}
			}
		}

		// Package.appxmanifest
		{
			var appxManifestFiles = Directory.GetFiles(currentDirectory, "Package.appxmanifest", SearchOption.AllDirectories);

			foreach (var eachAppxManifestFile in appxManifestFiles)
			{
				try
				{
					var doc = new XmlDocument();
					doc.Load(eachAppxManifestFile);

					var nsmgr = new XmlNamespaceManager(doc.NameTable);
					nsmgr.AddNamespace("asm", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");

					var version = doc.SelectSingleNode("/asm:Package/asm:Identity/@Version", nsmgr);
					if (!string.Equals(targetVersion.ToString(), version.Value, StringComparison.Ordinal))
						version.Value = targetVersion.ToString();

					doc.Save(eachAppxManifestFile);
				}
				catch (Exception ex)
				{
					ex.Dump();
				}
			}
		}

		// *.csproj (SDK Style Only)
		{
			var csprojFiles = Directory.GetFiles(currentDirectory, "*.csproj", SearchOption.AllDirectories);

			foreach (var eachCsprojFile in csprojFiles)
			{
				try
				{
					var doc = new XmlDocument();
					doc.Load(eachCsprojFile);

					var version = doc.SelectSingleNode("/Project/PropertyGroup/Version");
					if (version == null)
						continue;
					if (!string.Equals(targetVersion.ToString(), version.Value, StringComparison.Ordinal))
						version.InnerText = targetVersion.ToString();

					var fileVersion = doc.SelectSingleNode("/Project/PropertyGroup/FileVersion");
					if (fileVersion == null)
						continue;
					if (!string.Equals(targetVersion.ToString(), fileVersion.Value, StringComparison.Ordinal))
						fileVersion.InnerText = targetVersion.ToString();

					doc.Save(eachCsprojFile);
				}
				catch (Exception ex)
				{
					ex.Dump();
				}
			}
		}

		// AssemblyInfo.cs
		{
			var asmInfoFiles = Directory.GetFiles(currentDirectory, "AssemblyInfo.cs", SearchOption.AllDirectories);

			foreach (var eachAsmInfoFile in asmInfoFiles)
			{
				try
				{
					var content = File.ReadAllText(eachAsmInfoFile, new UTF8Encoding(false));
					var asmVerRegex = new Regex(@"\[assembly: AssemblyVersion\(\""(?<VersionString>[^""]+)\""\)\]", RegexOptions.Compiled | RegexOptions.Multiline);
					var asmFileVerRegex = new Regex(@"\[assembly: AssemblyFileVersion\(\""(?<VersionString>[^""]+)\""\)\]", RegexOptions.Compiled | RegexOptions.Multiline);

					content = asmVerRegex.Replace(content, new MatchEvaluator(m =>
					{
						var group = m.Groups["VersionString"];
						var relativeIndex = group.Index - m.Index;
						var leftPart = m.Value.Substring(0, relativeIndex);
						var rightPart = m.Value.Substring(relativeIndex + group.Length);
						return $"{leftPart}{targetVersion}{rightPart}";
					}));

					content = asmFileVerRegex.Replace(content, new MatchEvaluator(m =>
					{
						var group = m.Groups["VersionString"];
						var relativeIndex = group.Index - m.Index;
						var leftPart = m.Value.Substring(0, relativeIndex);
						var rightPart = m.Value.Substring(relativeIndex + group.Length);
						return $"{leftPart}{targetVersion}{rightPart}";
					}));

					File.WriteAllText(eachAsmInfoFile, content, new UTF8Encoding(false));
				}
				catch (Exception ex)
				{
					ex.Dump();
				}
			}
		}
	}
}
