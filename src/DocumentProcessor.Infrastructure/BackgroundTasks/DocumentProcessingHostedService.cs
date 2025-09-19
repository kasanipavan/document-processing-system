using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.BackgroundTasks;

public class DocumentProcessingHostedService(
    IBackgroundTaskQueue taskQueue,
    ILogger<DocumentProcessingHostedService> logger,
    int maxConcurrency = 3)
    : BackgroundService
{
    private readonly SemaphoreSlim _semaphore = new(maxConcurrency, maxConcurrency);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Document Processing Hosted Service is starting with max concurrency: {MaxConcurrency}", 
            maxConcurrency);

        var tasks = new Task[maxConcurrency];
            
        for (int i = 0; i < maxConcurrency; i++)
        {
            tasks[i] = ProcessTasksAsync(i, stoppingToken);
        }

        await Task.WhenAll(tasks);

        logger.LogInformation("Document Processing Hosted Service is stopping.");
    }

    private async Task ProcessTasksAsync(int workerId, CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker {WorkerId} started", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _semaphore.WaitAsync(stoppingToken);
                    
                try
                {
                    var workItem = await taskQueue.DequeueAsync(stoppingToken);

                    if (workItem != null)
                    {
                        logger.LogInformation("Worker {WorkerId} processing task", workerId);
                            
                        try
                        {
                            await workItem(stoppingToken);
                            logger.LogInformation("Worker {WorkerId} completed task", workerId);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            // Expected when cancellation is requested
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Worker {WorkerId} encountered error processing task", 
                                workerId);
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker {WorkerId} encountered unexpected error", workerId);
                await Task.Delay(1000, stoppingToken); // Brief delay before retry
            }
        }

        logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Document Processing Hosted Service is stopping.");
        await base.StopAsync(cancellationToken);
    }
}