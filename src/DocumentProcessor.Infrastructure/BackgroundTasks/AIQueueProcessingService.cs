using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentProcessor.Infrastructure.BackgroundTasks;

/// <summary>
/// Background service that processes items from the AI processing queue (database-backed)
/// </summary>
public class AIQueueProcessingService(
    IAIProcessingQueue processingQueue,
    IServiceProvider serviceProvider,
    ILogger<AIQueueProcessingService> logger)
    : BackgroundService
{
    private readonly int _pollingIntervalSeconds = 30; // Poll every 30 seconds for better performance

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AI Queue Processing Service is starting.");

        // Wait a moment for all services to initialize
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        logger.LogInformation("AI Queue Processing Service initialized and ready to process.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug("Checking for items in queue...");
                    
                // Try to dequeue an item from the database queue
                var queueItem = await processingQueue.DequeueNextAsync();
                    
                if (queueItem != null)
                {
                    logger.LogInformation("Dequeued document {DocumentId} from queue {QueueId}", queueItem.DocumentId, queueItem.QueueId);
                        
                    // Process the document
                    await ProcessDocumentAsync(queueItem, stoppingToken);
                }
                else
                {
                    logger.LogDebug("No items found in queue, waiting...");
                    // No items in queue, wait before checking again
                    await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AI Queue Processing Service");
                    
                // Wait a bit before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        logger.LogInformation("AI Queue Processing Service is stopping.");
    }

    private async Task ProcessDocumentAsync(ProcessingQueueItem queueItem, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
            
        try
        {
            logger.LogInformation("Starting processing for document {DocumentId}", queueItem.DocumentId);
                
            // Get the document processing service dynamically
            // This avoids circular dependency between Infrastructure and Application layers
            var processingServiceType = Type.GetType("DocumentProcessor.Application.Services.IDocumentProcessingService, DocumentProcessor.Application");
            if (processingServiceType == null)
            {
                logger.LogError("Could not load IDocumentProcessingService type");
                return;
            }
                
            var processingService = scope.ServiceProvider.GetService(processingServiceType);
            if (processingService == null)
            {
                logger.LogError("IDocumentProcessingService not found in service provider");
                return;
            }
                
            // Process the document using reflection
            var processMethod = processingServiceType.GetMethod("ProcessDocumentAsync");
            if (processMethod == null)
            {
                logger.LogError("ProcessDocumentAsync method not found");
                return;
            }
                
            var resultTask = processMethod.Invoke(processingService, [queueItem.DocumentId, null]) as Task;
            if (resultTask == null)
            {
                logger.LogError("Failed to invoke ProcessDocumentAsync");
                return;
            }
                
            await resultTask;
                
            // Get the result using reflection
            var resultProperty = resultTask.GetType().GetProperty("Result");
            dynamic result = resultProperty?.GetValue(resultTask);
                
            if (result == null)
            {
                logger.LogError("Failed to get processing result");
                return;
            }
                
            bool success = (bool)result.Success;
                
            if (success)
            {
                logger.LogInformation("Successfully processed document {DocumentId}", queueItem.DocumentId);
                    
                // Update queue status to completed
                if (processingQueue is AI.DatabaseProcessingQueue dbQueue)
                {
                    var resultData = new
                    {
                        Classification = result.Classification,
                        Extraction = result.Extraction,
                        Summary = result.Summary,
                        Intent = result.Intent
                    };
                    var resultJson = JsonSerializer.Serialize(resultData);
                    await dbQueue.CompleteProcessingAsync(queueItem.QueueId, resultJson);
                }
            }
            else
            {
                string errorMessage = (string)(result.ErrorMessage ?? "Processing failed");
                logger.LogError("Failed to process document {DocumentId}: {ErrorMessage}", 
                    queueItem.DocumentId, errorMessage);
                    
                // Update queue status to failed
                if (processingQueue is AI.DatabaseProcessingQueue dbQueue)
                {
                    await dbQueue.FailProcessingAsync(queueItem.QueueId, 
                        errorMessage, 
                        null);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document {DocumentId}", queueItem.DocumentId);
                
            // Update queue status to failed or retry
            if (processingQueue is AI.DatabaseProcessingQueue dbQueue)
            {
                await dbQueue.FailProcessingAsync(queueItem.QueueId,
                    ex.Message,
                    ex.ToString());
            }
                
            // Don't re-throw the exception to prevent the service from crashing
            // The item has been marked as failed and can be retried later
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("AI Queue Processing Service is stopping.");
        await base.StopAsync(cancellationToken);
    }
}