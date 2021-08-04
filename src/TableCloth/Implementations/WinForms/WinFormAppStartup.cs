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
    public sealed class WinFormAppStartup : IAppStartup
    {
        public WinFormAppStartup(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider _serviceProvider;

        public void InitializeEnvironment(IEnumerable<string> _)
        {
            InitializeWindowsFormsEnvironment();
            CheckWindowsSandboxPrerequisites();
            CheckPreLaunchedAppInstance();
            InitializeAppDataDirectory();
            ConfigureLowLevelLogging();
        }

        public void StartApplication(IEnumerable<string> args)
        {
            var appThread = new Thread(new ParameterizedThreadStart(_ =>
            {
                using var form = ScreenBuilder.CreateMainForm(_serviceProvider);
                Application.Run(new ApplicationContext(form));
            }));

            appThread.SetApartmentState(ApartmentState.STA);
            appThread.Start(args);
            appThread.Join();
        }

        private static string AppDataDirectoryPath
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TableCloth");

        private static void InitializeWindowsFormsEnvironment()
        {
            _ = Application.OleRequired();
            Application.EnableVisualStyles();
            _ = Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetCompatibleTextRenderingDefault(false);
        }

        private static void CheckWindowsSandboxPrerequisites()
        {
            var is64BitOperatingSystem = (IntPtr.Size == 8) || NativeMethods.InternalCheckIsWow64();

            if (!is64BitOperatingSystem)
            {
                _ = MessageBox.Show(StringResources.Error_Windows_OS_Too_Old, StringResources.TitleText_Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button1);
                Environment.Exit(1);
            }

            var wsbExecPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsSandbox.exe");

            if (!File.Exists(wsbExecPath))
            {
                _ = MessageBox.Show(StringResources.Error_Windows_Sandbox_Missing, StringResources.TitleText_Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Stop, MessageBoxDefaultButton.Button1);
                Environment.Exit(2);
            }
        }

        private static void CheckPreLaunchedAppInstance()
        {
            _ = new Mutex(true, typeof(Program).FullName, out var isFirstInstance);

            if (isFirstInstance)
                return;

            _ = MessageBox.Show(StringResources.Error_Already_TableCloth_Running, StringResources.TitleText_Warning,
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);

            Environment.Exit(3);
        }

        private static void InitializeAppDataDirectory()
        {
            var targetPath = AppDataDirectoryPath;

            if (Directory.Exists(targetPath))
                return;

            try { Directory.CreateDirectory(targetPath); }
            catch (Exception e)
            {
                var message = StringResources.Error_Cannot_Create_AppDataDirectory(e);

                _ = MessageBox.Show(null,
                    message, StringResources.TitleText_Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);

                Environment.Exit(4);
            }
        }

        private static void ConfigureLowLevelLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.File(new JsonFormatter(), Path.Combine(AppDataDirectoryPath, "ApplicationLog.jsonl"))
                .CreateLogger();
        }
    }
}
