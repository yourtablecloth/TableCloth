using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Collections.Generic;
using System.Threading;
using TableCloth.Resources;

namespace TableCloth.Components
{
    public sealed class AppUserInterface
    {
        public AppUserInterface(
            SharedLocations sharedLocations,
            Preferences preferences)
        {
            _sharedLocations = sharedLocations;
            _preferences = preferences;
        }

        private readonly SharedLocations _sharedLocations;
        private readonly Preferences _preferences;

        private App _appInstance;

        public object MainWindowHandle
            => _appInstance.MainWindow;

        public void StartApplication(IEnumerable<string> args)
        {
            var appThread = new Thread(new ParameterizedThreadStart(_args =>
            {
                var config = _preferences.LoadPreferences();

                var logBuilder = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(new JsonFormatter(), _sharedLocations.ApplicationLogPath);

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

                _appInstance = new App() { Arguments = (IEnumerable<string>)_args, };
                _appInstance.Run();
            }));

            appThread.SetApartmentState(ApartmentState.STA);
            appThread.Start(args);
            appThread.Join();
        }
    }
}
