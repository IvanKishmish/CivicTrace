using System.Collections.Concurrent;
using System.Threading.Channels;
using CivicTrace.Application.Features.Rentals.Commands.SyncRentals;
using Mediator;

namespace CivicTrace.WebApi.Services;

public sealed record RentalSyncJobSnapshot(Guid JobId, string Status, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, string? Message, SyncRentalsResponse? Result);

public sealed class RentalSyncJobService(IServiceScopeFactory scopeFactory, ILogger<RentalSyncJobService> logger) : BackgroundService
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly ConcurrentDictionary<Guid, RentalSyncJobSnapshot> _jobs = new();
    private readonly object _startLock = new();
    private Guid? _activeJobId;

    public RentalSyncJobSnapshot Start()
    {
        lock (_startLock)
        {
            if (_activeJobId is { } activeId && _jobs.TryGetValue(activeId, out var active)) return active;
            var job = new RentalSyncJobSnapshot(Guid.NewGuid(), "queued", DateTimeOffset.UtcNow, null, null, "Синхронізацію додано в чергу.", null);
            _jobs[job.JobId] = job;
            _activeJobId = job.JobId;
            _queue.Writer.TryWrite(job.JobId);
            return job;
        }
    }

    public bool TryGet(Guid jobId, out RentalSyncJobSnapshot snapshot) => _jobs.TryGetValue(jobId, out snapshot!);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!_jobs.TryGetValue(jobId, out var job)) continue;
            var started = job with { Status = "running", StartedAt = DateTimeOffset.UtcNow, Message = "Завантажуємо дані Prozorro.Продажі та оновлюємо записи." };
            _jobs[jobId] = started;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var response = await mediator.Send(new SyncRentalsCommand(MaxBatchesPerSlice: 60), stoppingToken);
                var finishedAt = DateTimeOffset.UtcNow;
                _jobs[jobId] = response.IsError
                    ? started with { Status = "failed", FinishedAt = finishedAt, Message = response.FirstError.Description }
                    : started with { Status = "completed", FinishedAt = finishedAt, Message = "Синхронізацію завершено.", Result = response.Value };
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _jobs[jobId] = started with { Status = "failed", FinishedAt = DateTimeOffset.UtcNow, Message = "Синхронізацію перервано під час зупинки сервера." };
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Manual rental sync job {JobId} failed", jobId);
                _jobs[jobId] = started with { Status = "failed", FinishedAt = DateTimeOffset.UtcNow, Message = "Синхронізація завершилася помилкою. Перевірте журнал сервера." };
            }
            finally
            {
                lock (_startLock) if (_activeJobId == jobId) _activeJobId = null;
                foreach (var old in _jobs.Values.Where(x => x.Status is "completed" or "failed").OrderByDescending(x => x.FinishedAt).Skip(20))
                    _jobs.TryRemove(old.JobId, out _);
            }
        }
    }
}
