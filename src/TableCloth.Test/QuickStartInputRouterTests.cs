using TableCloth.Models.Catalog;

namespace TableCloth.Test;

[TestClass]
public sealed class QuickStartInputRouterTests
{
    private static readonly CatalogInternetService[] Services =
    [
        new() { DisplayName = "국민은행", Url = "https://bank.example/" },
        new() { DisplayName = "국민 카드", Url = "https://card.example/" },
    ];

    [TestMethod]
    public void ChoosesTheExpectedDestinationInPriorityOrder()
    {
        Assert.AreEqual(QuickStartRouteKind.EmptySandbox, QuickStartInputRouter.Classify("  ", Services).Kind);
        Assert.AreEqual(QuickStartRouteKind.SpecialCommand, QuickStartInputRouter.Classify("#debug", Services).Kind);
        Assert.AreEqual(QuickStartRouteKind.WebAddress, QuickStartInputRouter.Classify("https://bank.example/", Services).Kind);
        Assert.AreEqual("https://bank.example/path", QuickStartInputRouter.Classify("bank.example/path", Services).Value);
        Assert.AreEqual(QuickStartRouteKind.WebAddress, QuickStartInputRouter.Classify("https://localhost/", Services).Kind);
        Assert.AreEqual(QuickStartRouteKind.CatalogSearch, QuickStartInputRouter.Classify("국민", Services).Kind);
        Assert.AreEqual(QuickStartRouteKind.CatalogSearch, QuickStartInputRouter.Classify("국민 카드", Services).Kind);
        Assert.AreEqual(QuickStartRouteKind.AiQuestion, QuickStartInputRouter.Classify("없는 은행", Services).Kind);
        Assert.AreEqual(QuickStartRouteKind.AiQuestion, QuickStartInputRouter.Classify("국민 은행 이용 방법", Services).Kind);
    }

    [TestMethod]
    [DataRow("이 페이지 https://example.com/ 내용을 요약해 주세요.", QuickStartRouteKind.AiQuestion)]
    [DataRow("https://example.com/ 페이지 내용을 요약해 주세요.", QuickStartRouteKind.AiQuestion)]
    [DataRow("Compare https://example.com/ and http://example.net/", QuickStartRouteKind.AiQuestion)]
    [DataRow("https://example.com/\n설명해 주세요.", QuickStartRouteKind.AiQuestion)]
    [DataRow("이 주소 https://localhost/ 를 설명해 주세요.", QuickStartRouteKind.AiQuestion)]
    [DataRow("https://example.com/", QuickStartRouteKind.WebAddress)]
    [DataRow("http://example.com/", QuickStartRouteKind.WebAddress)]
    [DataRow("https://", QuickStartRouteKind.WebAddress)]
    [DataRow("https://[invalid", QuickStartRouteKind.WebAddress)]
    [DataRow("ftp://example.com/", QuickStartRouteKind.WebAddress)]
    [DataRow("https://localhost/", QuickStartRouteKind.WebAddress)]
    [DataRow("https://192.168.1.2/", QuickStartRouteKind.WebAddress)]
    [DataRow("https://user:synthetic@example.com/", QuickStartRouteKind.WebAddress)]
    public void SeparatesUrlQuestionsFromStandaloneAddressAttempts(string input, QuickStartRouteKind expectedKind)
    {
        var route = QuickStartInputRouter.Classify(input, Services);

        Assert.AreEqual(expectedKind, route.Kind);
        Assert.AreEqual(input, route.Value);
    }

    [TestMethod]
    public void CatalogSearchMatchesDisplayNamesOnly()
    {
        Assert.IsTrue(QuickStartInputRouter.MatchesCatalogName(Services[0], "국민"));
        Assert.IsFalse(QuickStartInputRouter.MatchesCatalogName(Services[0], "bank.example"));
        Assert.IsFalse(QuickStartInputRouter.MatchesCatalogName(Services[0], "국민 카드"));
    }
}
