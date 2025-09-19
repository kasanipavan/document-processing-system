using DocumentProcessor.Core.Interfaces;

namespace DocumentProcessor.Application.Services;

public class DocumentProcessingResult
{
    public Guid DocumentId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public DocumentClassificationResult? Classification { get; set; }
    public DocumentExtractionResult? Extraction { get; set; }
    public DocumentSummaryResult? Summary { get; set; }
    public DocumentIntentResult? Intent { get; set; }
}