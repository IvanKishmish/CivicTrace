using CivicTrace.Application.Features.Prozorro.Models;
using ErrorOr;

namespace CivicTrace.Application.Common.Interfaces;

public interface IProzorroClient
{
    /// <summary>Читає одну закупівлю з публічного API Prozorro за внутрішнім ідентифікатором (32 hex-символи).</summary>
    Task<ErrorOr<ProzorroTender>> GetTenderAsync(string apiId, CancellationToken ct = default);

    /// <summary>Читає один план закупівлі з публічного API Prozorro за його внутрішнім ідентифікатором.</summary>
    Task<ErrorOr<ProzorroPlan>> GetPlanAsync(string planApiId, CancellationToken ct = default);
}