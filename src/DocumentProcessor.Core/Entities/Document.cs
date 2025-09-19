using System;
using System.Collections.Generic;

namespace DocumentProcessor.Core.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public string? S3Key { get; set; }
        public string? S3Bucket { get; set; }
        public DocumentSource Source { get; set; }
        public DocumentStatus Status { get; set; }
        public Guid? DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }
        public string? ExtractedText { get; set; }
        public string? Summary { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        
        // Navigation properties
        public ICollection<Classification> Classifications { get; set; } = new List<Classification>();
        public ICollection<ProcessingQueue> ProcessingQueueItems { get; set; } = new List<ProcessingQueue>();
        public DocumentMetadata? Metadata { get; set; }
    }

    public enum DocumentSource
    {
        LocalUpload,
        S3,
        FileShare
    }

    public enum DocumentStatus
    {
        Pending,
        Queued,
        Processing,
        Processed,
        Failed
    }
}