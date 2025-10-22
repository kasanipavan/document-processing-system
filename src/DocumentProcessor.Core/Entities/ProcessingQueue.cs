using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessor.Core.Entities
{
    [Table("processingqueues", Schema = "documentprocessor_dbo")]
    public class ProcessingQueue
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        [Column("documentid")]
        public Guid DocumentId { get; set; }
        public Document Document { get; set; } = null!;
        public ProcessingType ProcessingType { get; set; }
        public ProcessingStatus Status { get; set; }
        [Column("priority")]
        public int Priority { get; set; }
        [Column("retrycount")]
        public int RetryCount { get; set; }
        [Column("maxretries")]
        public int MaxRetries { get; set; } = 3;
        [Column("startedat")]
        public DateTime? StartedAt { get; set; }
        [Column("completedat")]
        public DateTime? CompletedAt { get; set; }
        [Column("errormessage")]
        public string? ErrorMessage { get; set; }
        [Column("errordetails")]
        public string? ErrorDetails { get; set; }
        [Column("processorid")]
        public string? ProcessorId { get; set; } // For tracking which service/instance is processing
        [Column("resultdata")]
        public string? ResultData { get; set; } // JSON result data
        [Column("createdat")]
        public DateTime CreatedAt { get; set; }
        [Column("updatedat")]
        public DateTime UpdatedAt { get; set; }
        [Column("nextretryat")]
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