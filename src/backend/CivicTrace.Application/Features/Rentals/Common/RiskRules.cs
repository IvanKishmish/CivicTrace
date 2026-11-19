using System.Text.Json;
using CivicTrace.Domain.Entities;

namespace CivicTrace.Application.Features.Rentals.Common;

public sealed class RiskRuleWeights
{
    public int SINGLE_BIDDER { get; init; } = 25;
    public int MIN_PRICE_INCREASE { get; init; } = 20;
    public int DIRECT_OR_BEFORE_ETS { get; init; } = 20;
    public int REPEATED_FAILED_ATTEMPTS { get; init; } = 20;
    public int LOW_RATE_VS_MEDIAN { get; init; } = 15;
}

public sealed class RiskRulesConfig
{
    public decimal MinPriceIncreasePercent { get; init; } = 1.5m;
    public decimal LowMedianRateDiffPercent { get; init; } = 30.0m;
    public int LongUnrentedDays { get; init; } = 180;
    public RiskRuleWeights Weights { get; init; } = new();

    public static RiskRulesConfig Load()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "risk-rules.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "risk-rules.json"),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../data/risk-rules.json"))
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<RiskRulesConfig>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (parsed is not null) return parsed;
            }
            catch { }
        }

        return new RiskRulesConfig();
    }
}

public sealed record RiskFlagDto(string Code, string Title, int Weight, string Reason, string LegitimateExplanation);

public sealed record RiskRuleDefinitionDto(
    string Code, string Title, int Weight, string Formula, string Threshold, string WhyRisk, string LegitimateExplanation);

public static class RiskEvaluator
{
    public static IReadOnlyList<RiskRuleDefinitionDto> DescribeRules(RiskRulesConfig cfg) =>
    [
        new("SINGLE_BIDDER", "Аукціон із одним учасником", cfg.Weights.SINGLE_BIDDER,
            "bidsCount == 1 AND status == 'complete'", "1 учасник",
            "Відсутність конкуренції часто призводить до оренди за стартовою ціною.",
            "Низький попит на приміщення у поганому стані, на периферії або зі специфічним цільовим призначенням."),
        new("MIN_PRICE_INCREASE", "Мінімальне підвищення ціни", cfg.Weights.MIN_PRICE_INCREASE,
            "(finalPrice - startPrice) / startPrice * 100 < threshold", $"< {cfg.MinPriceIncreasePercent}%",
            "Номінальне перевищення стартової ставки може свідчити про узгоджену поведінку учасників.",
            "Стартова ціна вже відповідала ринковій або лише 1 учасник реально цікавився лотом."),
        new("DIRECT_OR_BEFORE_ETS", "Передача або продовження без аукціону", cfg.Weights.DIRECT_OR_BEFORE_ETS,
            "contractMethod == 'signedBeforeETS' OR listType == 'second'", "Наявність позааукціонного договору",
            "Позаконкурентна передача комунального майна потребує перевірки правових підстав.",
            "Пільгові категорії за ст. 15 Закону (школи, ДЮСШ, державні установи) або чинний договір до запуску ЕТС."),
        new("REPEATED_FAILED_ATTEMPTS", "Повторні торги / неуспішні спроби", cfg.Weights.REPEATED_FAILED_ATTEMPTS,
            "tenderAttempts >= 2 OR multiple unsuccessful auctions", ">= 2 спроб",
            "Багаторазові неуспішні торги часто передують зниженню стартової ціни на 50%.",
            "Завищена первинна оцінка або обтяжливі умови утримання об’єкта."),
        new("LOW_RATE_VS_MEDIAN", "Ставка суттєво нижча за медіану портфеля", cfg.Weights.LOW_RATE_VS_MEDIAN,
            "pricePerSqmFinal < medianPricePerSqm * (1 - threshold/100)", $"Нижче медіани на > {cfg.LowMedianRateDiffPercent}%",
            "Можливе заниження надходжень громади порівняно з аналогічним майном.",
            "Підвал, дах, аварійний стан, відсутність опалення/води або потреба капремонту.")
    ];

