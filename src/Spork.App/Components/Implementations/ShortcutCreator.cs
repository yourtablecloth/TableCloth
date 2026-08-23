using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Interop;

namespace Spork.Components.Implementations
{
    public sealed class ShortcutCreator : IShortcutCreator
    {
        public Task<string> CreateShortcutOnDesktopAsync(string destinationPath, string linkName,
            string arguments = default, string iconFilePath = default, string description = default,
            CancellationToken cancellationToken = default)
        {
            var shortcutDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var shortcutFilePath = Path.Combine(shortcutDirectoryPath, linkName + ".lnk");

            // 기존 Shell.Application late-bound COM + dynamic (Native AOT 비호환)을 AOT 안전한
            // IShellLinkW/IPersistFile(GeneratedComInterface)로 대체. 유니코드 경로/아이콘도 정상 지원.
            NativeShellLink.Create(
                linkFilePath: shortcutFilePath,
                targetPath: destinationPath,
                arguments: arguments ?? string.Empty,
                workingDirectory: Path.GetDirectoryName(destinationPath),
                description: description ?? linkName,
                iconPath: (iconFilePath != null && File.Exists(iconFilePath)) ? iconFilePath : null,
                iconIndex: 0);

            return Task.FromResult(shortcutDirectoryPath);
        }
    }
}
