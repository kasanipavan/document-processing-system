using Microsoft.EntityFrameworkCore.Migrations;

namespace DocumentProcessor.Infrastructure.Migrations
{
    /// <summary>
    /// Migration to create stored procedures for document repository operations
    /// </summary>
    public partial class StoredProcedureMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create stored procedure: GetDocumentById
            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE dbo.GetDocumentById
                    @DocumentId UNIQUEIDENTIFIER
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    SELECT 
                        d.Id,
                        d.FileName,
                        d.OriginalFileName,
                        d.FileExtension,
                        d.FileSize,
                        d.ContentType,
                        d.StoragePath,
                        d.S3Key,
                        d.S3Bucket,
                        d.Source,
                        d.Status,
                        d.DocumentTypeId,
                        d.ExtractedText,
                        d.Summary,
                        d.UploadedAt,
                        d.ProcessedAt,
                        d.UploadedBy,
                        d.CreatedAt,
                        d.UpdatedAt,
                        d.IsDeleted,
                        d.DeletedAt
                    FROM Documents d
                    WHERE d.Id = @DocumentId;
                END;
            ");

            // Create stored procedure: GetAllDocuments
            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE dbo.GetAllDocuments
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    SELECT 
                        d.Id,
                        d.FileName,
                        d.OriginalFileName,
                        d.FileExtension,
                        d.FileSize,
                        d.ContentType,
                        d.StoragePath,
                        d.S3Key,
                        d.S3Bucket,
                        d.Source,
                        d.Status,
                        d.DocumentTypeId,
                        d.ExtractedText,
                        d.Summary,
                        d.UploadedAt,
                        d.ProcessedAt,
                        d.UploadedBy,
                        d.CreatedAt,
                        d.UpdatedAt,
                        d.IsDeleted,
                        d.DeletedAt
                    FROM Documents d
                    ORDER BY d.UploadedAt DESC;
                END;
            ");

            // Create stored procedure: GetDocumentsByUser
            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE dbo.GetDocumentsByUser
                    @UserId NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    SELECT 
                        d.Id,
                        d.FileName,
                        d.OriginalFileName,
                        d.FileExtension,
                        d.FileSize,
                        d.ContentType,
                        d.StoragePath,
                        d.S3Key,
                        d.S3Bucket,
                        d.Source,
                        d.Status,
                        d.DocumentTypeId,
                        d.ExtractedText,
                        d.Summary,
                        d.UploadedAt,
                        d.ProcessedAt,
                        d.UploadedBy,
                        d.CreatedAt,
                        d.UpdatedAt,
                        d.IsDeleted,
                        d.DeletedAt
                    FROM Documents d
                    WHERE d.UploadedBy = @UserId
                    ORDER BY d.UploadedAt DESC;
                END;
            ");

            // Create stored procedure: GetRecentDocuments
            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE dbo.GetRecentDocuments
                    @Days INT = 7,
                    @Limit INT = 100
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @CutoffDate DATETIME2 = DATEADD(DAY, -@Days, GETUTCDATE());
                    
                    SELECT TOP (@Limit)
                        d.Id,
                        d.FileName,
                        d.FilePath,
                        d.FileSize,
                        d.MimeType,
                        d.UploadedAt,
                        d.UploadedBy,
                        d.Status,
                        d.DocumentTypeId,
                        d.IsDeleted,
                        d.DeletedAt,
                        d.CreatedAt,
                        d.UpdatedAt
                    FROM Documents d
                    WHERE d.UploadedAt >= @CutoffDate
                    ORDER BY d.UploadedAt DESC;
                END;
            ");

            // Create stored procedure: GetPagedDocuments
            migrationBuilder.Sql(@"
                CREATE OR ALTER PROCEDURE dbo.GetPagedDocuments
                    @PageNumber INT = 1,
                    @PageSize INT = 10
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
                    
                    SELECT 
                        d.Id,
                        d.FileName,
                        d.OriginalFileName,
                        d.FileExtension,
                        d.FileSize,
                        d.ContentType,
                        d.StoragePath,
                        d.S3Key,
                        d.S3Bucket,
                        d.Source,
                        d.Status,
                        d.DocumentTypeId,
                        d.ExtractedText,
                        d.Summary,
                        d.UploadedAt,
                        d.ProcessedAt,
                        d.UploadedBy,
                        d.CreatedAt,
                        d.UpdatedAt,
                        d.IsDeleted,
                        d.DeletedAt
                    FROM Documents d
                    ORDER BY d.UploadedAt DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;
                END;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.GetDocumentById");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.GetAllDocuments");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.GetDocumentsByUser");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.GetRecentDocuments");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.GetPagedDocuments");
        }
    }
}