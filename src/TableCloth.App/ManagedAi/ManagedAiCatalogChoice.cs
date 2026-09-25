using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Models.Catalog;

namespace TableCloth.ManagedAi;

public interface IManagedAiCatalogChoice
{
    Task<string?> SelectAsync(Uri target, IReadOnlyList<CatalogInternetService> candidates, CancellationToken cancellationToken);
}

public sealed class ManagedAiCatalogChoice(IApplicationService application) : IManagedAiCatalogChoice
{
    public async Task<string?> SelectAsync(Uri target, IReadOnlyList<CatalogInternetService> candidates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = application.GetActiveWindow() ?? application.GetMainWindow();
        if (owner is null) throw new ManagedAiException(AiFailureCode.BrowserOpenFailed);
        var window = new Window
        {
            Title = "Catalog 서비스 선택", Width = 560, Height = 460, MinWidth = 400, MinHeight = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(20), RowSpacing = 12 };
        root.Children.Add(new TextBlock
        {
            Text = "같은 도메인을 사용하는 서비스가 여러 개 있습니다. 이용할 서비스를 선택하면 Spork로 필요한 소프트웨어를 설치합니다.",
            TextWrapping = TextWrapping.Wrap
        });
        var destination = new SelectableTextBlock { Text = target.OriginalString, TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(destination, 1); root.Children.Add(destination);
        var list = new ListBox();
        foreach (var service in candidates)
        {
            var label = new StackPanel { Spacing = 4 };
            label.Children.Add(new TextBlock { Text = service.DisplayName, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            label.Children.Add(new TextBlock { Text = service.Url, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
            label.Children.Add(new TextBlock { Text = $"설치 항목 {service.PackageCountForDisplay}개", FontSize = 12 });
            list.Items.Add(new ListBoxItem { Content = label, Tag = service.Id });
        }
        Grid.SetRow(list, 2); root.Children.Add(list);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var cancel = new Button { Content = "취소" };
        var open = new Button { Content = "설치 후 열기", IsEnabled = false };
        open.Classes.Add("accent");
        list.SelectionChanged += (_, _) => open.IsEnabled = list.SelectedItem is ListBoxItem;
        open.Click += (_, _) => window.Close((list.SelectedItem as ListBoxItem)?.Tag as string);
        cancel.Click += (_, _) => window.Close((string?)null);
        buttons.Children.Add(cancel); buttons.Children.Add(open);
        Grid.SetRow(buttons, 3); root.Children.Add(buttons);
        window.Content = root;
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(() => window.Close((string?)null)));
        var selected = await window.ShowDialog<string?>(owner);
        cancellationToken.ThrowIfCancellationRequested();
        return selected;
    }
}
