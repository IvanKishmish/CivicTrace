using CivicTrace.Application.Features.Rentals.Commands.SyncRentals;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CivicTrace.Persistence.Prozorro;

public sealed class RentalSyncHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<RentalSyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(4), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var res = await mediator.Send(new SyncRentalsCommand(MaxBatchesPerSlice: 25), stoppingToken);

                if (res.IsError)
                    logger.LogWarning("Background rental sync returned error: {Error}", res.FirstError.Description);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Background rental sync failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}