namespace TableCloth.ManagedAi.OpenAi;

// model/list has no price fields. Keep a dated, independently reviewable official rate card.
// Never infer availability from these entries; enrich only models returned for the current account.
public static class OpenAiCodexRateCard
{
    public const string Source = "https://learn.chatgpt.com/docs/pricing";
    public static readonly DateOnly VerifiedOn = new(2026, 9, 25);

    public static AiModel WithCost(AiModel model) => model with { TokenCost = model.Id switch
    {
        "gpt-6-astra" => Cost(250m, 25m, 1250m),
        "gpt-6-sol" => Cost(50m, 5m, 250m),
        "gpt-6-luna" => Cost(2.5m, .25m, 12.5m),
        "gpt-5.6-sol" => Cost(100m, 10m, 500m),
        "gpt-5.6-terra" => Cost(50m, 5m, 300m),
        "gpt-5.6-luna" => Cost(5m, .5m, 30m),
        "gpt-5.5" => Cost(125m, 12.5m, 750m),
        "gpt-5.4" => Cost(62.5m, 6.25m, 375m),
        "gpt-5.4-mini" => Cost(18.75m, 1.875m, 113m),
        _ => null
    } };

    private static AiModelTokenCost Cost(decimal input, decimal cachedInput, decimal output)
        => new(input, cachedInput, output, VerifiedOn);
}
