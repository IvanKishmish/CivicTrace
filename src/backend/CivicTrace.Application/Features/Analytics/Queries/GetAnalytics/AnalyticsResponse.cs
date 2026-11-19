namespace CivicTrace.Application.Features.Analytics.Queries.GetAnalytics;

public sealed record AmountTotals(decimal WithVat, decimal WithoutVat, decimal VatUnspecified);

public sealed record AnalyticsGroup(
    string Key,
    string Label,
    int ProcurementsCount,
    int ContractSignedCount,
    int NotHeldCount,
    int ActiveCount,
    int CancelledCount,
    int NoElectronicSystemCount,
    int BidsListCompleteCount,
    double? AverageBidsInCompleteLists,
    AmountTotals ExpectedAmount,
    AmountTotals ContractAmount,
    int RecordsWithMissingData,
    double AverageMissingDataItems,
    int RecordsNeedingCheck,
    decimal? FundingAmount);

public sealed record AnalyticsResponse(
    string GroupBy,
    AnalyticsGroup Summary,
    IReadOnlyList<AnalyticsGroup> Groups,
    IReadOnlyList<string> Notes);