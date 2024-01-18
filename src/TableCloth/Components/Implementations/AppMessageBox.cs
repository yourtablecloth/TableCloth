﻿using System;
using System.Linq;
using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Components;

/// <summary>
/// Windows Presentation Foundation의 메시지 상자 표시 기능을 구현합니다.
/// </summary>
public sealed class AppMessageBox(
    IApplicationService applicationService,
    IMessageBoxService messageBoxService) : IAppMessageBox
{
    /// <summary>
    /// 정보를 안내하는 메시지 상자를 띄웁니다.
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    /// <param name="messageBoxButton">메시지 박스 버튼 구성</param>
    /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
    public MessageBoxResult DisplayInfo(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK)
    {
        return (MessageBoxResult)applicationService.DispatchInvoke(() =>
        {
            return messageBoxService.Show(
                applicationService.GetActiveWindow(), message, UIStringResources.TitleText_Info,
                messageBoxButton, MessageBoxImage.Information,
                MessageBoxResult.OK);
        }, new object[] { } )!;
    }

    /// <summary>
    /// 오류를 안내하는 메시지 상자를 띄웁니다.
    /// </summary>
    /// <param name="failureReason">발생한 예외 개체의 참조</param>
    /// <param name="isCritical">심각성 여부</param>
    /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
    public MessageBoxResult DisplayError(Exception? failureReason, bool isCritical)
        => DisplayError(StringResources.TableCloth_UnwrapException(failureReason), isCritical);

    /// <summary>
    /// 오류를 안내하는 메시지 상자를 띄웁니다.
    /// </summary>
    /// <param name="message">표시할 메시지</param>
    /// <param name="isCritical">심각성 여부</param>
    /// <returns>누른 버튼이 무엇인지 반환합니다.</returns>
    public MessageBoxResult DisplayError(string? message, bool isCritical)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = StringResources.Error_Unknown();

        var title = isCritical ? UIStringResources.TitleText_Error : UIStringResources.TitleText_Warning;
        var image = isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning;

        return (MessageBoxResult)applicationService.DispatchInvoke(() =>
        {
            return messageBoxService.Show(
                applicationService.GetActiveWindow(), message, title, MessageBoxButton.OK,
                image, MessageBoxResult.OK);
        }, new object[] { })!;
    }
}