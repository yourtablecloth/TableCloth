using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TableCloth.Components
{
    public sealed class SandboxCleanupManager
    {
        public SandboxCleanupManager(
            SandboxLauncher sandboxLauncher)
        {
            _sandboxLauncher = sandboxLauncher;
        }

        private readonly SandboxLauncher _sandboxLauncher;

        private readonly List<string> _temporaryDirectories = new List<string>();

        public string? CurrentDirectory { get; private set; }

        public void SetWorkingDirectory(string workingDirectory)
        {
            var normalizedPath = Path.GetFullPath(workingDirectory);

            if (!Directory.Exists(normalizedPath))
                throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}");

            CurrentDirectory = normalizedPath;

            if (!_temporaryDirectories.Contains(CurrentDirectory))
                _temporaryDirectories.Add(CurrentDirectory);
        }

        private void OpenExplorer(string targetDirectoryPath)
        {
            if (!Directory.Exists(targetDirectoryPath))
                return;

            var psi = new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                targetDirectoryPath)
            {
                UseShellExecute = false,
            };

            Process.Start(psi);
        }

        public void TryCleanup()
        {
            foreach (var eachDirectory in _temporaryDirectories)
            {
                if (!string.IsNullOrWhiteSpace(CurrentDirectory))
                {
                    if (string.Equals(Path.GetFullPath(eachDirectory), Path.GetFullPath(CurrentDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        if (_sandboxLauncher.IsSandboxRunning())
                        {
                            OpenExplorer(eachDirectory);
                            continue;
                        }
                    }
                }

                if (!Directory.Exists(eachDirectory))
                    continue;

                try { Directory.Delete(eachDirectory, true); }
                catch { OpenExplorer(eachDirectory); }
            }
        }
    }
}
