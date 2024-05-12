using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class AppStartup : IAppStartup
{
    public AppStartup(
        ISharedLocations sharedLocations,
        IArchiveExpander archiveExpander,
        ILogger<AppStartup> logger,
        IHttpClientFactory httpClientFactory,
        ISystemProperties systemProperties)
    {
        _sharedLocations = sharedLocations;
        _archiveExpander = archiveExpander;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _systemProperties = systemProperties;

        _mutex = new Mutex(true, $"Global\\{GetType().FullName}", out this._isFirstInstance);
    }

    ~AppStartup() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }

        _disposed = true;
    }

    private bool _disposed;
    private readonly ISharedLocations _sharedLocations;
    private readonly IArchiveExpander _archiveExpander;
    private readonly ILogger _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISystemProperties _systemProperties;

    private readonly Mutex _mutex;
    private readonly bool _isFirstInstance;

    public async Task<bool> CheckForInternetConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUICulture = CultureInfo.InstalledUICulture;
            var testUri = "http://www.gstatic.com/generate_204";

            if (currentUICulture.Name.StartsWith("fa", StringComparison.Ordinal))
                testUri = "http://www.aparat.com";
            else if (currentUICulture.Name.StartsWith("zh", StringComparison.Ordinal))
                testUri = "http://www.baidu.com";

            var client = _httpClientFactory.CreateTableClothHttpClient();
            using var response = await client.GetAsync(new Uri(testUri, UriKind.Absolute), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApplicationStartupResultModel> HasRequirementsMetAsync(IList<string> warnings,
        CancellationToken cancellationToken = default)
    {
        var result = default(ApplicationStartupResultModel);

        if (!File.Exists(_sharedLocations.SporkZipFilePath))
        {
            result = ApplicationStartupResultModel.FromErrorMessage(
                ErrorStrings.Error_Spork_Missing, isCritical: true, providedWarnings: warnings);
            return result;
        }

        if (!File.Exists(_sharedLocations.SporkZipFilePath))
        {
            result = ApplicationStartupResultModel.FromErrorMessage(
                ErrorStrings.Error_Sponge_Missing, isCritical: true, providedWarnings: warnings);
            return result;
        }

        if (!File.Exists(_sharedLocations.ImagesZipFilePath))
        {
            result = ApplicationStartupResultModel.FromErrorMessage(
                ErrorStrings.Error_Images_Missing, isCritical: true, providedWarnings: warnings);
            return result;
        }

        // https://stackoverflow.com/questions/336633/how-to-detect-windows-64-bit-platform-with-net
        var isWow64 = Environment.OSVersion.Version.Major >= 10
            && NativeMethods.IsWow64Process(Process.GetCurrentProcess().Handle, out var retVal)
            && retVal;
        var is64BitOperatingSystem = (IntPtr.Size == 8) || isWow64;

        if (!is64BitOperatingSystem)
        {
            result = ApplicationStartupResultModel.FromErrorMessage(
                ErrorStrings.Error_Windows_OS_Too_Old, isCritical: true, providedWarnings: warnings);
            return result;
        }

        using (var queryResult = new ManagementObjectSearcher("select HyperVisorPresent from Win32_ComputerSystem"))
        using (var objCollection = queryResult.Get())
        {
            var hyperVisorPresent = objCollection.Cast<ManagementBaseObject?>().FirstOrDefault()?.GetPropertyValue("HyperVisorPresent") as bool?;

            if (!hyperVisorPresent.HasValue || !hyperVisorPresent.Value)
            {
                if (Helpers.IsDevelopmentBuild)
                    warnings.Add(ErrorStrings.Error_HyperVisor_Missing);
                else
                {
                    result = ApplicationStartupResultModel.FromErrorMessage(
                        ErrorStrings.Error_HyperVisor_Missing, isCritical: true, providedWarnings: warnings);
                    return result;
                }
            }
        }

        var wsbExecPath = Helpers.GetDefaultWindowsSandboxPath();

        // 1st Try: WindowsSandbox.exe 파일이 없을 경우 dism.exe로 설치 시도
        if (!Helpers.IsDevelopmentBuild && !File.Exists(wsbExecPath))
        {
            var dismExecPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "dism.exe");

            if (!File.Exists(dismExecPath))
            {
                if (Helpers.IsDevelopmentBuild)
                    warnings.Add(ErrorStrings.Error_Windows_Dism_Missing);
                else
                {
                    result = ApplicationStartupResultModel.FromErrorMessage(
                        ErrorStrings.Error_Windows_Dism_Missing, isCritical: true, providedWarnings: warnings);
                    return result;
                }
            }
            else
            {
                var args = new[] {
                    "/Online",
                    "/Enable-Feature",
                    "/FeatureName:Containers-DisposableClientVM",
                    "/All",
                };

                var psi = new ProcessStartInfo(dismExecPath, string.Join(' ', args))
                {
                    UseShellExecute = true,
                    Verb = "runas",
                };

                var process = Process.Start(psi);
                process?.WaitForExit();

                result = ApplicationStartupResultModel.FromErrorMessage(
                    ErrorStrings.Error_Restart_And_RunAgain, isCritical: true, providedWarnings: warnings);
                return result;
            }
        }

        if (!File.Exists(wsbExecPath))
        {
            if (Helpers.IsDevelopmentBuild)
                warnings.Add(ErrorStrings.Error_Windows_Sandbox_Missing);
            else
            {
                result = ApplicationStartupResultModel.FromErrorMessage(
                    ErrorStrings.Error_Windows_Sandbox_Missing, isCritical: true, providedWarnings: warnings);
                return result;
            }
        }

        if (!this._isFirstInstance)
        {
            result = ApplicationStartupResultModel.FromErrorMessage(
                ErrorStrings.Error_Already_TableCloth_Running, isCritical: true, providedWarnings: warnings);
            return result;
        }

        var iePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Internet Explorer", "iexplore.exe");

        if (!File.Exists(iePath))
        {
            warnings.Add(ErrorStrings.Error_IEMode_NotAvailable);
        }

        var osvi = new NativeMethods.OSVERSIONINFOEXW()
        {
            dwOSVersionInfoSize = Marshal.SizeOf(typeof(NativeMethods.OSVERSIONINFOEXW)),
        };

        var supportedOSEditions = new NativeMethods.OSEdition[]
        {
            NativeMethods.OSEdition.PRODUCT_EDUCATION,
            NativeMethods.OSEdition.PRODUCT_EDUCATION_N,
            NativeMethods.OSEdition.PRODUCT_ENTERPRISE,
            NativeMethods.OSEdition.PRODUCT_ENTERPRISE_E,
            NativeMethods.OSEdition.PRODUCT_ENTERPRISE_EVALUATION,
            NativeMethods.OSEdition.PRODUCT_ENTERPRISE_N,
            NativeMethods.OSEdition.PRODUCT_ENTERPRISE_N_EVALUATION,
            NativeMethods.OSEdition.PRODUCT_PRO_WORKSTATION,
            NativeMethods.OSEdition.PRODUCT_PRO_WORKSTATION_N,
            NativeMethods.OSEdition.PRODUCT_PROFESSIONAL,
            NativeMethods.OSEdition.PRODUCT_PROFESSIONAL_N,
        };

        if (!NativeMethods.GetVersionExW(ref osvi))
            warnings.Add(StringResources.Error_Cannot_Invoke_GetVersionEx(Marshal.GetLastWin32Error()));
        else
        {
            if (!NativeMethods.GetProductInfo(
                osvi.dwMajorVersion, osvi.dwMinorVersion,
                osvi.wServicePackMajor, osvi.wServicePackMinor,
                out NativeMethods.OSEdition productType))
                warnings.Add(ErrorStrings.Error_Cannot_Invoke_GetProductInfo);
            else
            {
                if (Array.IndexOf(supportedOSEditions, productType) < 0)
                    warnings.Add(ErrorStrings.Error_SandboxMightNotAvailable);
            }
        }

        var bitLockerStatus = _systemProperties.IsSystemPartitionBitLockerEnabled();
 
        if (!bitLockerStatus.HasValue || !bitLockerStatus.Value)
            warnings.Add(ErrorStrings.Error_SystemDrive_Vulnerable);

        result = ApplicationStartupResultModel.FromSucceedResult(providedWarnings: warnings);
        return await Task.FromResult(result).ConfigureAwait(false);
    }

    public async Task<ApplicationStartupResultModel> InitializeAsync(
        IList<string> warnings,
        CancellationToken cancellationToken = default)
    {
        var result = default(ApplicationStartupResultModel);
        var targetPath = _sharedLocations.AppDataDirectoryPath;

        if (!Directory.Exists(targetPath))
        {
            try { Directory.CreateDirectory(targetPath); }
            catch (Exception e)
            {
                result = ApplicationStartupResultModel.FromErrorMessage(
                    StringResources.Error_With_Exception(ErrorStrings.Error_Cannot_Create_AppDataDirectory, e), e, isCritical: true);
                return result;
            }
        }

        var imageDirectoryPath = _sharedLocations.GetImageDirectoryPath();

        if (!Directory.Exists(imageDirectoryPath))
        {
            try { Directory.CreateDirectory(imageDirectoryPath); }
            catch (Exception e)
            {
                result = ApplicationStartupResultModel.FromErrorMessage(
                    StringResources.Error_With_Exception(ErrorStrings.Error_Cannot_Create_AppDataDirectory, e), e, isCritical: true);
                return result;
            }
        }

        try
        {
            await _archiveExpander.ExpandArchiveAsync(
                _sharedLocations.ImagesZipFilePath, imageDirectoryPath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Cannot write image file to directory {destPath}.", imageDirectoryPath);
            result = ApplicationStartupResultModel.FromErrorMessage(
                StringResources.Error_With_Exception(ErrorStrings.Error_Cannot_Prepare_AppContents, e), e, isCritical: true);
            return result;
        }

        result = ApplicationStartupResultModel.FromSucceedResult(providedWarnings: warnings);
        return await Task.FromResult(result).ConfigureAwait(false);
    }
}
