using System;

namespace Hostess.ViewModels
{
    [Serializable]
    public enum InstallItemType : int
    {
        DownloadAndInstall = 0,
        PowerShellScript,
        OpenWebSite,
        CustomAction,
    }
}
