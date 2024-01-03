using System;
using System.Windows;
using System.Windows.Threading;
using TableCloth.Resources;

namespace Hostess.Components
{
    /// <summary>
    /// Windows Presentation Foundation의 메시지 상자 표시 기능을 구현합니다.
    /// </summary>
    public sealed class AppMessageBox
    {
        public AppMessageBox(
            Application application)
        {
            _application = application;
        }

        private readonly Application _application;

        /// <summary>
        /// 정보를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="messageBoxButton">메시지 박스 버튼 구성</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayInfo(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK)
        {
            var dispatcher = _application.Dispatcher;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (MessageBoxResult)dispatcher.Invoke(
                new Func<string, MessageBoxButton, MessageBoxResult>((_message, _messageBoxButton) =>
                {
                    // owner 파라미터를 null 참조로 지정하더라도 Windows Forms 처럼 parent-less 메시지 박스를 만들어주지는 않음.
                    // GH-121 fix
                    var owner = Application.Current.MainWindow;

                    if (owner != null)
                    {
                        return MessageBox.Show(
                            owner, _message, StringResources.TitleText_Info,
                            _messageBoxButton, MessageBoxImage.Information,
                            MessageBoxResult.OK);
                    }
                    else
                    {
                        return MessageBox.Show(
                            _message, StringResources.TitleText_Info,
                            _messageBoxButton, MessageBoxImage.Information,
                            MessageBoxResult.OK);
                    }
                }),
                new object[] { message, messageBoxButton, });
        }

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="failureReason">발생한 예외 개체의 참조</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayError(Exception failureReason, bool isCritical)
            => DisplayError(StringResources.TableCloth_UnwrapException(failureReason), isCritical);

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        public MessageBoxResult DisplayError(string message, bool isCritical)
        {
            var dispatcher = _application.Dispatcher;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (MessageBoxResult)dispatcher.Invoke(
                new Func<string, bool, MessageBoxResult>((_message, _isCritical) =>
                {
                    if (string.IsNullOrWhiteSpace(_message))
                        _message = StringResources.Error_Unknown();

                    var owner = Application.Current.MainWindow;
                    var title = _isCritical ? StringResources.TitleText_Error : StringResources.TitleText_Warning;
                    var image = _isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning;

                    // owner 파라미터를 null 참조로 지정하더라도 Windows Forms 처럼 parent-less 메시지 박스를 만들어주지는 않음.
                    // GH-121 fix
                    if (owner != null)
                    {
                        return MessageBox.Show(owner,
                            _message, title, MessageBoxButton.OK,
                            image, MessageBoxResult.OK);
                    }
                    else
                    {
                        return MessageBox.Show(
                            _message, title, MessageBoxButton.OK,
                            image, MessageBoxResult.OK);
                    }
                }),
                new object[] { message, isCritical });
        }

        public MessageBoxResult DisplayQuestion(string message, MessageBoxButton messageBoxButton = MessageBoxButton.YesNo, MessageBoxResult defaultAnswer = MessageBoxResult.Yes)
        {
            var dispatcher = _application.Dispatcher;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (MessageBoxResult)dispatcher.Invoke(
                new Func<string, MessageBoxButton, MessageBoxResult>((_message, _messageBoxButton) =>
                {
                    // owner 파라미터를 null 참조로 지정하더라도 Windows Forms 처럼 parent-less 메시지 박스를 만들어주지는 않음.
                    // GH-121 fix
                    var owner = Application.Current.MainWindow;

                    if (owner != null)
                    {
                        return MessageBox.Show(
                            owner, _message, StringResources.TitleText_Question,
                            _messageBoxButton, MessageBoxImage.Question,
                            defaultAnswer);
                    }
                    else
                    {
                        return MessageBox.Show(
                            _message, StringResources.TitleText_Question,
                            _messageBoxButton, MessageBoxImage.Question,
                            defaultAnswer);
                    }
                }),
                new object[] { message, messageBoxButton, });
        }
    }
}
