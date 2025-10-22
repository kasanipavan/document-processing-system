using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessor.Core.Entities
{
    [Table("documenttypes", Schema = "documentprocessor_dbo")]
    public class DocumentType
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        [Column("description")]
        public string Description { get; set; } = string.Empty;
        [Column("category")]
        public string Category { get; set; } = string.Empty;
        [Column("isactive")]
        public bool IsActive { get; set; } = true;
        [Column("priority")]
        public int Priority { get; set; }
        [Column("fileextensions")]
        public string? FileExtensions { get; set; } // Comma-separated list
        [Column("keywords")]
        public string? Keywords { get; set; } // Comma-separated list for matching
        [Column("processingrules")]
        public string? ProcessingRules { get; set; } // JSON configuration
        [Column("createdat")]
        public DateTime CreatedAt { get; set; }
        [Column("updatedat")]
        public DateTime UpdatedAt { get; set; }
        
        // Navigation properties
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<Classification> Classifications { get; set; } = new List<Classification>();
    }
}