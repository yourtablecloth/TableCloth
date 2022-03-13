using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Opener
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var urlList = new List<string>();

            foreach (var eachArgument in args)
            {
                if (!Uri.TryCreate(eachArgument, UriKind.Absolute, out Uri eachUri))
                    continue;

                urlList.Add(eachUri.AbsoluteUri);
            }

            if (urlList.Count < 1)
                urlList.Add("about:home");

            foreach (var eachUrl in urlList)
            {
                try
                {
                    // Internet Explorer COM 인스턴스 생성을 시도합니다.
                    var targetType = Type.GetTypeFromProgID("InternetExplorer.Application");
                    dynamic iexploreInstance = Activator.CreateInstance(targetType);
                    iexploreInstance.Visible = true;
                    iexploreInstance.AddressBar = true;
                    iexploreInstance.Resizable = true;
                    iexploreInstance.StatusBar = true;
                    iexploreInstance.ToolBar = 1;
                    iexploreInstance.TheaterMode = false;
                    iexploreInstance.FullScreen = false;
                    iexploreInstance.Navigate(eachUrl);
                }
                catch
                {
                    // 실패할 경우 ShellExecute로 Fallback 처리합니다.
                    var psi = new ProcessStartInfo(eachUrl);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
            }
        }
    }
}
