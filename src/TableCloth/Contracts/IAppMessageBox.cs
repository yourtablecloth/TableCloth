using System;
using System.Windows;

namespace TableCloth.Contracts
{
    /// <summary>
    /// 메시지 상자를 표시하는 기능의 인터페이스를 정의합니다.
    /// </summary>
    public interface IAppMessageBox
    {
        /// <summary>
        /// 정보를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="parentWindowHandle">메시지 상자가 속한 부모 창의 핸들</param>
        /// <param name="message">표시할 메시지</param>
        /// <param name="messageBoxButton">메시지 박스 버튼 구성</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        MessageBoxResult DisplayInfo(object? parentWindowHandle, string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK);

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="parentWindowHandle">메시지 상자가 속한 부모 창의 핸들</param>
        /// <param name="failureReason">발생한 예외 개체의 참조</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        int DisplayError(object? parentWindowHandle, Exception failureReason, bool isCritical);

        /// <summary>
        /// 오류를 안내하는 메시지 상자를 띄웁니다.
        /// </summary>
        /// <param name="parentWindowHandle">메시지 상자가 속한 부모 창의 핸들</param>
        /// <param name="message">표시할 메시지</param>
        /// <param name="isCritical">심각성 여부</param>
        /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
        int DisplayError(object? parentWindowHandle, string message, bool isCritical);
    }
}
