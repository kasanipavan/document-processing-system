-- Stored Procedures for Document Processing System
-- These stored procedures replace Entity Framework DbSet queries in DocumentRepository

-- 1. Get Document by ID with all related data
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
        d.DeletedAt,
        
        -- Document Type
        dt.Id AS DocumentType_Id,
        dt.Name AS DocumentType_Name,
        dt.Description AS DocumentType_Description,
        dt.IsActive AS DocumentType_IsActive,
        dt.CreatedAt AS DocumentType_CreatedAt,
        dt.UpdatedAt AS DocumentType_UpdatedAt,
        
        -- Metadata
        dm.Id AS Metadata_Id,
        dm.DocumentId AS Metadata_DocumentId,
        dm.Author AS Metadata_Author,
        dm.Title AS Metadata_Title,
        dm.Subject AS Metadata_Subject,
        dm.Keywords AS Metadata_Keywords,
        dm.CreationDate AS Metadata_CreationDate,
        dm.ModificationDate AS Metadata_ModificationDate,
        dm.PageCount AS Metadata_PageCount,
        dm.WordCount AS Metadata_WordCount,
        dm.Language AS Metadata_Language,
        dm.CustomMetadata AS Metadata_CustomMetadata,
        dm.Tags AS Metadata_Tags,
        dm.CreatedAt AS Metadata_CreatedAt,
        dm.UpdatedAt AS Metadata_UpdatedAt,
        
        -- Classifications
        c.Id AS Classification_Id,
        c.DocumentId AS Classification_DocumentId,
        c.DocumentTypeId AS Classification_DocumentTypeId,
        c.ConfidenceScore AS Classification_ConfidenceScore,
        c.Method AS Classification_Method,
        c.AIModelUsed AS Classification_AIModelUsed,
        c.AIResponse AS Classification_AIResponse,
        c.ExtractedIntents AS Classification_ExtractedIntents,
        c.ExtractedEntities AS Classification_ExtractedEntities,
        c.IsManuallyVerified AS Classification_IsManuallyVerified,
        c.VerifiedBy AS Classification_VerifiedBy,
        c.VerifiedAt AS Classification_VerifiedAt,
        c.ClassifiedAt AS Classification_ClassifiedAt,
        c.CreatedAt AS Classification_CreatedAt,
        c.UpdatedAt AS Classification_UpdatedAt,
        
        -- Classification Document Type
        cdt.Id AS ClassificationDocumentType_Id,
        cdt.Name AS ClassificationDocumentType_Name,
        cdt.Description AS ClassificationDocumentType_Description,
        cdt.IsActive AS ClassificationDocumentType_IsActive,
        cdt.CreatedAt AS ClassificationDocumentType_CreatedAt,
        cdt.UpdatedAt AS ClassificationDocumentType_UpdatedAt,
        
        -- Processing Queue Items
        pq.Id AS ProcessingQueue_Id,
        pq.DocumentId AS ProcessingQueue_DocumentId,
        pq.ProcessingType AS ProcessingQueue_ProcessingType,
        pq.Status AS ProcessingQueue_Status,
        pq.Priority AS ProcessingQueue_Priority,
        pq.RetryCount AS ProcessingQueue_RetryCount,
        pq.MaxRetries AS ProcessingQueue_MaxRetries,
        pq.StartedAt AS ProcessingQueue_StartedAt,
        pq.CompletedAt AS ProcessingQueue_CompletedAt,
        pq.ErrorMessage AS ProcessingQueue_ErrorMessage,
        pq.ErrorDetails AS ProcessingQueue_ErrorDetails,
        pq.ProcessorId AS ProcessingQueue_ProcessorId,
        pq.ResultData AS ProcessingQueue_ResultData,
        pq.NextRetryAt AS ProcessingQueue_NextRetryAt,
        pq.CreatedAt AS ProcessingQueue_CreatedAt,
        pq.UpdatedAt AS ProcessingQueue_UpdatedAt
        
    FROM Documents d
    LEFT JOIN DocumentTypes dt ON d.DocumentTypeId = dt.Id
    LEFT JOIN DocumentMetadata dm ON d.Id = dm.DocumentId
    LEFT JOIN Classifications c ON d.Id = c.DocumentId
    LEFT JOIN DocumentTypes cdt ON c.DocumentTypeId = cdt.Id
    LEFT JOIN ProcessingQueues pq ON d.Id = pq.DocumentId
    WHERE d.Id = @DocumentId;
END;

