using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class AiModelSelectionTests
{
    private static readonly DateOnly Today = new(2026, 9, 25);

    [TestMethod]
    public void SavedAvailableModelWinsOverPriceLunaAndProviderDefault()
    {
        var models = new[] { Model("gpt-6-astra", true), Model("gpt-6-luna"), Model("gpt-5.6-sol") };
        Assert.AreSame(models[2], AiModelSelection.Select(models, models[2].Id, Today));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("retired-model")]
    [DataRow("--invalid-model")]
    public void KnownStandardCreditRatesSelectLowestCombinedInputOutputCost(string? saved)
    {
        var models = new[] { Model("gpt-6-astra", true), Model("gpt-5.6-luna"), Model("gpt-6-luna") };
        Assert.AreSame(models[2], AiModelSelection.Select(models, saved, Today));
        Assert.AreEqual(2.5m, models[2].TokenCost!.Input);
        Assert.AreEqual(12.5m, models[2].TokenCost!.Output);
        Assert.IsNull(Model("gpt-7-luna").TokenCost);
    }

    [TestMethod]
    public void CostUsesInputAndOutputTogetherAndUnknownRatesAreNotFree()
    {
        AiModel[] models = [new("unknown", "Unknown", true), Priced("input-cheap", 1, 100),
            Priced("combined-cheap", 5, 5), new("gpt-99-luna", "Luna"), Priced("same-cost", 5, 5)];
        Assert.AreSame(models[2], AiModelSelection.Select(models, null, Today));
        models[4] = Priced("free", 0, 0);
        Assert.AreSame(models[4], AiModelSelection.Select(models, null, Today));
    }

    [TestMethod]
    public void StaleFutureAndInvalidRatesFallBackToNewestLuna()
    {
        AiModel[] models = [Priced("stale", 0, 0) with { TokenCost = new(0, 0, 0, Today.AddDays(-30)) },
            Priced("future", 0, 0) with { TokenCost = new(0, 0, 0, Today.AddDays(1)) },
            Priced("invalid", -1, 0), new("gpt-5.9-luna", "Older"), new("gpt-5.10-luna", "Newer")];
        Assert.AreSame(models[4], AiModelSelection.Select(models, null, Today));
        Assert.AreSame(models[0], AiModelSelection.Select(models, null, Today.AddDays(-1)));
    }

    [TestMethod]
    [DataRow("gpt-6-luna", "gpt-5.10-luna", "gpt-6-luna")]
    [DataRow("gpt-5.9-luna", "gpt-5.10-luna", "gpt-5.10-luna")]
    [DataRow("gpt-6-luna-2026-01-01", "gpt-6-luna-2026-09-01", "gpt-6-luna-2026-09-01")]
    [DataRow("gpt-6-luna-2026-09-01", "gpt-6-luna", "gpt-6-luna")]
    public void LunaVersionsCompareNumericallyThenBySnapshot(string first, string second, string expected)
    {
        AiModel[] models = [new("provider-default", "Default", true), new(first, first), new(second, second)];
        Assert.AreEqual(expected, AiModelSelection.Select(models, null, Today).Id);
    }

    [TestMethod]
    public void WithoutUsableCostOrLunaUsesFirstModelEvenIfProviderDefaultIsLater()
    {
        AiModel[] models = [new("first", "First"), new("provider-default", "Default", true),
            new("gpt-99-lunatic", "Luna"), new("gpt-99-luna-invalid", "Invalid suffix")];
        Assert.AreSame(models[0], AiModelSelection.Select(models, null, Today));
        Assert.AreEqual(AiFailureCode.ModelListUnavailable,
            Assert.ThrowsExactly<ManagedAiException>(() => AiModelSelection.Select([], null, Today)).Code);
    }

    private static AiModel Model(string id, bool isDefault = false) => OpenAiCodexRateCard.WithCost(new(id, id, isDefault));
    private static AiModel Priced(string id, decimal input, decimal output) => new(id, id) { TokenCost = new(input, 0, output, Today) };
}
