using System.Text;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TableCloth.ManagedAi;

public sealed record ChatTextPart(string Text, Uri? Link = null);

// Plain-text link preview using the same syntax tree as the chat renderer. URLs in code are not links.
public static class ChatMessageLinks
{
    public static IReadOnlyList<ChatTextPart> Parse(string text)
    {
        var document = ChatMarkdown.Parse(text);
        var parts = new List<ChatTextPart>();
        int cursor = 0;
        foreach (var inline in document.Descendants<Inline>().OrderBy(x => x.Span.Start))
        {
            string? destination = inline switch
            {
                LinkInline { IsImage: false } link => link.Url,
                AutolinkInline { IsEmail: false } auto => auto.Url,
                _ => null
            };
            if (ChatMarkdown.GetWebLink(destination) is not { } uri || inline.Span.Start < cursor) continue;
            int start = inline.Span.Start, end = inline.Span.End + 1;
            if (start < 0 || end > text.Length || end <= start) continue;
            if (start > cursor) parts.Add(new(text[cursor..start]));
            var label = inline is LinkInline linkInline ? PlainText(linkInline) : destination!;
            parts.Add(new(string.IsNullOrWhiteSpace(label) ? destination! : label, uri));
            cursor = end;
        }
        if (cursor < text.Length) parts.Add(new(text[cursor..]));
        return parts;
    }

    public static string PlainText(ContainerInline inlines)
    {
        var builder = new StringBuilder();
        Append(inlines);
        return builder.ToString();
        void Append(ContainerInline container)
        {
            foreach (var child in container)
            {
                switch (child)
                {
                    case LiteralInline literal: builder.Append(literal.Content); break;
                    case CodeInline code: builder.Append(code.Content); break;
                    case HtmlEntityInline entity: builder.Append(entity.Transcoded); break;
                    case LineBreakInline: builder.Append(' '); break;
                    case AutolinkInline auto: builder.Append(auto.Url); break;
                    case ContainerInline nested: Append(nested); break;
                }
            }
        }
    }
}
