
using CRUDAppUsingADO.DataAccess;

namespace CRUDAppUsingADO.Services;

public sealed class BulkJobBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkJobBackgroundService> _logger;

    public BulkJobBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BulkJobBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Bulk job background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var dal = scope.ServiceProvider
                    .GetRequiredService<BulkCardDAL>();

                var jobId = await dal.ClaimNextJobAsync(stoppingToken);

                if (jobId == null)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken);

                    continue;
                }

                _logger.LogInformation(
                    "Claimed bulk job {JobId}",
                    jobId.Value);

                var processor = scope.ServiceProvider
                    .GetRequiredService<BulkJobProcessor>();

                try
                {
                    await processor.ProcessAsync(
                        jobId.Value,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Bulk job {JobId} failed.",
                        jobId.Value);

                    await dal.FailJobAsync(
                        jobId.Value,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error in bulk job background service.");

                await Task.Delay(
                    TimeSpan.FromSeconds(10),
                    stoppingToken);
            }
        }

        _logger.LogInformation(
            "Bulk job background service stopped.");
    }
}
