using FluentValidation;

namespace CivicTrace.Application.Features.Analytics.Queries.GetAnalytics;

public sealed class GetAnalyticsQueryValidator : AbstractValidator<GetAnalyticsQuery>
{
    public static readonly string[] GroupByValues = ["buyer", "cpv", "year", "method", "status", "funding", "expenditure"];

    public GetAnalyticsQueryValidator()
    {
        RuleFor(x => x.GroupBy)
            .Must(value => GroupByValues.Contains(value))
            .WithMessage($"GroupBy must be one of: {string.Join(", ", GroupByValues)}.");
    }
}