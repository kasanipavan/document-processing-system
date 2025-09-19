using System;
using System.Collections.Generic;

namespace DocumentProcessor.Core.Entities
{
    public class DocumentType
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int Priority { get; set; }
        public string? FileExtensions { get; set; } // Comma-separated list
        public string? Keywords { get; set; } // Comma-separated list for matching
        public string? ProcessingRules { get; set; } // JSON configuration
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<Classification> Classifications { get; set; } = new List<Classification>();
    }
}