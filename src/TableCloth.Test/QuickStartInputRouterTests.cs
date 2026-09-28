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
    public void CatalogSearchMatchesDisplayNamesOnly()
    {
        Assert.IsTrue(QuickStartInputRouter.MatchesCatalogName(Services[0], "국민"));
        Assert.IsFalse(QuickStartInputRouter.MatchesCatalogName(Services[0], "bank.example"));
        Assert.IsFalse(QuickStartInputRouter.MatchesCatalogName(Services[0], "국민 카드"));
    }
}
