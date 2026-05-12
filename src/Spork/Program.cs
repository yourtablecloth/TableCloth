using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spork.App.DependencyInjection;
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using TableCloth;
using TableCloth.Models.Answers;

namespace Spork
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                var answer = default(SporkAnswers);

                try
                {
                    // Spork.exe와 동일 디렉터리 (단일 파일 게시에서도 안전한 AppContext.BaseDirectory 사용)
                    var exeDirectory = AppContext.BaseDirectory;
                    var answerFilePath = Path.Combine(exeDirectory, "SporkAnswers.json");

                    if (File.Exists(answerFilePath))
                    {
                        using (var answerFileContent = File.OpenRead(answerFilePath))
                        {
                            answer = JsonSerializer.Deserialize<SporkAnswers>(answerFileContent);
                        }
                    }
                }
                catch { answer = default; }

                if (!string.IsNullOrWhiteSpace(answer?.HostUILocale))
                {
                    var desiredCulture = new CultureInfo(answer.HostUILocale);
                    Thread.CurrentThread.CurrentCulture = desiredCulture;
                    Thread.CurrentThread.CurrentUICulture = desiredCulture;
                    CultureInfo.DefaultThreadCurrentCulture = desiredCulture;
                    CultureInfo.DefaultThreadCurrentUICulture = desiredCulture;
                }

                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    MessageBox.Show(
                        e.ExceptionObject?.ToString() ?? "Unknown Error",
                        "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
                };

                if (args == null)
                    args = Helpers.GetCommandLineArguments();

                var builder = Host.CreateApplicationBuilder(args);

                // Spork 모듈 합성: Sentry/로깅/HTTP/Components/Browsers/Steps/UI/Application 일괄 등록.
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
