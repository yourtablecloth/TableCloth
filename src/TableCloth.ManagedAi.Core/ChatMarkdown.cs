using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TableCloth.ManagedAi;

// A shared CommonMark/GFM syntax tree drives both link discovery and native UI rendering.
// No HTML renderer, external image loader or provider-defined extension is involved.
public static class ChatMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder { MaximumNestingDepth = 32 }
        .DisableHtml()
        .UsePreciseSourceLocation()
        .UseCjkFriendlyEmphasis()
        .UseAutoLinks()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UsePipeTables(new PipeTableOptions { UseGfmRules = true })
        .Build();

    public static MarkdownDocument Parse(string text)
    {
        if (text.Length > 65536) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
        MarkdownDocument document;
        try { document = Markdown.Parse(text, Pipeline); }
        catch (ArgumentException) { throw new ManagedAiException(AiFailureCode.OutputLimitExceeded); }
        int count = 0;
        Check(document, 0);
        return document;

        void Check(MarkdownObject node, int depth)
        {
            if (++count > 10000 || depth > 64) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
            if (node is ContainerBlock blocks) foreach (var child in blocks) Check(child, depth + 1);
            if (node is LeafBlock { Inline: { } inline }) Check(inline, depth + 1);
            if (node is ContainerInline inlines) foreach (var child in inlines) Check(child, depth + 1);
        }
    }

    public static Uri? GetWebLink(string? destination)
        => PublicWebUrl.TryParse(destination, out var link) ? link : null;
}
