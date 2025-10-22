using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessor.Core.Entities
{
    [Table("documentmetadata", Schema = "documentprocessor_dbo")]
    public class DocumentMetadata
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("documentid")]
        public Guid DocumentId { get; set; }

        public Document Document { get; set; } = null!;

        [Column("author")]
        public string? Author { get; set; }

        [Column("title")]
        public string? Title { get; set; }

        [Column("subject")]
        public string? Subject { get; set; }

        [Column("keywords")]
        public string? Keywords { get; set; }

        [Column("creationdate")]
        public DateTime? CreationDate { get; set; }

        [Column("modificationdate")]
        public DateTime? ModificationDate { get; set; }

        [Column("pagecount")]
        public int? PageCount { get; set; }

        [Column("wordcount")]
        public int? WordCount { get; set; }

        [Column("language")]
        public string? Language { get; set; }

        [Column("custommetadata")]
        public string? CustomMetadata { get; set; } // JSON for additional metadata

        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        [Column("createdat")]
        public DateTime CreatedAt { get; set; }

        [Column("updatedat")]
        public DateTime UpdatedAt { get; set; }
    }
}