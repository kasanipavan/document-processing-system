namespace DocumentProcessor.Infrastructure.AI;

/// <summary>
/// Represents extracted document content
/// </summary>
public class DocumentContent
{
    public string Text { get; set; } = "";
    public string ContentType { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new();
    public bool IsTruncated { get; set; }
    public List<string> ExtractedTables { get; set; } = new();
    public List<string> ExtractedImages { get; set; } = new();
}