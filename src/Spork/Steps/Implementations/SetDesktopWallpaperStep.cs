using Spork.Components;
using Spork.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;

namespace Spork.Steps.Implementations
{
    public sealed class SetDesktopWallpaperStep : StepBase<InstallItemViewModel>
    {
        public SetDesktopWallpaperStep(
            ISharedLocations sharedLocations,
            ILogger<SetDesktopWallpaperStep> logger)
        {
            _sharedLocations = sharedLocations;
            _logger = logger;
        }

        private readonly ISharedLocations _sharedLocations;
        private readonly ILogger _logger;

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, CancellationToken cancellationToken = default)
        {
            var picturesDirectoryPath = _sharedLocations.GetPicturesDirectoryPath();

            if (!Directory.Exists(picturesDirectoryPath))
                Directory.CreateDirectory(picturesDirectoryPath);

            var wallpaperPath = Path.Combine(picturesDirectoryPath, "Signature.jpg");
            Properties.Resources.Signature.Save(wallpaperPath, ImageFormat.Jpeg);

            var result = NativeMethods.SystemParametersInfoW(
                NativeMethods.SetDesktopWallpaper, 0, wallpaperPath,
                NativeMethods.UpdateIniFile | NativeMethods.SendWinIniChange);

            if (result != 0)
            {
                _logger.LogWarning("SystemParametersInfoW result: {result}", result);

                var lastWin32Error = Marshal.GetLastWin32Error();
                _logger.LogWarning(
                    "SetDesktopWallpaper failed. SystemParametersInfoW says: {result} and GetLastWin32Error says: {lastWin32Error}",
                    result, lastWin32Error);
            }

            NativeMethods.UpdatePerUserSystemParameters(
                IntPtr.Zero, IntPtr.Zero, "1, True", 0);

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
