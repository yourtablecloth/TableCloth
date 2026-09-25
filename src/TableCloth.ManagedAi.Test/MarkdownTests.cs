using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class MarkdownTests
{
    [TestMethod]
    public void ParsesBlockStructureAndInlineFormatting()
    {
        var doc = ChatMarkdown.Parse("""
            ## 제목

            **굵게** *기울임* ~~삭제~~ `코드` &amp; \*그대로\*

            > 인용문

            3. 셋째
               - 중첩 항목
            4. 넷째

            - [x] 완료
            - [ ] 대기

            | 서비스 | 링크 |
            | :--- | ---: |
            | 은행 | [방문](https://bank.example/) |

            ```text
            https://code.example/ **코드 원문**
            ```
            """);
        Assert.HasCount(1, doc.Descendants<HeadingBlock>().ToArray());
        Assert.HasCount(3, doc.Descendants<EmphasisInline>().ToArray());
        Assert.HasCount(1, doc.Descendants<CodeInline>().ToArray());
        Assert.HasCount(1, doc.Descendants<QuoteBlock>().ToArray());
        Assert.AreEqual("3", doc.Descendants<ListBlock>().First(x => x.IsOrdered).OrderedStart);
        Assert.AreEqual(1, doc.Descendants<TaskList>().Count(x => x.Checked));
        var table = doc.Descendants<Table>().Single();
        Assert.HasCount(2, table.ColumnDefinitions);
        Assert.AreEqual(TableColumnAlign.Right, table.ColumnDefinitions[1].Alignment);
        Assert.Contains("**코드 원문**", doc.Descendants<FencedCodeBlock>().Single().Lines.ToString());
    }

    [TestMethod]
    public void ReferenceLinksEscapesAndEntitiesResolveThroughParser()
    {
        var parts = ChatMessageLinks.Parse("[**공식** &amp; 안내][site]\n\n[site]: https://bank.example/a_(b)?a=1&b=2 \"제목\"");
        var link = parts.Single(x => x.Link is not null);
        Assert.AreEqual("공식 & 안내", link.Text);
        Assert.AreEqual("https://bank.example/a_(b)?a=1&b=2", link.Link!.OriginalString);
        Assert.HasCount(0, ChatMessageLinks.Parse("`https://inline-code.example/`\n\n```\nhttps://block-code.example/\n```").Where(x => x.Link is not null).ToArray());
    }

    [TestMethod]
    public void ImagesHtmlAndUnsafeDestinationsNeverBecomeActiveWebLinks()
    {
        var source = "![추적 이미지](https://tracker.example/pixel) <script>alert(1)</script> [실행](javascript:alert(1)) [로컬](http://localhost/)";
        var doc = ChatMarkdown.Parse(source);
        Assert.HasCount(0, doc.Descendants<HtmlBlock>().ToArray());
        Assert.HasCount(0, doc.Descendants<HtmlInline>().ToArray());
        Assert.HasCount(0, ChatMessageLinks.Parse(source).Where(x => x.Link is not null).ToArray());
    }

    [TestMethod]
    public void CodeAndEscapedMarkdownRemainLiteral()
    {
        var doc = ChatMarkdown.Parse("\\*\\*별표\\*\\* 및 `a  b`\n줄 바꿈  \n다음 줄");
        Assert.HasCount(0, doc.Descendants<EmphasisInline>().ToArray());
        Assert.AreEqual("a  b", doc.Descendants<CodeInline>().Single().Content);
        Assert.AreEqual(1, doc.Descendants<LineBreakInline>().Count(x => x.IsHard));
        Assert.AreEqual(1, doc.Descendants<LineBreakInline>().Count(x => !x.IsHard));
    }
}
