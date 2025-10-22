using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessor.Core.Entities
{
    [Table("documents", Schema = "documentprocessor_dbo")]
    public class Document
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        [Column("filename")]
        public string FileName { get; set; } = string.Empty;
        [Column("originalfilename")]
        public string OriginalFileName { get; set; } = string.Empty;
        [Column("fileextension")]
        public string FileExtension { get; set; } = string.Empty;
        [Column("filesize")]
        public long FileSize { get; set; }
        [Column("contenttype")]
        public string ContentType { get; set; } = string.Empty;
        [Column("storagepath")]
        public string StoragePath { get; set; } = string.Empty;
        [Column("s3key")]
        public string? S3Key { get; set; }
        [Column("s3bucket")]
        public string? S3Bucket { get; set; }
        public DocumentSource Source { get; set; }
        public DocumentStatus Status { get; set; }
        [Column("documenttypeid")]
        public Guid? DocumentTypeId { get; set; }
        public DocumentType? DocumentType { get; set; }
        [Column("extractedtext")]
        public string? ExtractedText { get; set; }
        [Column("summary")]
        public string? Summary { get; set; }
        [Column("uploadedat")]
        public DateTime UploadedAt { get; set; }
        [Column("processedat")]
        public DateTime? ProcessedAt { get; set; }
        [Column("uploadedby")]
        public string UploadedBy { get; set; } = string.Empty;
        [Column("createdat")]
        public DateTime CreatedAt { get; set; }
        [Column("updatedat")]
        public DateTime UpdatedAt { get; set; }
        [Column("isdeleted")]
        public bool IsDeleted { get; set; }
        [Column("deletedat")]
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