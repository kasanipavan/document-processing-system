using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<Classification> Classifications { get; set; }
        public DbSet<ProcessingQueue> ProcessingQueues { get; set; }
        public DbSet<DocumentMetadata> DocumentMetadata { get; set; }
        public DbSet<UserActivityLog> UserActivityLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Document entity
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileExtension).HasMaxLength(50);
                entity.Property(e => e.ContentType).HasMaxLength(100);
                entity.Property(e => e.StoragePath).HasMaxLength(1000);
                entity.Property(e => e.S3Key).HasMaxLength(500);
                entity.Property(e => e.S3Bucket).HasMaxLength(255);
                entity.Property(e => e.UploadedBy).IsRequired().HasMaxLength(255);
                
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.DocumentTypeId);
                entity.HasIndex(e => e.UploadedAt);
                entity.HasIndex(e => e.IsDeleted);
                
                // Configure soft delete filter
                entity.HasQueryFilter(e => !e.IsDeleted);
                
                // Configure one-to-one relationship with metadata
                entity.HasOne(e => e.Metadata)
                    .WithOne(m => m.Document)
                    .HasForeignKey<DocumentMetadata>(m => m.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure DocumentType entity
            modelBuilder.Entity<DocumentType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.FileExtensions).HasMaxLength(500);
                entity.Property(e => e.Keywords).HasMaxLength(2000);
                
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure Classification entity
            modelBuilder.Entity<Classification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AIModelUsed).HasMaxLength(100);
                entity.Property(e => e.VerifiedBy).HasMaxLength(255);
                
                entity.HasIndex(e => new { e.DocumentId, e.DocumentTypeId });
                entity.HasIndex(e => e.ConfidenceScore);
                entity.HasIndex(e => e.ClassifiedAt);
                
                entity.HasOne(e => e.Document)
                    .WithMany(d => d.Classifications)
                    .HasForeignKey(e => e.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasOne(e => e.DocumentType)
                    .WithMany(dt => dt.Classifications)
                    .HasForeignKey(e => e.DocumentTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ProcessingQueue entity
            modelBuilder.Entity<ProcessingQueue>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProcessorId).HasMaxLength(100);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Priority);
                entity.HasIndex(e => new { e.Status, e.Priority });
                entity.HasIndex(e => e.CreatedAt);
                
                entity.HasOne(e => e.Document)
                    .WithMany(d => d.ProcessingQueueItems)
                    .HasForeignKey(e => e.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure DocumentMetadata entity
            modelBuilder.Entity<DocumentMetadata>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Author).HasMaxLength(255);
                entity.Property(e => e.Title).HasMaxLength(500);
                entity.Property(e => e.Subject).HasMaxLength(500);
                entity.Property(e => e.Keywords).HasMaxLength(2000);
                entity.Property(e => e.Language).HasMaxLength(10);
                
                // Configure Tags as JSON column (EF Core 9.0+)
                entity.OwnsOne(e => e.Tags, builder =>
                {
                    builder.ToJson();
                });
                
                entity.HasIndex(e => e.DocumentId).IsUnique();
            });

            // Configure UserActivityLog entity
            modelBuilder.Entity<UserActivityLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Activity).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Details).HasMaxLength(1000);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Timestamp);
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ActivityLogs)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure ApplicationUser entity extensions
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Department).HasMaxLength(100);
                entity.Property(e => e.ProfilePictureUrl).HasMaxLength(500);
                
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.CreatedAt);
                
                // Configure relationship with Documents
                entity.HasMany(e => e.Documents)
                    .WithOne()
                    .HasForeignKey("UploadedById")
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ApplicationRole entity extensions
            modelBuilder.Entity<ApplicationRole>(entity =>
            {
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasIndex(e => e.IsActive);
            });

            // Configure table names
            modelBuilder.Entity<Document>().ToTable("Documents");
            modelBuilder.Entity<DocumentType>().ToTable("DocumentTypes");
            modelBuilder.Entity<Classification>().ToTable("Classifications");
            modelBuilder.Entity<ProcessingQueue>().ToTable("ProcessingQueues");
            modelBuilder.Entity<DocumentMetadata>().ToTable("DocumentMetadata");
            modelBuilder.Entity<UserActivityLog>().ToTable("UserActivityLogs");

            // Seed initial document types
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            modelBuilder.Entity<DocumentType>().HasData(
                new DocumentType
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Invoice",
                    Description = "Commercial invoice documents",
                    Category = "Financial",
                    IsActive = true,
                    Priority = 1,
                    FileExtensions = ".pdf,.doc,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Contract",
                    Description = "Legal contract documents",
                    Category = "Legal",
                    IsActive = true,
                    Priority = 1,
                    FileExtensions = ".pdf,.doc,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Name = "Report",
                    Description = "Business reports and analytics",
                    Category = "Business",
                    IsActive = true,
                    Priority = 2,
                    FileExtensions = ".pdf,.xlsx,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Name = "Resume",
                    Description = "Resume and CV documents",
                    Category = "HR",
                    IsActive = true,
                    Priority = 3,
                    FileExtensions = ".pdf,.doc,.docx",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                },
                new DocumentType
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    Name = "Email",
                    Description = "Email correspondence",
                    Category = "Communication",
                    IsActive = true,
                    Priority = 3,
                    FileExtensions = ".eml,.msg,.txt",
                    CreatedAt = seedDate,
                    UpdatedAt = seedDate
                }
            );
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity is Document document)
                {
                    if (entry.State == EntityState.Added)
                    {
                        document.CreatedAt = DateTime.UtcNow;
                    }
                    document.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is DocumentType documentType)
                {
                    if (entry.State == EntityState.Added)
                    {
                        documentType.CreatedAt = DateTime.UtcNow;
                    }
                    documentType.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Classification classification)
                {
                    if (entry.State == EntityState.Added)
                    {
                        classification.CreatedAt = DateTime.UtcNow;
                    }
                    classification.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is ProcessingQueue queue)
                {
                    if (entry.State == EntityState.Added)
                    {
                        queue.CreatedAt = DateTime.UtcNow;
                    }
                    queue.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is DocumentMetadata metadata)
                {
                    if (entry.State == EntityState.Added)
                    {
                        metadata.CreatedAt = DateTime.UtcNow;
                    }
                    metadata.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}