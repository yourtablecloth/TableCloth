using System;
using System.Runtime.CompilerServices;
using TableCloth.Models;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// 이슈 #296: WPF MessageBox 매핑 제거. 공개 계약(UI 중립 <see cref="AppMessageBoxButton"/> 등)을 그대로
    /// 자작 <see cref="Spork.Dialogs.MessageBoxWindow"/> 기반 <see cref="IMessageBoxService"/> 로 전달한다.
    /// </summary>
    public sealed class AppMessageBox : IAppMessageBox
    {
        public AppMessageBox(
            IApplicationService applicationService,
            IMessageBoxService messageBoxService)
        {
            _applicationService = applicationService;
            _messageBoxService = messageBoxService;
        }

        private readonly IApplicationService _applicationService;
        private readonly IMessageBoxService _messageBoxService;

        public AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton messageBoxButton = AppMessageBoxButton.OK)
            => _messageBoxService.Show(
                null, message, BrandStrings.TitleText_Info,
                messageBoxButton, AppMessageBoxImage.Information, AppMessageBoxResult.OK);

        public AppMessageBoxResult DisplayError(Exception failureReason, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => DisplayError(StringResources.TableCloth_UnwrapException(failureReason), isCritical, file, member, line);

        public AppMessageBoxResult DisplayError(string message, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = StringResources.Error_Unknown(file, member, line);

            var title = isCritical ? BrandStrings.TitleText_Error : BrandStrings.TitleText_Warning;
            var image = isCritical ? AppMessageBoxImage.Error : AppMessageBoxImage.Warning;

            return _messageBoxService.Show(
                null, message, title, AppMessageBoxButton.OK, image, AppMessageBoxResult.OK);
        }

        public AppMessageBoxResult DisplayQuestion(string message,
            AppMessageBoxButton messageBoxButton = AppMessageBoxButton.YesNo,
            AppMessageBoxResult defaultAnswer = AppMessageBoxResult.Yes)
            => _messageBoxService.Show(
                null, message, BrandStrings.TitleText_Question,
                messageBoxButton, AppMessageBoxImage.Question, defaultAnswer);
    }
}
