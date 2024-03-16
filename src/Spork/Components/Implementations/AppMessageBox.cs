using System;
using System.Runtime.CompilerServices;
using System.Windows;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// Windows Presentation Foundation의 메시지 상자 표시 기능을 구현합니다.
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
        /// <param name="message">표시할 메시지</param>
        /// <param name="messageBoxButton">메시지 박스 버튼 구성</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayInfo(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK)
        {
            return (MessageBoxResult)_applicationService.DispatchInvoke(new Func<MessageBoxResult>(() =>
            {
                return _messageBoxService.Show(
                    _applicationService.GetMainWindow(), message, UIStringResources.TitleText_Info,
                    messageBoxButton, MessageBoxImage.Information,
                    MessageBoxResult.OK);
            }), new object[] { });
        }

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="failureReason">발생한 예외 개체의 참조</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayError(Exception failureReason, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => DisplayError(StringResources.TableCloth_UnwrapException(failureReason), isCritical, file, member, line);

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayError(string message, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => DisplayErrorCore(message, isCritical, file, member, line);

        private MessageBoxResult DisplayErrorCore(string message, bool isCritical, string file, string member, int line)
        {
            if (string.IsNullOrWhiteSpace(message))
                message = StringResources.Error_Unknown(file, member, line);

            var owner = Application.Current.MainWindow;
            var title = isCritical ? UIStringResources.TitleText_Error : UIStringResources.TitleText_Warning;
            var image = isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning;

            return (MessageBoxResult)_applicationService.DispatchInvoke(new Func<MessageBoxResult>(() =>
            {
                return _messageBoxService.Show(
                    _applicationService.GetMainWindow(), message, title, MessageBoxButton.OK,
                    image, MessageBoxResult.OK);
            }), new object[] { });
        }

        public MessageBoxResult DisplayQuestion(string message, MessageBoxButton messageBoxButton = MessageBoxButton.YesNo, MessageBoxResult defaultAnswer = MessageBoxResult.Yes)
        {
            return (MessageBoxResult)_applicationService.DispatchInvoke(new Func<MessageBoxResult>(() =>
            {
                return _messageBoxService.Show(
                    _applicationService.GetMainWindow(), message, UIStringResources.TitleText_Question,
                    messageBoxButton, MessageBoxImage.Question, defaultAnswer);
            }), new object[] { });
        }
    }
}
