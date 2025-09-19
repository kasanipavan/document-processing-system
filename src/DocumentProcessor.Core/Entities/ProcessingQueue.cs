using System;

namespace DocumentProcessor.Core.Entities
{
    public class ProcessingQueue
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public ProcessingType ProcessingType { get; set; }
        public ProcessingStatus Status { get; set; }
        public int Priority { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; } = 3;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorDetails { get; set; }
        public string? ProcessorId { get; set; } // For tracking which service/instance is processing
        public string? ResultData { get; set; } // JSON result data
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
    }

    public enum ProcessingType
    {
        TextExtraction,
        Classification,
        IntentDetection,
        EntityExtraction,
        Summarization,
        CustomProcessing
    }

    public enum ProcessingStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Retrying,
        Cancelled,
        Skipped
    }
}