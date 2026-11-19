using CivicTrace.Domain.Enums;

namespace CivicTrace.Application.Common.Options;

public sealed class ProzorroOptions
{
    public const string SectionName = "Prozorro";

    public string BaseUrl { get; init; } = "https://public-api.prozorro.gov.ua/api/2.5/";
    public int TimeoutSeconds { get; init; } = 15;

    public bool AutoSyncOnStartup { get; init; } = true;
    public int SyncIntervalMinutes { get; init; } = 60;
    public int SyncMaxPages { get; init; } = 20;
    public int SyncTargetNewCount { get; init; } = 25;
    public string PoltavaLocalityFilter { get; init; } = "Полтав";

    public Dictionary<string, string> MethodTypeMap { get; init; } = new();

    public IReadOnlyDictionary<string, ProcurementMethod> BuildMethodTypeMap()
    {
        var map = new Dictionary<string, ProcurementMethod>(StringComparer.OrdinalIgnoreCase)
        {
            ["reporting"] = ProcurementMethod.NoElectronicSystem,
            ["negotiation"] = ProcurementMethod.NoElectronicSystem,
            ["negotiation.quick"] = ProcurementMethod.NoElectronicSystem,
            ["priceQuotation"] = ProcurementMethod.RequestForQuotations,
            ["belowThreshold"] = ProcurementMethod.RequestForQuotations,
            ["aboveThreshold"] = ProcurementMethod.OpenTendersWithSpecifics,
            ["aboveThresholdUA"] = ProcurementMethod.OpenTendersWithSpecifics,
            ["aboveThresholdEU"] = ProcurementMethod.OpenTendersWithSpecifics,
            ["aboveThresholdUA.defense"] = ProcurementMethod.OpenTendersWithSpecifics,
            ["closeFrameworkAgreementSelectionUA"] = ProcurementMethod.Other,
            ["competitiveDialogueUA"] = ProcurementMethod.Other,
            ["competitiveDialogueEU"] = ProcurementMethod.Other,
            ["esco"] = ProcurementMethod.Other
        };

        foreach (var (type, method) in MethodTypeMap)
        {
            if (Enum.TryParse<ProcurementMethod>(method, ignoreCase: true, out var parsed))
                map[type] = parsed;
        }

        return map;
    }
}