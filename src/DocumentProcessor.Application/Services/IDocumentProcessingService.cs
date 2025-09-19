using DocumentProcessor.Core.Interfaces;

namespace DocumentProcessor.Application.Services;

public interface IDocumentProcessingService
{
    Task<Guid> QueueDocumentForProcessingAsync(Guid documentId, ProcessingPriority priority = ProcessingPriority.Normal);
    Task<DocumentProcessingResult> ProcessDocumentAsync(Guid documentId, AIProviderType? providerType = null);
    Task<ProcessingQueueStatus> GetProcessingStatusAsync(Guid queueId);
    Task<IEnumerable<ProcessingQueueItem>> GetPendingDocumentsAsync();
    Task CancelProcessingAsync(Guid queueId);
    Task<ProcessingCost> EstimateProcessingCostAsync(Guid documentId, AIProviderType? providerType = null);
}