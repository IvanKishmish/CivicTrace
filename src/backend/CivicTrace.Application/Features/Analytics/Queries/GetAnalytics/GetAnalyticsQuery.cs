using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Analytics.Queries.GetAnalytics;

/// <summary>GroupBy: buyer | cpv | year | method | status | funding | expenditure.</summary>
public sealed record GetAnalyticsQuery(string GroupBy = "buyer") : IQuery<ErrorOr<AnalyticsResponse>>;