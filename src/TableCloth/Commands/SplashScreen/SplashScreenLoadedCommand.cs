using Sentry;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.SplashScreen;

public sealed class SplashScreenLoadedCommand : CommandBase
{
    public SplashScreenLoadedCommand(
        AppStartup appStartup,
        AppMessageBox appMessageBox,
        CommandLineParser commandLineParser,
        PreferencesManager preferencesManager)
    {
        _appStartup = appStartup;
        _appMessageBox = appMessageBox;
        _commandLineParser = commandLineParser;
        _preferencesManager = preferencesManager;
    }

    private readonly AppStartup _appStartup;
    private readonly AppMessageBox _appMessageBox;
    private readonly CommandLineParser _commandLineParser;
    private readonly PreferencesManager _preferencesManager;

    public override async void Execute(object? parameter)
    {
        if (parameter is not SplashScreenViewModel viewModel)
            throw new ArgumentException("Selected paramter is not a compatible object.", nameof(parameter));

        try
        {
            await Task.Delay(5000);

            using var _ = SentrySdk.Init(o =>
            {
                o.Dsn = StringResources.SentryDsn;
                o.Debug = true;
                o.TracesSampleRate = 1.0;
            });

            var preferences = _preferencesManager.LoadPreferences();
            viewModel.V2UIOptedIn = preferences?.V2UIOptIn ?? true;

            var helpMessage = default(string);

            if (viewModel.V2UIOptedIn)
            {
                viewModel.ParsedArgument = _commandLineParser.ParseForV2(viewModel.PassedArguments.ToArray());
                helpMessage = StringResources.TableCloth_TableCloth_Switches_Help_V2;
            }
            else
            {
                viewModel.ParsedArgument = _commandLineParser.ParseForV1(viewModel.PassedArguments.ToArray());
                helpMessage = StringResources.TableCloth_TableCloth_Switches_Help_V1;
            }

            if (viewModel.ParsedArgument.ShowCommandLineHelp)
            {
                _appMessageBox.DisplayInfo(helpMessage, MessageBoxButton.OK);
                return;
            }

            if (!_appStartup.HasRequirementsMet(viewModel.Warnings, out Exception? failedReason, out bool isCritical))
            {
                _appMessageBox.DisplayError(failedReason, isCritical);

                if (isCritical)
                    return;
            }

            if (viewModel.Warnings.Any())
                _appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, viewModel.Warnings), false);

            if (!_appStartup.Initialize(out failedReason, out isCritical))
            {
                _appMessageBox.DisplayError(failedReason, isCritical);

                if (isCritical)
                    return;
            }

            viewModel.AppStartupSucceed = true;
        }
        catch (Exception ex)
        {
            viewModel.AppStartupSucceed = false;
            _appMessageBox.DisplayError(ex, true);
        }
        finally
        {
            viewModel.NotifyInitialized(this, new DialogRequestEventArgs(viewModel.AppStartupSucceed));
        }
    }
}
