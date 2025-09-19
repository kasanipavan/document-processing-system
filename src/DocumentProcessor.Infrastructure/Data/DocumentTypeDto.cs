using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Infrastructure.Data;

/// <summary>
/// DTO for DocumentType entity without navigation properties for stored procedure calls
/// </summary>
public class DocumentTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public string? FileExtensions { get; set; }
    public string? Keywords { get; set; }
    public string? ProcessingRules { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Converts DocumentTypeDto to DocumentType entity
    /// </summary>
    public DocumentType ToDocumentType()
    {
        return new DocumentType
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Category = Category,
            IsActive = IsActive,
            Priority = Priority,
            FileExtensions = FileExtensions,
            Keywords = Keywords,
            ProcessingRules = ProcessingRules,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}