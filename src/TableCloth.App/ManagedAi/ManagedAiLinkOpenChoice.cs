using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;

namespace TableCloth.ManagedAi;

public enum ManagedAiLinkOpenDestination
{
    Cancel,
    WindowsSandbox,
    CurrentBrowser
}

public interface IManagedAiLinkOpenChoice
{
    Task<ManagedAiLinkOpenDestination> SelectAsync(Uri target, CancellationToken cancellationToken);
}

public sealed class ManagedAiLinkOpenChoice(IApplicationService application) : IManagedAiLinkOpenChoice
{
    public async Task<ManagedAiLinkOpenDestination> SelectAsync(Uri target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = application.GetActiveWindow() ?? application.GetMainWindow();
        if (owner is null) throw new ManagedAiException(AiFailureCode.BrowserOpenFailed);
        var window = new ManagedAiLinkOpenWindow(target);
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(
            () => window.Close(ManagedAiLinkOpenDestination.Cancel)));
        var selected = await window.ShowDialog<ManagedAiLinkOpenDestination>(owner);
        cancellationToken.ThrowIfCancellationRequested();
        return selected;
    }
}

internal sealed class ManagedAiLinkOpenWindow : Window
{
    public ManagedAiLinkOpenWindow(Uri target)
    {
        Title = "링크 열기";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 520;
        MinWidth = 420;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
        root.Children.Add(new TextBlock
        {
            Text = "이 링크를 어디에서 열지 선택합니다.",
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        var destination = new SelectableTextBlock
        {
            Text = target.OriginalString,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 100
        };
        AutomationProperties.SetAutomationId(destination, "ManagedAiLinkTarget");
        Grid.SetRow(destination, 1);
        root.Children.Add(destination);
        var description = new TextBlock
        {
            Text = "Windows Sandbox를 선택하면 TableCloth Catalog를 확인하고 필요한 소프트웨어를 Spork로 설치합니다. " +
                "현재 브라우저를 선택하면 Windows 기본 브라우저에서 바로 엽니다.",
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(description, 2);
        root.Children.Add(description);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var cancel = Button("취소", "ManagedAiLinkCancel");
        cancel.IsCancel = true;
        cancel.Click += (_, _) => Close(ManagedAiLinkOpenDestination.Cancel);
        var browser = Button("현재 브라우저에서 열기", "ManagedAiLinkCurrentBrowser");
        browser.Click += (_, _) => Close(ManagedAiLinkOpenDestination.CurrentBrowser);
        var sandbox = Button("Windows Sandbox에서 열기", "ManagedAiLinkWindowsSandbox");
        sandbox.IsDefault = true;
        sandbox.Classes.Add("accent");
        sandbox.Click += (_, _) => Close(ManagedAiLinkOpenDestination.WindowsSandbox);
        buttons.Children.Add(cancel);
        buttons.Children.Add(browser);
        buttons.Children.Add(sandbox);
        Grid.SetRow(buttons, 3);
        root.Children.Add(buttons);
        Content = root;
        AutomationProperties.SetAutomationId(this, "ManagedAiLinkOpenDialog");
    }

    private static Button Button(string text, string automationId)
    {
        var button = new Button { Content = text, MinWidth = 96, Padding = new Thickness(12, 6) };
        AutomationProperties.SetAutomationId(button, automationId);
        return button;
    }
}
