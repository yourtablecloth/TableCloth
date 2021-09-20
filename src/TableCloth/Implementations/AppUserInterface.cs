using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Collections.Generic;
using System.Threading;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class AppUserInterface : IAppUserInterface
    {
        public AppUserInterface(
            ISharedLocations sharedLocations,
            IPreferences preferences)
        {
            _sharedLocations = sharedLocations;
            _preferences = preferences;
        }

        private readonly ISharedLocations _sharedLocations;
        private readonly IPreferences _preferences;

        private App _appInstance;

        public object MainWindowHandle
            => _appInstance.MainWindow;

        public void StartApplication(IEnumerable<string> args)
        {
            var appThread = new Thread(new ParameterizedThreadStart(_ =>
            {
                var config = _preferences.LoadConfig();

                var logBuilder = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(new JsonFormatter(), _sharedLocations.GetDataPath("ApplicationLog.jsonl"));

                if (config.UseLogCollection)
                {
                    logBuilder = logBuilder.WriteTo.Sentry(o =>
                    {
                        o.Dsn = StringResources.SentryDsn;
                        o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                        o.MinimumEventLevel = LogEventLevel.Warning;
                    });
                }

                Log.Logger = logBuilder.CreateLogger();

                _appInstance = new App();
                _appInstance.Run();
            }));

            appThread.SetApartmentState(ApartmentState.STA);
            appThread.Start(args);
            appThread.Join();
        }
    }
}
