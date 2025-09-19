namespace DocumentProcessor.Core.Interfaces
{
    public interface IAIProcessorFactory
    {
        IAIProcessor CreateProcessor(AIProviderType providerType);
        IAIProcessor GetDefaultProcessor();
        IEnumerable<AIProviderType> GetAvailableProviders();
    }

    public enum AIProviderType
    {
        AmazonBedrock,
        OpenAI,
        AzureOpenAI,
        GoogleVertexAI
    }

    public interface IAIProcessingQueue
    {
        Task<Guid> EnqueueDocumentAsync(Guid documentId, ProcessingPriority priority = ProcessingPriority.Normal);
        Task<ProcessingQueueStatus> GetQueueStatusAsync(Guid queueId);
        Task CancelProcessingAsync(Guid queueId);
        Task<IEnumerable<ProcessingQueueItem>> GetPendingItemsAsync();
        Task<ProcessingQueueItem?> DequeueNextAsync();
    }

    public enum ProcessingPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public class ProcessingQueueStatus
    {
        public Guid QueueId { get; set; }
        public Guid DocumentId { get; set; }
        public ProcessingState State { get; set; }
        public ProcessingPriority Priority { get; set; }
        public DateTime EnqueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public enum ProcessingState
    {
        Queued,
        Processing,
        Completed,
        Failed,
        Cancelled,
        Retrying
    }

    public class ProcessingQueueItem
    {
        public Guid QueueId { get; set; }
        public Guid DocumentId { get; set; }
        public ProcessingPriority Priority { get; set; }
        public DateTime EnqueuedAt { get; set; }
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
    }
}