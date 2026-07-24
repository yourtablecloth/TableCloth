using System;
using System.Runtime.CompilerServices;
using System.Windows;
using TableCloth.Models;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// Windows Presentation Foundation의 메시지 상자 표시 기능을 구현합니다.
    /// 공개 계약은 UI 중립 열거형(<see cref="AppMessageBoxButton"/> 등)이며, 내부에서 WPF 타입으로 매핑합니다.
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

        /// <summary>
        /// 정보를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        public AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton messageBoxButton = AppMessageBoxButton.OK)
        {
            var result = (MessageBoxResult)_applicationService.DispatchInvoke(new Func<MessageBoxResult>(() =>
            {
                return _messageBoxService.Show(
                    _applicationService.GetMainWindow(), message, BrandStrings.TitleText_Info,
                    ToWpf(messageBoxButton), MessageBoxImage.Information,
                    MessageBoxResult.OK);
            }), new object[] { });
            return FromWpf(result);
        }

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        public AppMessageBoxResult DisplayError(Exception failureReason, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => DisplayError(StringResources.TableCloth_UnwrapException(failureReason), isCritical, file, member, line);

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        public AppMessageBoxResult DisplayError(string message, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => DisplayErrorCore(message, isCritical, file, member, line);

        private AppMessageBoxResult DisplayErrorCore(string message, bool isCritical, string file, string member, int line)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = StringResources.Error_Unknown(file, member, line);

            var title = isCritical ? BrandStrings.TitleText_Error : BrandStrings.TitleText_Warning;
            var image = isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning;

            var result = (MessageBoxResult)_applicationService.DispatchInvoke(new Func<MessageBoxResult>(() =>
            {
                return _messageBoxService.Show(
                    _applicationService.GetMainWindow(), message, title, MessageBoxButton.OK,
                    image, MessageBoxResult.OK);
            }), new object[] { });
            return FromWpf(result);
        }

        public AppMessageBoxResult DisplayQuestion(string message, AppMessageBoxButton messageBoxButton = AppMessageBoxButton.YesNo, AppMessageBoxResult defaultAnswer = AppMessageBoxResult.Yes)
        {
            var result = (MessageBoxResult)_applicationService.DispatchInvoke(new Func<MessageBoxResult>(() =>
            {
                return _messageBoxService.Show(
                    _applicationService.GetMainWindow(), message, BrandStrings.TitleText_Question,
                    ToWpf(messageBoxButton), MessageBoxImage.Question, ToWpf(defaultAnswer));
            }), new object[] { });
            return FromWpf(result);
        }

        // UI 중립 열거형 ↔ WPF 매핑
        private static MessageBoxButton ToWpf(AppMessageBoxButton value) => value switch
        {
            AppMessageBoxButton.OKCancel => MessageBoxButton.OKCancel,
            AppMessageBoxButton.YesNo => MessageBoxButton.YesNo,
            AppMessageBoxButton.YesNoCancel => MessageBoxButton.YesNoCancel,
            _ => MessageBoxButton.OK,
        };

        private static MessageBoxResult ToWpf(AppMessageBoxResult value) => value switch
        {
            AppMessageBoxResult.OK => MessageBoxResult.OK,
            AppMessageBoxResult.Cancel => MessageBoxResult.Cancel,
            AppMessageBoxResult.Yes => MessageBoxResult.Yes,
            AppMessageBoxResult.No => MessageBoxResult.No,
            _ => MessageBoxResult.None,
        };

        private static AppMessageBoxResult FromWpf(MessageBoxResult value) => value switch
        {
            MessageBoxResult.OK => AppMessageBoxResult.OK,
            MessageBoxResult.Cancel => AppMessageBoxResult.Cancel,
            MessageBoxResult.Yes => AppMessageBoxResult.Yes,
            MessageBoxResult.No => AppMessageBoxResult.No,
            _ => AppMessageBoxResult.None,
        };
    }
}
