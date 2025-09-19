using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Application.Services;

public class BackgroundDocumentProcessingService(
    IBackgroundTaskQueue taskQueue,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<BackgroundDocumentProcessingService> logger)
    : IBackgroundDocumentProcessingService
{
    public async Task<string> QueueDocumentForProcessingAsync(
        Guid documentId, 
        Stream documentStream,
        int priority = 0)
    {
        var taskId = $"doc-{documentId}-{Guid.NewGuid()}";
            
        // Copy stream to memory for background processing
        var streamCopy = new MemoryStream();
        await documentStream.CopyToAsync(streamCopy);
        streamCopy.Position = 0;
            
        await taskQueue.QueueBackgroundWorkItemAsync(
            async (cancellationToken) =>
            {
                await ProcessDocumentAsync(documentId, streamCopy, cancellationToken);
            },
            taskId,
            priority);
            
        logger.LogInformation("Queued document {DocumentId} for processing with task ID {TaskId}", 
            documentId, taskId);
            
        return taskId;
    }

    private async ValueTask ProcessDocumentAsync(
        Guid documentId, 
        Stream documentStream,
        CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
            
        try
        {
            logger.LogInformation("Starting background processing for document {DocumentId}", documentId);
                
            // Get required services from the scope
            var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
                
            // Load the document
            var document = await documentRepository.GetByIdAsync(documentId);
            if (document == null)
            {
                logger.LogWarning("Document {DocumentId} not found for processing", documentId);
                return;
            }
                
            // Process the document using the correct method signature
            var result = await processingService.ProcessDocumentAsync(documentId);
                
            if (result.Success)
            {
                logger.LogInformation("Successfully processed document {DocumentId}", documentId);
                    
                // Update document status
                document.Status = DocumentStatus.Processed;
                document.ProcessedAt = DateTime.UtcNow;
                await documentRepository.UpdateAsync(document);
            }
            else
            {
                logger.LogError("Failed to process document {DocumentId}: {Error}",
                    documentId, result.ErrorMessage);
                    
                // Update document status
                document.Status = DocumentStatus.Failed;
                await documentRepository.UpdateAsync(document);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document {DocumentId} in background", documentId);
                
            using var innerScope = serviceScopeFactory.CreateScope();
            var documentRepository = innerScope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                
            // Update document status to failed
            var document = await documentRepository.GetByIdAsync(documentId);
            if (document == null) throw;
            document.Status = DocumentStatus.Failed;
            await documentRepository.UpdateAsync(document);

            throw;
        }
        finally
        {
            documentStream?.Dispose();
        }
    }

    public Task<BackgroundTaskStatus?> GetProcessingStatusAsync(string taskId)
    {
        if (taskQueue.TryGetStatus(taskId, out var status))
        {
            return Task.FromResult<BackgroundTaskStatus?>(status);
        }
            
        return Task.FromResult<BackgroundTaskStatus?>(null);
    }

    public Task<int> GetQueueLengthAsync()
    {
        return Task.FromResult(taskQueue.Count);
    }

    public async Task CleanupStuckDocumentsAsync(int timeoutMinutes = 30)
    {
        using var scope = serviceScopeFactory.CreateScope();
        
        try
        {
            logger.LogInformation("Starting cleanup of stuck documents (timeout: {TimeoutMinutes} minutes)", timeoutMinutes);
            
            var processingQueue = scope.ServiceProvider.GetRequiredService<IAIProcessingQueue>();
            if (processingQueue is DocumentProcessor.Infrastructure.AI.DatabaseProcessingQueue dbQueue)
            {
                await dbQueue.CleanupStuckDocumentsAsync(TimeSpan.FromMinutes(timeoutMinutes));
            }
            
            var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-timeoutMinutes);
            
            // Find documents that are still in processing status but have been there too long
            var stuckDocuments = await documentRepository.GetByStatusAsync(DocumentStatus.Processing);
            var actuallyStuck = stuckDocuments.Where(d => d.UpdatedAt < cutoffTime).ToList();
            
            foreach (var document in actuallyStuck)
            {
                logger.LogWarning("Marking stuck document {DocumentId} ({FileName}) as failed - stuck since {UpdatedAt}", 
                    document.Id, document.FileName, document.UpdatedAt);
                
                document.Status = DocumentStatus.Failed;
                document.ProcessedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;
                await documentRepository.UpdateAsync(document);
            }
            
            if (actuallyStuck.Any())
            {
                logger.LogInformation("Cleaned up {Count} stuck documents", actuallyStuck.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during stuck document cleanup");
        }
    }
}