-- 2. Get All Documents with related data
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
        d.DeletedAt,
        
        -- Document Type
        dt.Id AS DocumentType_Id,
        dt.Name AS DocumentType_Name,
        dt.Description AS DocumentType_Description,
        dt.IsActive AS DocumentType_IsActive,
        dt.CreatedAt AS DocumentType_CreatedAt,
        dt.UpdatedAt AS DocumentType_UpdatedAt,
        
        -- Metadata
        dm.Id AS Metadata_Id,
        dm.DocumentId AS Metadata_DocumentId,
        dm.Author AS Metadata_Author,
        dm.Title AS Metadata_Title,
        dm.Subject AS Metadata_Subject,
        dm.Keywords AS Metadata_Keywords,
        dm.CreationDate AS Metadata_CreationDate,
        dm.ModificationDate AS Metadata_ModificationDate,
        dm.PageCount AS Metadata_PageCount,
        dm.WordCount AS Metadata_WordCount,
        dm.Language AS Metadata_Language,
        dm.CustomMetadata AS Metadata_CustomMetadata,
        dm.Tags AS Metadata_Tags,
        dm.CreatedAt AS Metadata_CreatedAt,
        dm.UpdatedAt AS Metadata_UpdatedAt,
        
        -- Classifications
        c.Id AS Classification_Id,
        c.DocumentId AS Classification_DocumentId,
        c.DocumentTypeId AS Classification_DocumentTypeId,
        c.ConfidenceScore AS Classification_ConfidenceScore,
        c.Method AS Classification_Method,
        c.AIModelUsed AS Classification_AIModelUsed,
        c.AIResponse AS Classification_AIResponse,
        c.ExtractedIntents AS Classification_ExtractedIntents,
        c.ExtractedEntities AS Classification_ExtractedEntities,
        c.IsManuallyVerified AS Classification_IsManuallyVerified,
        c.VerifiedBy AS Classification_VerifiedBy,
        c.VerifiedAt AS Classification_VerifiedAt,
        c.ClassifiedAt AS Classification_ClassifiedAt,
        c.CreatedAt AS Classification_CreatedAt,
        c.UpdatedAt AS Classification_UpdatedAt,
        
        -- Classification Document Type
        cdt.Id AS ClassificationDocumentType_Id,
        cdt.Name AS ClassificationDocumentType_Name,
        cdt.Description AS ClassificationDocumentType_Description,
        cdt.IsActive AS ClassificationDocumentType_IsActive,
        cdt.CreatedAt AS ClassificationDocumentType_CreatedAt,
        cdt.UpdatedAt AS ClassificationDocumentType_UpdatedAt,
        
        -- Processing Queue Items
        pq.Id AS ProcessingQueue_Id,
        pq.DocumentId AS ProcessingQueue_DocumentId,
        pq.ProcessingType AS ProcessingQueue_ProcessingType,
        pq.Status AS ProcessingQueue_Status,
        pq.Priority AS ProcessingQueue_Priority,
        pq.RetryCount AS ProcessingQueue_RetryCount,
        pq.MaxRetries AS ProcessingQueue_MaxRetries,
        pq.StartedAt AS ProcessingQueue_StartedAt,
        pq.CompletedAt AS ProcessingQueue_CompletedAt,
        pq.ErrorMessage AS ProcessingQueue_ErrorMessage,
        pq.ErrorDetails AS ProcessingQueue_ErrorDetails,
        pq.ProcessorId AS ProcessingQueue_ProcessorId,
        pq.ResultData AS ProcessingQueue_ResultData,
        pq.NextRetryAt AS ProcessingQueue_NextRetryAt,
        pq.CreatedAt AS ProcessingQueue_CreatedAt,
        pq.UpdatedAt AS ProcessingQueue_UpdatedAt
        
    FROM Documents d
    LEFT JOIN DocumentTypes dt ON d.DocumentTypeId = dt.Id
    LEFT JOIN DocumentMetadata dm ON d.Id = dm.DocumentId
    LEFT JOIN Classifications c ON d.Id = c.DocumentId
    LEFT JOIN DocumentTypes cdt ON c.DocumentTypeId = cdt.Id
    LEFT JOIN ProcessingQueues pq ON d.Id = pq.DocumentId
    ORDER BY d.UploadedAt DESC;
END;