    public static (int Score, List<RiskFlagDto> Flags) EvaluateAuction(
        RentalAuction auction, RentalObject? obj, decimal? medianPricePerSqm, RiskRulesConfig cfg)
    {
        var flags = new List<RiskFlagDto>();

        if (auction.BidsCount == 1 && auction.Status == "complete")
            flags.Add(new RiskFlagDto("SINGLE_BIDDER", "Один учасник на переможному аукціоні", cfg.Weights.SINGLE_BIDDER,
                "Зафіксовано лише 1 пропозицію на завершеному аукціоні.",
                "Низький попит на приміщення або жорстке цільове обмеження."));

        if (auction.Status == "complete" && auction.PriceIncreasePct is { } inc && inc < cfg.MinPriceIncreasePercent)
            flags.Add(new RiskFlagDto("MIN_PRICE_INCREASE", "Мінімальне підвищення ціни", cfg.Weights.MIN_PRICE_INCREASE,
                $"Приріст ціни {inc:0.##}% (поріг < {cfg.MinPriceIncreasePercent}%).",
                "Стартова оцінка вже була наближена до граничної готовності орендарів."));

        if (obj?.CurrentContractMethod == "signedBeforeETS" || obj?.ListType == "second")
            flags.Add(new RiskFlagDto("DIRECT_OR_BEFORE_ETS", "Договір без електронного аукціону / 2-й тип", cfg.Weights.DIRECT_OR_BEFORE_ETS,
                "Договір укладено до ЕТС або об’єкт у переліку 2-го типу.",
                "Пільгові орендарі (освіта, спорт, держустанови) або перехідний договір за законом."));

        if (auction.TenderAttempts is >= 2)
            flags.Add(new RiskFlagDto("REPEATED_FAILED_ATTEMPTS", "Повторний аукціон після попередніх спроб", cfg.Weights.REPEATED_FAILED_ATTEMPTS,
                $"Аукціон оголошено на спробі №{auction.TenderAttempts}.",
                "Попередня стартова ставка була завищеною відносно стану об’єкта."));

        if (medianPricePerSqm is > 0 && auction.PricePerSqmFinal is { } rate)
        {
            var threshold = medianPricePerSqm.Value * (1m - cfg.LowMedianRateDiffPercent / 100m);
            if (rate < threshold)
                flags.Add(new RiskFlagDto("LOW_RATE_VS_MEDIAN", "Ставка нижча за медіану портфеля", cfg.Weights.LOW_RATE_VS_MEDIAN,
                    $"{rate:0.##} грн/м² проти медіани {medianPricePerSqm.Value:0.##} грн/м².",
                    "Підвальне приміщення, дах або необхідність значного ремонту."));
        }

        return (flags.Sum(f => f.Weight), flags);
    }

    public static List<RiskFlagDto> EvaluateObject(RentalObject obj, decimal? medianPricePerSqm, RiskRulesConfig cfg)
    {
        var all = new Dictionary<string, RiskFlagDto>(StringComparer.Ordinal);

        if (obj.CurrentContractMethod == "signedBeforeETS" || obj.ListType == "second")
        {
            all["DIRECT_OR_BEFORE_ETS"] = new RiskFlagDto("DIRECT_OR_BEFORE_ETS",
                "Договір без аукціону / 2-й тип переліку", cfg.Weights.DIRECT_OR_BEFORE_ETS,
                obj.CurrentContractMethod == "signedBeforeETS"
                    ? "У картці зафіксовано чинний договір, укладений без аукціону (signedBeforeETS)."
                    : "Об’єкт віднесено до переліку 2-го типу.",
                "Законне надання в оренду бюджетним/соціальним установам або договір до старту ЕТС.");
        }

        if (medianPricePerSqm is > 0 && obj.AreaSqm is > 0 && obj.CurrentContractValue is > 10m)
        {
            var rate = obj.CurrentContractValue.Value / obj.AreaSqm.Value;
            if (rate < medianPricePerSqm.Value * (1m - cfg.LowMedianRateDiffPercent / 100m))
                all["LOW_RATE_VS_MEDIAN"] = new RiskFlagDto("LOW_RATE_VS_MEDIAN",
                    "Ставка за договором нижча за медіану", cfg.Weights.LOW_RATE_VS_MEDIAN,
                    $"Розрахункова ставка {rate:0.##} грн/м² нижча за медіану {medianPricePerSqm.Value:0.##} грн/м².",
                    "Можливі пільгова ставка (1 грн/рік для комунальних закладів) або незадовільний стан.");
        }

        foreach (var auction in obj.Auctions)
        {
            if (string.IsNullOrWhiteSpace(auction.RiskFlagsJson) || auction.RiskFlagsJson == "[]") continue;
            try
            {
                var flags = JsonSerializer.Deserialize<List<RiskFlagDto>>(auction.RiskFlagsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (flags is null) continue;
                foreach (var f in flags) all.TryAdd(f.Code, f);
            }
            catch { }
        }

        return all.Values.ToList();
    }
}