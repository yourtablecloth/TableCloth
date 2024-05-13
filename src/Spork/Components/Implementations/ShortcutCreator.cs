using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Components.Implementations
{
    public sealed class ShortcutCreator : IShortcutCreator
    {
        public async Task<string> CreateShortcutOnDesktopAsync(string destinationPath, string linkName,
            string arguments = default, string iconFilePath = default, string description = default,
            CancellationToken cancellationToken = default)
        {
            var shortcutDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var shortcutFileName = linkName + ".lnk";

            var initialData = new byte[] { };
            var shortcutFilePath = Path.Combine(shortcutDirectoryPath, shortcutFileName);
            using (var fileStream = File.Open(shortcutFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await fileStream.WriteAsync(initialData, 0, initialData.Length, cancellationToken).ConfigureAwait(false);
            }

            // https://stackoverflow.com/questions/13542005/create-shortcut-with-unicode-character
            // WshRuntimeLibrary의 경우 유니코드 문자열로 바로 가기 아이콘을 만들지 못하는 버그가 있음.
            var shellType = Type.GetTypeFromProgID("Shell.Application") ?? throw new Exception("Cannot obtain Shell.Application type.");
            object oInstance = Activator.CreateInstance(shellType) ?? throw new Exception("Cannot create instance of Shell.Application.");

            dynamic shell = oInstance;
            dynamic folder = shell.NameSpace(shortcutDirectoryPath);
            dynamic folderItem = folder.Items().Item(shortcutFileName);
            dynamic shortcut = folderItem.GetLink;

            shortcut.Path = destinationPath;

            if (iconFilePath != null && File.Exists(iconFilePath))
                shortcut.SetIconLocation(iconFilePath, 0);

            shortcut.Arguments = arguments ?? string.Empty;
            shortcut.Description = description ?? $"{linkName}";
            shortcut.WorkingDirectory = Path.GetDirectoryName(destinationPath);
            shortcut.Save();

            return shortcutDirectoryPath;
        }
    }
}
