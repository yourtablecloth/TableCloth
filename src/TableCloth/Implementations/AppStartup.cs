using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using TableCloth.Contracts;
using TableCloth.Resources;
using Windows.Storage;

namespace TableCloth.Implementations
{
    public sealed class AppStartup : IAppStartup
    {
        public IEnumerable<string> Arguments { get; set; }

        public string AppDataDirectoryPath
        {
            get
            {
                return Path.Combine(UserDataPaths.GetDefault().LocalAppDataLow, "TableCloth");
                //return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TableCloth");
            }
        }

        public bool HasRequirementsMet(List<string> warnings, out Exception failedResaon, out bool isCritical)
        {
            // https://stackoverflow.com/questions/336633/how-to-detect-windows-64-bit-platform-with-net
            var isWow64 = Environment.OSVersion.Version.Major >= 10
                && NativeMethods.IsWow64Process(Process.GetCurrentProcess().Handle, out var retVal)
                && retVal;
            var is64BitOperatingSystem = (IntPtr.Size == 8) || isWow64;

            if (!is64BitOperatingSystem)
            {
                failedResaon = new PlatformNotSupportedException(StringResources.Error_Windows_OS_Too_Old);
                isCritical = true;
                return false;
            }

            var wsbExecPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsSandbox.exe");

            if (!File.Exists(wsbExecPath))
            {
                failedResaon = new PlatformNotSupportedException(StringResources.Error_Windows_Sandbox_Missing);
                isCritical = true;
                return false;
            }

            new Mutex(true, GetType().FullName, out var isFirstInstance);

            if (!isFirstInstance)
            {
                failedResaon = new ApplicationException(StringResources.Error_Already_TableCloth_Running);
                isCritical = true;
                return false;
            }

            var iePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Internet Explorer", "iexplore.exe");

            if (!File.Exists(iePath))
            {
                warnings.Add(StringResources.Error_IEMode_NotAvailable);
            }

            failedResaon = null;
            isCritical = false;
            return true;
        }

        public bool Initialize(out Exception failedReason, out bool isCritical)
        {
            var targetPath = AppDataDirectoryPath;

            if (!Directory.Exists(targetPath))
            {
                try { Directory.CreateDirectory(targetPath); }
                catch (Exception e)
                {
                    failedReason = new ApplicationException(StringResources.Error_Cannot_Create_AppDataDirectory(e), e);
                    isCritical = true;
                    return false;
                }
            }

            failedReason = null;
            isCritical = false;
            return true;
        }
    }
}
