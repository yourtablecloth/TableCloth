using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class AppUserInterface : IAppUserInterface
    {
        public AppUserInterface(
            IAppStartup appStartup,
            IX509CertPairScanner certPairScanner,
            ISandboxBuilder sandboxBuilder,
            IAppMessageBox appMessageBox,
            ISandboxLauncher sandboxLauncher)
        {
            _appStartup = appStartup;
            _certPairScanner = certPairScanner;
            _sandboxBuilder = sandboxBuilder;
            _appMessageBox = appMessageBox;
            _sandboxLauncher = sandboxLauncher;
        }

        private readonly IAppStartup _appStartup;
        private readonly IX509CertPairScanner _certPairScanner;
        private readonly ISandboxBuilder _sandboxBuilder;
        private readonly IAppMessageBox _appMessageBox;
        private readonly ISandboxLauncher _sandboxLauncher;

        private App _appInstance;

        public object MainWindowHandle
            => _appInstance.MainWindow;

        public void StartApplication(IEnumerable<string> args)
        {
            var appThread = new Thread(new ParameterizedThreadStart(_ =>
            {
                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(new JsonFormatter(), Path.Combine(_appStartup.AppDataDirectoryPath, "ApplicationLog.jsonl"))
                    .WriteTo.Sentry(o =>
                    {
                        o.Dsn = StringResources.SentryDsn;
                        o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                        o.MinimumEventLevel = LogEventLevel.Warning;
                    })
                    .CreateLogger();

                _appInstance = new App();
                _appInstance.Run();
            }));

            appThread.SetApartmentState(ApartmentState.STA);
            appThread.Start(args);
            appThread.Join();
        }
    }
}
