using TableCloth.Models.Catalog;

namespace TableCloth.Test;

[TestClass]
public sealed class QuickStartSuggestionSearchTests
{
    private static readonly CatalogInternetService[] Services =
    [
        new() { DisplayName = "국민은행", Url = "https://bank.example/" },
        new() { DisplayName = "국민 카드", Url = "https://card.example/" },
        new() { DisplayName = "다른 은행", Url = "https://other.example/" },
        new() { DisplayName = "웹 은행", Url = "https://www.webbank.example/" },
    ];

    [TestMethod]
    public void NameAndAddressSuggestionsUseTheCorrectCompletion()
    {
        var names = QuickStartSuggestionSearch.Find("국민", Services);
        Assert.HasCount(2, names);
        Assert.IsTrue(names.All(item => item.Kind == QuickStartSuggestionKind.CatalogName));
        Assert.AreEqual("국민은행", names.Single(item => item.DisplayName == "국민은행").Completion);

        var addresses = QuickStartSuggestionSearch.Find("https://bank.", Services);
        Assert.HasCount(1, addresses);
        Assert.AreEqual(QuickStartSuggestionKind.WebAddress, addresses[0].Kind);
        Assert.AreEqual("https://bank.example/", addresses[0].Completion);
        Assert.HasCount(1, QuickStartSuggestionSearch.Find("bank.example/path", Services));
        Assert.AreEqual("https://www.webbank.example/",
            QuickStartSuggestionSearch.Find("webbank.example", Services).Single().Completion);
    }

    [TestMethod]
    public void HidesSuggestionsForCommandsQuestionsAndEmptyInputs()
    {
        Assert.HasCount(0, QuickStartSuggestionSearch.Find("", Services));
        Assert.HasCount(0, QuickStartSuggestionSearch.Find("#debug", Services));
        Assert.HasCount(0, QuickStartSuggestionSearch.Find("국민은행 이용 방법 알려줘", Services));
        Assert.HasCount(1, QuickStartSuggestionSearch.Find("국민", Services, 1));
    }
}
