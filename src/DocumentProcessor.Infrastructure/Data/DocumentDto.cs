using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Infrastructure.Data;

/// <summary>
/// Data Transfer Object for Document entity without navigation properties.
/// Solves Entity Framework SqlQueryRaw limitation where navigation properties are not supported.
/// Used exclusively with stored procedures to avoid "Navigation property not supported" errors.
/// </summary>
public class DocumentDto
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
    public string? ExtractedText { get; set; }
    public string? Summary { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Converts DocumentDto to Document entity.
    /// Navigation properties (DocumentType, Metadata) must be loaded separately by the repository.
    /// </summary>
    /// <returns>Document entity with all scalar properties populated</returns>
    public Document ToDocument()
    {
        return new Document
        {
            Id = Id,
            FileName = FileName,
            OriginalFileName = OriginalFileName,
            FileExtension = FileExtension,
            FileSize = FileSize,
            ContentType = ContentType,
            StoragePath = StoragePath,
            S3Key = S3Key,
            S3Bucket = S3Bucket,
            Source = Source,
            Status = Status,
            DocumentTypeId = DocumentTypeId,
            ExtractedText = ExtractedText,
            Summary = Summary,
            UploadedAt = UploadedAt,
            ProcessedAt = ProcessedAt,
            UploadedBy = UploadedBy,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            IsDeleted = IsDeleted,
            DeletedAt = DeletedAt
        };
    }
}