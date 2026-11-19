using CivicTrace.Application.Features.Rentals.Models;
using ErrorOr;

namespace CivicTrace.Application.Common.Interfaces;

public interface IProzorroSaleClient
{
    Task<ErrorOr<List<SaleRegistryObjectDto>>> GetRegistryObjectsByDateModifiedAsync(
        DateTimeOffset from,
        int limit = 100,
        CancellationToken ct = default);

    Task<ErrorOr<List<SaleProcedureDto>>> GetProceduresByDateModifiedAsync(
        DateTimeOffset from,
        int limit = 100,
        CancellationToken ct = default);
}