using System;
using System.Windows;
using System.Windows.Threading;
using TableCloth.Resources;

namespace TableCloth.Components
{
    /// <summary>
    /// Windows Presentation Foundation의 메시지 상자 표시 기능을 구현합니다.
    /// </summary>
    public sealed class AppMessageBox
    {
        /// <summary>
        /// 정보를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="messageBoxButton">메시지 박스 버튼 구성</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayInfo(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK)
        {
            var dispatcher = App.Current.Dispatcher;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (MessageBoxResult)dispatcher.Invoke(
                new Func<string, MessageBoxResult>((message) =>
                {
                    return MessageBox.Show(
                        App.Current.MainWindow, message, StringResources.TitleText_Info,
                        messageBoxButton, MessageBoxImage.Information, MessageBoxResult.OK);
                }),
                new object[] { message, });
        }

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="failureReason">발생한 예외 개체의 참조</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayError(Exception failureReason, bool isCritical)
        {
            var unwrappedException = failureReason;

            if (failureReason is AggregateException ae)
                unwrappedException = ae.InnerException;

            return DisplayError(unwrappedException?.Message ?? StringResources.UnknownText, isCritical);
        }

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayError(string message, bool isCritical)
        {
            var dispatcher = App.Current.Dispatcher;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (MessageBoxResult)dispatcher.Invoke(
                new Func<string, bool, MessageBoxResult>((message, isCritical) =>
                {
                    return MessageBox.Show(
                        App.Current.MainWindow, message,
                        isCritical ? StringResources.TitleText_Error : StringResources.TitleText_Warning,
                        MessageBoxButton.OK,
                        isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning, MessageBoxResult.OK);
                }),
                new object[] { message, isCritical });
        }
    }
}
