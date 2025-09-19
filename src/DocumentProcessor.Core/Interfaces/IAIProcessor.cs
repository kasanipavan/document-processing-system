using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IAIProcessor
    {
        Task<DocumentClassificationResult> ClassifyDocumentAsync(Document document, Stream documentContent);
        Task<DocumentExtractionResult> ExtractDataAsync(Document document, Stream documentContent);
        Task<DocumentSummaryResult> GenerateSummaryAsync(Document document, Stream documentContent);
        Task<DocumentIntentResult> DetectIntentAsync(Document document, Stream documentContent);
        Task<ProcessingCost> EstimateCostAsync(long fileSize, string contentType);
        string ModelId { get; }
        string ProviderName { get; }
    }

    public class DocumentClassificationResult
    {
        public string PrimaryCategory { get; set; } = string.Empty;
        public Dictionary<string, double> CategoryConfidences { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public string ProcessingNotes { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
    }

    public class DocumentExtractionResult
    {
        public Dictionary<string, object> ExtractedData { get; set; } = new();
        public List<ExtractedEntity> Entities { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }

    public class ExtractedEntity
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int StartPosition { get; set; }
        public int EndPosition { get; set; }
    }

    public class DocumentSummaryResult
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new();
        public List<string> ActionItems { get; set; } = new();
        public string Language { get; set; } = "en";
        public TimeSpan ProcessingTime { get; set; }
    }

    public class DocumentIntentResult
    {
        public string PrimaryIntent { get; set; } = string.Empty;
        public List<string> SecondaryIntents { get; set; } = new();
        public double Confidence { get; set; }
        public string SuggestedAction { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
    }

    public class ProcessingCost
    {
        public decimal EstimatedCost { get; set; }
        public string Currency { get; set; } = "USD";
        public int EstimatedTokens { get; set; }
        public string ModelUsed { get; set; } = string.Empty;
        public string PricingTier { get; set; } = string.Empty;
    }
}