using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Globalization;
using System.Linq;

namespace TableCloth.ManagedAi;

// Renders parsed Markdown as native controls. All navigation is delegated to the caller.
public sealed class ChatMarkdownView : StackPanel
{
    private readonly Action<Uri> _openLink;

    public ChatMarkdownView(string markdown, Action<Uri> openLink)
    {
        _openLink = openLink;
        Spacing = 10;
        try
        {
            foreach (var block in ChatMarkdown.Parse(markdown))
                if (RenderBlock(block) is { } control) Children.Add(control);
        }
        catch (ManagedAiException)
        {
            // A complex/oversized response must not crash the window or reinterpret code as links.
            Children.Clear();
            Children.Add(new SelectableTextBlock { Text = markdown[..Math.Min(markdown.Length, 65536)], TextWrapping = TextWrapping.Wrap });
        }
    }

    private Control? RenderBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var title = Paragraph(heading.Inline);
                title.FontSize = heading.Level switch { 1 => 25, 2 => 22, 3 => 19, _ => 16 };
                title.FontWeight = FontWeight.SemiBold;
                title.LineHeight = title.FontSize * 1.5;
                title.Margin = new Thickness(0, 6, 0, 2);
                title.Classes.Add("markdown-heading");
                return title;
            case ParagraphBlock paragraph: return Paragraph(paragraph.Inline);
            case CodeBlock code:
                var codeText = new SelectableTextBlock
                {
                    Text = code.Lines.ToString(), FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"),
                    FontSize = 13, LineHeight = 21, TextWrapping = TextWrapping.NoWrap
                };
                var codePanel = new StackPanel { Spacing = 6 };
                if (code is FencedCodeBlock { Info: { Length: > 0 } language })
                    codePanel.Children.Add(new TextBlock { Text = language, FontSize = 11, Opacity = 0.7 });
                codePanel.Children.Add(new ScrollViewer
                {
                    Content = codeText, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
                });
                var codeBorder = Frame(codePanel, new Thickness(12));
                codeBorder.Classes.Add("markdown-code");
                return codeBorder;
            case QuoteBlock quote:
                var quoteBorder = Frame(Blocks(quote), new Thickness(12, 4));
                quoteBorder.BorderThickness = new Thickness(3, 0, 0, 0);
                quoteBorder.Classes.Add("markdown-quote");
                return quoteBorder;
            case ListBlock list: return List(list);
            case Table table: return TableView(table);
            case ThematicBreakBlock:
                var rule = Frame(null, new Thickness(0));
                rule.Height = 1; rule.Margin = new Thickness(0, 8); return rule;
            case LinkReferenceDefinitionGroup: return null;
            case ContainerBlock container: return Blocks(container);
            case LeafBlock leaf: return Paragraph(leaf.Inline);
            default: return null;
        }
    }

    private StackPanel Blocks(ContainerBlock blocks)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var block in blocks) if (RenderBlock(block) is { } child) panel.Children.Add(child);
        return panel;
    }

    private StackPanel List(ListBlock list)
    {
        var panel = new StackPanel { Spacing = list.IsLoose ? 10 : 4 };
        panel.Classes.Add("markdown-list");
        _ = int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var number);
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
            var marker = new TextBlock
            {
                Text = list.IsOrdered ? (number++).ToString(CultureInfo.InvariantCulture) + list.OrderedDelimiter : "•",
                MinWidth = 18, FontSize = 15, LineHeight = 24, TextAlignment = TextAlignment.Right
            };
            var task = (item.FirstOrDefault() as ParagraphBlock)?.Inline?.FirstChild as TaskList;
            if (task is not null)
                row.Children.Add(new CheckBox
                {
                    IsChecked = task.Checked, IsHitTestVisible = false, Focusable = false,
                    MinHeight = 32, Height = 32, Padding = new Thickness(0), Margin = new Thickness(0, -4, 0, 0), VerticalAlignment = VerticalAlignment.Top
                });
            else row.Children.Add(marker);
            var body = Blocks(item); body.Spacing = list.IsLoose ? 8 : 3;
            Grid.SetColumn(body, 1); row.Children.Add(body);
            panel.Children.Add(row);
        }
        return panel;
    }

    private Control TableView(Table table)
    {
        var grid = new Grid();
        grid.Classes.Add("markdown-table");
        for (int i = 0; i < table.ColumnDefinitions.Count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        int rowIndex = 0;
        foreach (var row in table.OfType<TableRow>())
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            foreach (var cell in row.OfType<TableCell>())
            {
                var body = Blocks(cell);
                body.MinWidth = 96; body.MaxWidth = 260;
                if (row.IsHeader)
                    foreach (var text in body.Children.OfType<TextBlock>()) text.FontWeight = FontWeight.SemiBold;
                if (cell.ColumnIndex < table.ColumnDefinitions.Count)
                {
                    var alignment = table.ColumnDefinitions[cell.ColumnIndex].Alignment switch
                    {
                        TableColumnAlign.Center => TextAlignment.Center,
                        TableColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left
                    };
                    foreach (var text in body.Children.OfType<TextBlock>()) text.TextAlignment = alignment;
                }
                var border = Frame(body, new Thickness(10, 8));
                Grid.SetColumn(border, cell.ColumnIndex); Grid.SetColumnSpan(border, cell.ColumnSpan);
                Grid.SetRow(border, rowIndex); Grid.SetRowSpan(border, cell.RowSpan);
                grid.Children.Add(border);
            }
            rowIndex++;
        }
        return new ScrollViewer
        {
            Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private SelectableTextBlock Paragraph(ContainerInline? inlines)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 15, LineHeight = 24 };
        if (inlines is not null) Append(text.Inlines!, inlines);
        return text;
    }

    private void Append(InlineCollection target, ContainerInline source, bool linksEnabled = true)
    {
        foreach (var inline in source)
        {
            switch (inline)
            {
                case LiteralInline literal: target.Add(new Run(literal.Content.ToString())); break;
                case HtmlEntityInline entity: target.Add(new Run(entity.Transcoded.ToString())); break;
                case LineBreakInline line:
                    target.Add(line.IsHard ? new LineBreak() : new Run(" ")); break;
                case CodeInline code:
                    target.Add(new Run(code.Content) { FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace") }); break;
                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                    else if (emphasis.DelimiterCount == 2) span.FontWeight = FontWeight.Bold;
                    else span.FontStyle = FontStyle.Italic;
                    Append(span.Inlines, emphasis, linksEnabled); target.Add(span); break;
                case LinkInline { IsImage: true } image:
                    target.Add(new Run("[이미지: " + ChatMessageLinks.PlainText(image) + "]")); break;
                case LinkInline link:
                    if (linksEnabled && ChatMarkdown.GetWebLink(link.Url) is { } uri)
                        AddLink(target, uri, link);
                    else Append(target, link, linksEnabled: false);
                    break;
                case AutolinkInline auto:
                    if (linksEnabled && !auto.IsEmail && ChatMarkdown.GetWebLink(auto.Url) is { } autoUri)
                        AddLink(target, autoUri, null, auto.Url);
                    else target.Add(new Run(auto.Url));
                    break;
                case TaskList: break; // Rendered in the list marker column, outside the text baseline.
                case ContainerInline nested: Append(target, nested, linksEnabled); break;
            }
        }
    }

    private void AddLink(InlineCollection target, Uri uri, ContainerInline? label, string? plainLabel = null)
    {
        var caption = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
        if (label is not null) Append(caption.Inlines!, label, linksEnabled: false);
        else caption.Text = plainLabel;
        var name = label is null ? plainLabel : ChatMessageLinks.PlainText(label);
        if (string.IsNullOrWhiteSpace(name)) caption.Text = name = uri.OriginalString;
        var button = new Button { Content = caption, Padding = new Thickness(0), Margin = new Thickness(0), MinHeight = 0, FontSize = 15 };
        button.Classes.Add("link"); button.Classes.Add("markdown-link");
        AutomationProperties.SetName(button, name + " | " + uri.OriginalString);
        ToolTip.SetTip(button, uri.OriginalString + "\n식탁보에서 열기. Catalog에 등록된 서비스는 Spork로 필요한 소프트웨어를 설치합니다.");
        button.Click += (_, _) => _openLink(uri);
        target.Add(new InlineUIContainer { Child = button, BaselineAlignment = BaselineAlignment.Center });
    }

    private static Border Frame(Control? child, Thickness padding)
    {
        var border = new Border { Child = child, Padding = padding, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("ControlFillColorDefaultBrush"));
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("ControlStrokeColorDefaultBrush"));
        return border;
    }
}
