using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using TableCloth.Models.Answers;

namespace Sponge
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
            => RunApp(args);

        // To Do: Not Working
        private static void SetDefaultCulture(CultureInfo desiredCulture)
        {
            Thread.CurrentThread.CurrentCulture = desiredCulture;
            Thread.CurrentThread.CurrentUICulture = desiredCulture;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static int RunApp(string[] _)
        {
            var answer = DeserializeSpongeAnswersJson();

            if (!string.IsNullOrWhiteSpace(answer?.HostUILocale))
            {
                var desiredCulture = new CultureInfo(answer.HostUILocale);
                SetDefaultCulture(desiredCulture);
            }

            var app = new App();
            app.InitializeComponent();
            app.Run();

            return Environment.ExitCode;
        }

        internal static SpongeAnswers DeserializeSpongeAnswersJson()
        {
            try
            {
                var answerFilePath = Path.GetFullPath("SpongeAnswers.json");

                if (File.Exists(answerFilePath))
                {
                    using (var answerFileContent = File.OpenRead(answerFilePath))
                    {
                        return JsonSerializer.Deserialize<SpongeAnswers>(answerFileContent);
                    }
                }

                return default;
            }
            catch { return default; }
        }
    }
}
