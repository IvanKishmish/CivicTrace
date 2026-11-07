namespace CivicTrace.Application.Features.Analytics.Queries.GetAnalytics;

/// <summary>
/// Суми розділено за позначкою ПДВ: додавати суми «з ПДВ» і «без ПДВ» в одне число не можна,
/// а для частини записів позначки в джерелі немає (VatUnspecified).
/// </summary>
public sealed record AmountTotals(decimal WithVat, decimal WithoutVat, decimal VatUnspecified);

public sealed record AnalyticsGroup(
    string Key,
    string Label,
    int ProcurementsCount,
    int ContractSignedCount,
    int NotHeldCount,
    int NoElectronicSystemCount,
    int BidsListCompleteCount,
    double? AverageBidsInCompleteLists,
    AmountTotals ExpectedAmount,
    AmountTotals ContractAmount,
    int RecordsWithMissingData,
    double AverageMissingDataItems,
    int RecordsNeedingCheck);

public sealed record AnalyticsResponse(
    string GroupBy,
    AnalyticsGroup Summary,
    IReadOnlyList<AnalyticsGroup> Groups,
    IReadOnlyList<string> Notes);