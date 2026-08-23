using System;
using System.Runtime.CompilerServices;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

/// <summary>
/// 이슈 #296: WPF MessageBox 매핑 제거. 공개 계약(UI 중립 <see cref="AppMessageBoxButton"/> 등)을 그대로
/// 자작 <see cref="TableCloth.Dialogs.MessageBoxWindow"/> 기반 <see cref="IMessageBoxService"/> 로 전달한다.
/// </summary>
public sealed class AppMessageBox(
    IApplicationService applicationService,
    IMessageBoxService messageBoxService) : IAppMessageBox
{
    public AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton messageBoxButton = AppMessageBoxButton.OK)
        => messageBoxService.Show(
            applicationService.GetActiveWindow(), message, UIStringResources.TitleText_Info,
            messageBoxButton, AppMessageBoxImage.Information, AppMessageBoxResult.OK);

    public AppMessageBoxResult DisplayQuestion(string message, AppMessageBoxButton messageBoxButton = AppMessageBoxButton.YesNo, AppMessageBoxResult defaultAnswer = AppMessageBoxResult.Yes)
        => messageBoxService.Show(
            applicationService.GetActiveWindow(), message, UIStringResources.TitleText_Info,
            messageBoxButton, AppMessageBoxImage.Question, defaultAnswer);

    public AppMessageBoxResult DisplayError(Exception? failureReason, bool isCritical,
        [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => DisplayErrorCore(StringResources.TableCloth_UnwrapException(failureReason), isCritical, file, member, line);

    public AppMessageBoxResult DisplayError(string? message, bool isCritical,
        [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        => DisplayErrorCore(message, isCritical, file, member, line);

    private AppMessageBoxResult DisplayErrorCore(string? message, bool isCritical, string file, string member, int line)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = StringResources.Error_Unknown(file, member, line);

        var title = isCritical ? UIStringResources.TitleText_Error : UIStringResources.TitleText_Warning;
        var image = isCritical ? AppMessageBoxImage.Error : AppMessageBoxImage.Warning;

        return messageBoxService.Show(
            applicationService.GetActiveWindow(), message, title, AppMessageBoxButton.OK, image, AppMessageBoxResult.OK);
    }
}
