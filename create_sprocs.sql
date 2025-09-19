-- Create stored procedures for DocumentRepository

-- 1. GetDocumentById
CREATE OR ALTER PROCEDURE dbo.GetDocumentById
    @DocumentId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        d.Id,
        d.FileName,
        d.FilePath,
        d.FileSize,
        d.ContentType as MimeType,
        d.UploadedAt,
        d.UploadedBy,
        d.Status,
        d.DocumentTypeId,
        d.IsDeleted,
        d.DeletedAt,
        d.CreatedAt,
        d.UpdatedAt,
        d.ExtractedText,
        d.FileExtension,
        d.OriginalFileName,
        d.ProcessedAt,
        d.S3Bucket,
        d.S3Key,
        d.Source,
        d.StoragePath,
        d.Summary,
        d.UploadedById
    FROM Documents d 
    WHERE d.Id = @DocumentId AND d.IsDeleted = 0;
END;
GO

-- 2. GetAllDocuments
CREATE OR ALTER PROCEDURE dbo.GetAllDocuments
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        d.Id,
        d.FileName,
        d.FilePath,
        d.FileSize,
        d.ContentType as MimeType,
        d.UploadedAt,
        d.UploadedBy,
        d.Status,
        d.DocumentTypeId,
        d.IsDeleted,
        d.DeletedAt,
        d.CreatedAt,
        d.UpdatedAt,
        d.ExtractedText,
        d.FileExtension,
        d.OriginalFileName,
        d.ProcessedAt,
        d.S3Bucket,
        d.S3Key,
        d.Source,
        d.StoragePath,
        d.Summary,
        d.UploadedById
    FROM Documents d 
    WHERE d.IsDeleted = 0
    ORDER BY d.UploadedAt DESC;
END;
GO

-- 3. GetDocumentsByUser
CREATE OR ALTER PROCEDURE dbo.GetDocumentsByUser
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        d.Id,
        d.FileName,
        d.FilePath,
        d.FileSize,
        d.ContentType as MimeType,
        d.UploadedAt,
        d.UploadedBy,
        d.Status,
        d.DocumentTypeId,
        d.IsDeleted,
        d.DeletedAt,
        d.CreatedAt,
        d.UpdatedAt,
        d.ExtractedText,
        d.FileExtension,
        d.OriginalFileName,
        d.ProcessedAt,
        d.S3Bucket,
        d.S3Key,
        d.Source,
        d.StoragePath,
        d.Summary,
        d.UploadedById
    FROM Documents d 
    WHERE d.UploadedBy = @UserId AND d.IsDeleted = 0
    ORDER BY d.UploadedAt DESC;
END;
GO

-- 4. GetRecentDocuments
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
        d.ContentType as MimeType,
        d.UploadedAt,
        d.UploadedBy,
        d.Status,
        d.DocumentTypeId,
        d.IsDeleted,
        d.DeletedAt,
        d.CreatedAt,
        d.UpdatedAt,
        d.ExtractedText,
        d.FileExtension,
        d.OriginalFileName,
        d.ProcessedAt,
        d.S3Bucket,
        d.S3Key,
        d.Source,
        d.StoragePath,
        d.Summary,
        d.UploadedById
    FROM Documents d 
    WHERE d.UploadedAt >= @CutoffDate AND d.IsDeleted = 0
    ORDER BY d.UploadedAt DESC;
END;
GO

-- 5. GetPagedDocuments
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
        d.FilePath,
        d.FileSize,
        d.ContentType as MimeType,
        d.UploadedAt,
        d.UploadedBy,
        d.Status,
        d.DocumentTypeId,
        d.IsDeleted,
        d.DeletedAt,
        d.CreatedAt,
        d.UpdatedAt,
        d.ExtractedText,
        d.FileExtension,
        d.OriginalFileName,
        d.ProcessedAt,
        d.S3Bucket,
        d.S3Key,
        d.Source,
        d.StoragePath,
        d.Summary,
        d.UploadedById
    FROM Documents d 
    WHERE d.IsDeleted = 0
    ORDER BY d.UploadedAt DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;
GO