-- 3. Get Documents by User with related data
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
        d.DeletedAt,
        
        -- Document Type
        dt.Id AS DocumentType_Id,
        dt.Name AS DocumentType_Name,
        dt.Description AS DocumentType_Description,
        dt.IsActive AS DocumentType_IsActive,
        dt.CreatedAt AS DocumentType_CreatedAt,
        dt.UpdatedAt AS DocumentType_UpdatedAt,
        
        -- Metadata
        dm.Id AS Metadata_Id,
        dm.DocumentId AS Metadata_DocumentId,
        dm.Author AS Metadata_Author,
        dm.Title AS Metadata_Title,
        dm.Subject AS Metadata_Subject,
        dm.Keywords AS Metadata_Keywords,
        dm.CreationDate AS Metadata_CreationDate,
        dm.ModificationDate AS Metadata_ModificationDate,
        dm.PageCount AS Metadata_PageCount,
        dm.WordCount AS Metadata_WordCount,
        dm.Language AS Metadata_Language,
        dm.CustomMetadata AS Metadata_CustomMetadata,
        dm.Tags AS Metadata_Tags,
        dm.CreatedAt AS Metadata_CreatedAt,
        dm.UpdatedAt AS Metadata_UpdatedAt
        
    FROM Documents d
    LEFT JOIN DocumentTypes dt ON d.DocumentTypeId = dt.Id
    LEFT JOIN DocumentMetadata dm ON d.Id = dm.DocumentId
    WHERE d.UploadedBy = @UserId
    ORDER BY d.UploadedAt DESC;
END;

-- 4. Get Recent Documents with related data
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
        d.UpdatedAt,
        
        -- Document Type
        dt.Id AS DocumentType_Id,
        dt.Name AS DocumentType_Name,
        dt.Description AS DocumentType_Description,
        dt.IsActive AS DocumentType_IsActive,
        dt.CreatedAt AS DocumentType_CreatedAt,
        dt.UpdatedAt AS DocumentType_UpdatedAt,
        
        -- Metadata
        dm.Id AS Metadata_Id,
        dm.DocumentId AS Metadata_DocumentId,
        dm.Author AS Metadata_Author,
        dm.Title AS Metadata_Title,
        dm.Subject AS Metadata_Subject,
        dm.Keywords AS Metadata_Keywords,
        dm.CreationDate AS Metadata_CreationDate,
        dm.ModificationDate AS Metadata_ModificationDate,
        dm.PageCount AS Metadata_PageCount,
        dm.WordCount AS Metadata_WordCount,
        dm.Language AS Metadata_Language,
        dm.CustomMetadata AS Metadata_CustomMetadata,
        dm.Tags AS Metadata_Tags,
        dm.CreatedAt AS Metadata_CreatedAt,
        dm.UpdatedAt AS Metadata_UpdatedAt
        
    FROM Documents d
    LEFT JOIN DocumentTypes dt ON d.DocumentTypeId = dt.Id
    LEFT JOIN DocumentMetadata dm ON d.Id = dm.DocumentId
    WHERE d.UploadedAt >= @CutoffDate
    ORDER BY d.UploadedAt DESC;
END;

-- 5. Get Paged Documents with related data
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
        d.DeletedAt,
        
        -- Document Type
        dt.Id AS DocumentType_Id,
        dt.Name AS DocumentType_Name,
        dt.Description AS DocumentType_Description,
        dt.IsActive AS DocumentType_IsActive,
        dt.CreatedAt AS DocumentType_CreatedAt,
        dt.UpdatedAt AS DocumentType_UpdatedAt,
        
        -- Metadata
        dm.Id AS Metadata_Id,
        dm.DocumentId AS Metadata_DocumentId,
        dm.Author AS Metadata_Author,
        dm.Title AS Metadata_Title,
        dm.Subject AS Metadata_Subject,
        dm.Keywords AS Metadata_Keywords,
        dm.CreationDate AS Metadata_CreationDate,
        dm.ModificationDate AS Metadata_ModificationDate,
        dm.PageCount AS Metadata_PageCount,
        dm.WordCount AS Metadata_WordCount,
        dm.Language AS Metadata_Language,
        dm.CustomMetadata AS Metadata_CustomMetadata,
        dm.Tags AS Metadata_Tags,
        dm.CreatedAt AS Metadata_CreatedAt,
        dm.UpdatedAt AS Metadata_UpdatedAt
        
    FROM Documents d
    LEFT JOIN DocumentTypes dt ON d.DocumentTypeId = dt.Id
    LEFT JOIN DocumentMetadata dm ON d.Id = dm.DocumentId
    ORDER BY d.UploadedAt DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END;

-- 6. Get All Document Types
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