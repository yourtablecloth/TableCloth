using Serilog;
using Serilog.Formatting.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations.WinForms
{
    public sealed class WinFormUserInterface : IAppUserInterface
    {
        public WinFormUserInterface(
            IServiceProvider serviceProvider,
            IAppStartup appStartup)
        {
            _serviceProvider = serviceProvider;
            _appStartup = appStartup;
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly IAppStartup _appStartup;

        public void DisplayError(IEnumerable<string> _, Exception failureReason, bool isCritical)
        {
            MessageBox.Show(
                failureReason.Message, StringResources.TitleText_Error,
                MessageBoxButtons.OK,
                (isCritical ? MessageBoxIcon.Stop : MessageBoxIcon.Warning),
                MessageBoxDefaultButton.Button1);
        }

        public void StartApplication(IEnumerable<string> args)
        {
            var appThread = new Thread(new ParameterizedThreadStart(_ =>
            {
                _ = Application.OleRequired();
                Application.EnableVisualStyles();
                _ = Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.SetCompatibleTextRenderingDefault(false);

                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(new JsonFormatter(), Path.Combine(_appStartup.AppDataDirectoryPath, "ApplicationLog.jsonl"))
                    .CreateLogger();

                using var form = ScreenBuilder.CreateMainForm(_serviceProvider);
                Application.Run(new ApplicationContext(form));
            }));

            appThread.SetApartmentState(ApartmentState.STA);
            appThread.Start(args);
            appThread.Join();
        }
    }
}
