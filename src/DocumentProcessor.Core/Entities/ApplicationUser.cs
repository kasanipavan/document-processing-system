using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocumentProcessor.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;
        public int? DocumentQuota { get; set; }
        public int DocumentsProcessedCount { get; set; }
        
        // Navigation properties
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
        public virtual ICollection<UserActivityLog> ActivityLogs { get; set; } = new List<UserActivityLog>();
    }

    public class ApplicationRole : IdentityRole
    {
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [Table("useractivitylogs", Schema = "documentprocessor_dbo")]
    public class UserActivityLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }
        [Column("userid")]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;
        [Column("activity")]
        public string Activity { get; set; } = null!;
        [Column("details")]
        public string? Details { get; set; }
        [Column("ipaddress")]
        public string? IpAddress { get; set; }
        [Column("useragent")]
        public string? UserAgent { get; set; }
        [Column("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}