using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Data;

/// <summary>
/// Initializes database stored procedures at application startup to avoid the need for database migrations.
/// Creates optimized stored procedures for document and document type retrieval operations.
/// </summary>
public static class StoredProcedureInitializer
{
    /// <summary>
    /// Creates or updates all stored procedures used by the DocumentRepository and DocumentTypeRepository.
    /// This approach allows for stored procedure implementation without requiring database migrations.
    /// </summary>
    /// <param name="context">The database context to execute stored procedure creation</param>
    /// <param name="logger">Logger for tracking initialization progress</param>
    public static async Task InitializeStoredProceduresAsync(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Initializing stored procedures...");

            // Create stored procedure: GetDocumentById
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE OR ALTER PROCEDURE dbo.GetDocumentById
                    @DocumentId UNIQUEIDENTIFIER
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    SELECT 
                        Id,
                        FileName,
                        OriginalFileName,
                        FileExtension,
                        FileSize,
                        ContentType,
                        StoragePath,
                        S3Key,
                        S3Bucket,
                        Source,
                        Status,
                        DocumentTypeId,
                        ExtractedText,
                        Summary,
                        UploadedAt,
                        ProcessedAt,
                        UploadedBy,
                        CreatedAt,
                        UpdatedAt,
                        IsDeleted,
                        DeletedAt
                    FROM Documents
                    WHERE Id = @DocumentId;
                END;
            ");

            // Create stored procedure: GetAllDocuments
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE OR ALTER PROCEDURE dbo.GetAllDocuments
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    SELECT 
                        Id,
                        FileName,
                        OriginalFileName,
                        FileExtension,
                        FileSize,
                        ContentType,
                        StoragePath,
                        S3Key,
                        S3Bucket,
                        Source,
                        Status,
                        DocumentTypeId,
                        ExtractedText,
                        Summary,
                        UploadedAt,
                        ProcessedAt,
                        UploadedBy,
                        CreatedAt,
                        UpdatedAt,
                        IsDeleted,
                        DeletedAt
                    FROM Documents
                    WHERE IsDeleted = 0
                    ORDER BY UploadedAt DESC;
                END;
            ");

            // Create stored procedure: GetDocumentsByUser
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE OR ALTER PROCEDURE dbo.GetDocumentsByUser
                    @UserId NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    SELECT 
                        Id,
                        FileName,
                        OriginalFileName,
                        FileExtension,
                        FileSize,
                        ContentType,
                        StoragePath,
                        S3Key,
                        S3Bucket,
                        Source,
                        Status,
                        DocumentTypeId,
                        ExtractedText,
                        Summary,
                        UploadedAt,
                        ProcessedAt,
                        UploadedBy,
                        CreatedAt,
                        UpdatedAt,
                        IsDeleted,
                        DeletedAt
                    FROM Documents
                    WHERE UploadedBy = @UserId AND IsDeleted = 0
                    ORDER BY UploadedAt DESC;
                END;
            ");

            // Create stored procedure: GetRecentDocuments
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE OR ALTER PROCEDURE dbo.GetRecentDocuments
                    @Days INT = 7,
                    @Limit INT = 100
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @CutoffDate DATETIME2 = DATEADD(DAY, -@Days, GETUTCDATE());
                    
                    SELECT TOP (@Limit)
                        Id,
                        FileName,
                        OriginalFileName,
                        FileExtension,
                        FileSize,
                        ContentType,
                        StoragePath,
                        S3Key,
                        S3Bucket,
                        Source,
                        Status,
                        DocumentTypeId,
                        ExtractedText,
                        Summary,
                        UploadedAt,
                        ProcessedAt,
                        UploadedBy,
                        CreatedAt,
                        UpdatedAt,
                        IsDeleted,
                        DeletedAt
                    FROM Documents
                    WHERE UploadedAt >= @CutoffDate AND IsDeleted = 0
                    ORDER BY UploadedAt DESC;
                END;
            ");

            // Create stored procedure: GetPagedDocuments
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE OR ALTER PROCEDURE dbo.GetPagedDocuments
                    @PageNumber INT = 1,
                    @PageSize INT = 10
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
                    
                    SELECT 
                        Id,
                        FileName,
                        OriginalFileName,
                        FileExtension,
                        FileSize,
                        ContentType,
                        StoragePath,
                        S3Key,
                        S3Bucket,
                        Source,
                        Status,
                        DocumentTypeId,
                        ExtractedText,
                        Summary,
                        UploadedAt,
                        ProcessedAt,
                        UploadedBy,
                        CreatedAt,
                        UpdatedAt,
                        IsDeleted,
                        DeletedAt
                    FROM Documents
                    WHERE IsDeleted = 0
                    ORDER BY UploadedAt DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;
                END;
            ");

            // Create stored procedure: GetDocumentTypes
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE OR ALTER PROCEDURE dbo.GetDocumentTypes
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    SELECT 
                        Id,
                        Name,
                        Description,
                        Category,
                        IsActive,
                        Priority,
                        FileExtensions,
                        Keywords,
                        ProcessingRules,
                        CreatedAt,
                        UpdatedAt
                    FROM DocumentTypes
                    WHERE IsActive = 1
                    ORDER BY Name;
                END;
            ");

            logger.LogInformation("Stored procedures initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing stored procedures");
            throw;
        }
    }
}