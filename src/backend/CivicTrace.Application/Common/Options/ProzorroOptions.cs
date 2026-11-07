using CivicTrace.Domain.Enums;

namespace CivicTrace.Application.Common.Options;

public sealed class ProzorroOptions
{
    public const string SectionName = "Prozorro";

    public string BaseUrl { get; init; } = "https://public-api.prozorro.gov.ua/api/2.5/";

    public int TimeoutSeconds { get; init; } = 15;

    /// <summary>
    /// Додаткові відповідності procurementMethodType (з API) -> ProcurementMethod. Ключ — тип процедури з API,
    /// значення — назва члена ProcurementMethod (NoElectronicSystem, RequestForQuotations, OpenTendersWithSpecifics).
    /// </summary>
    public Dictionary<string, string> MethodTypeMap { get; init; } = new();

    public IReadOnlyDictionary<string, ProcurementMethod> BuildMethodTypeMap()
    {
        var map = new Dictionary<string, ProcurementMethod>(StringComparer.OrdinalIgnoreCase)
        {
            // Підтверджено документацією Prozorro API
            ["reporting"] = ProcurementMethod.NoElectronicSystem,
            ["aboveThresholdUA.defense"] = ProcurementMethod.OpenTendersWithSpecifics
        };

        foreach (var (type, method) in MethodTypeMap)
        {
            if (Enum.TryParse<ProcurementMethod>(method, ignoreCase: true, out var parsed))
                map[type] = parsed;
        }

        return map;
    }
}