using CivicTrace.Application.Common.Options;
using CivicTrace.Application.Features.Prozorro.Commands.SyncPoltava;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CivicTrace.Persistence.Prozorro;

public sealed class ProzorroSyncHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ProzorroOptions> options,
    ILogger<ProzorroSyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.AutoSyncOnStartup)
            return;

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(5, options.Value.SyncIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new SyncPoltavaCommand(), stoppingToken);

                if (result.IsError)
                    logger.LogWarning("Background Prozorro sync returned error: {Error}", result.FirstError.Description);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Background Prozorro sync failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}