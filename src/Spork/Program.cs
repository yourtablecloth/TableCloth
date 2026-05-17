using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spork.App.DependencyInjection;
using System;
using System.Windows;
using TableCloth;

namespace Spork
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    MessageBox.Show(
                        e.ExceptionObject?.ToString() ?? "Unknown Error",
                        "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
                };

                args ??= Helpers.GetCommandLineArguments();

                var builder = Host.CreateApplicationBuilder(args);

                // Spork 모듈 합성: SporkAnswers/컬처 + Sentry/로깅/HTTP/Components/Browsers/Steps/UI/Application.
                builder.UseSpork();

                using var appHost = builder.Build();
                appHost.Start();
                var app = appHost.Services.GetRequiredService<Application>();
                app.Run();
                appHost.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex?.ToString() ?? "Unknown Error",
                    "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return Environment.ExitCode;
        }
    }
}
