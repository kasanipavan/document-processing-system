using DocumentProcessor.Core.Interfaces;

namespace DocumentProcessor.Application.Services;

public interface IBackgroundDocumentProcessingService
{
    Task<string> QueueDocumentForProcessingAsync(
        Guid documentId, 
        Stream documentStream,
        int priority = 0);
        
    Task<BackgroundTaskStatus?> GetProcessingStatusAsync(string taskId);
        
    Task<int> GetQueueLengthAsync();
    
    Task CleanupStuckDocumentsAsync(int timeoutMinutes = 30);
}