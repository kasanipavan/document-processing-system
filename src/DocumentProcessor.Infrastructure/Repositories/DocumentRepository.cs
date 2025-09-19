using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Data;

namespace DocumentProcessor.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for Document entities implementing custom stored procedures for optimized data access.
    /// Uses Data Transfer Objects (DTOs) to handle Entity Framework SqlQueryRaw limitations with navigation properties.
    /// </summary>
    public class DocumentRepository : RepositoryBase<Document>, IDocumentRepository
    {
        ApplicationDbContext _context;

        public DocumentRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves a document by ID using the GetDocumentById stored procedure.
        /// Loads document type and metadata navigation properties separately due to SqlQueryRaw limitations.
        /// </summary>
        /// <param name="id">The unique identifier of the document</param>
        /// <returns>The document with loaded navigation properties, or null if not found</returns>
        public async Task<Document?> GetByIdAsync(Guid id)
        {
            var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
                "EXEC dbo.GetDocumentById @DocumentId = {0}", id).ToListAsync();
            var documentDto = documentDtos.FirstOrDefault();
            
            if (documentDto == null) return null;
            
            var document = documentDto.ToDocument();
            
            // Load navigation properties if needed using regular EF
            if (document.DocumentTypeId.HasValue)
            {
                document.DocumentType = await _context.DocumentTypes
                    .FirstOrDefaultAsync(dt => dt.Id == document.DocumentTypeId.Value);
            }
            
            document.Metadata = await _context.DocumentMetadata
                .FirstOrDefaultAsync(dm => dm.DocumentId == document.Id);
                
            return document;
        }

        /// <summary>
        /// Retrieves all active documents using the GetAllDocuments stored procedure.
        /// Efficiently loads document types in a batch query to minimize database round trips.
        /// </summary>
        /// <returns>All active documents with their document types loaded</returns>
        public new async Task<IEnumerable<Document>> GetAllAsync()
        {
            var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
                "EXEC dbo.GetAllDocuments").ToListAsync();
            
            var documents = documentDtos.Select(dto => dto.ToDocument()).ToList();
            
            // Load document types for all documents in a single query
            if (documents.Any())
            {
                var documentTypeIds = documents.Where(d => d.DocumentTypeId.HasValue)
                    .Select(d => d.DocumentTypeId!.Value).Distinct().ToList();
                
                var documentTypes = await _context.DocumentTypes
                    .Where(dt => documentTypeIds.Contains(dt.Id))
                    .ToListAsync();
                
                foreach (var document in documents.Where(d => d.DocumentTypeId.HasValue))
                {
                    document.DocumentType = documentTypes.FirstOrDefault(dt => dt.Id == document.DocumentTypeId);
                }
            }
            
            return documents;
        }

        /// <summary>
        /// Retrieves documents by their processing status using Entity Framework.
        /// </summary>
        /// <param name="status">The document processing status to filter by</param>
        /// <returns>Documents with the specified status, including navigation properties</returns>
        public async Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(d => d.Status == status)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves documents by document type using Entity Framework.
        /// </summary>
        /// <param name="documentTypeId">The document type ID to filter by</param>
        /// <returns>Documents of the specified type, including navigation properties</returns>
        public async Task<IEnumerable<Document>> GetByDocumentTypeAsync(Guid documentTypeId)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(d => d.DocumentTypeId == documentTypeId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves documents uploaded by a specific user using stored procedure.
        /// </summary>
        /// <param name="userId">The ID of the user who uploaded the documents</param>
        /// <returns>Documents uploaded by the specified user</returns>
        public async Task<IEnumerable<Document>> GetByUserAsync(string userId)
        {
            var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
                "EXEC dbo.GetDocumentsByUser @UserId = {0}", userId).ToListAsync();
            
            return documentDtos.Select(dto => dto.ToDocument()).ToList();
        }

        public async Task<IEnumerable<Document>> GetPendingDocumentsAsync(int limit = 100)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Queued)
                .OrderBy(d => d.UploadedAt)
                .Take(limit)
                .ToListAsync();
        }

        public new async Task<IEnumerable<Document>> FindAsync(Expression<Func<Document, bool>> predicate)
        {
            return await _dbSet
                .Include(d => d.DocumentType)
                .Include(d => d.Metadata)
                .Where(predicate)
                .ToListAsync();
        }

        public new async Task<Document> AddAsync(Document document)
        {
            if (document.Id == Guid.Empty)
                document.Id = Guid.NewGuid();
            
            document.CreatedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            
            await _dbSet.AddAsync(document);
            await _context.SaveChangesAsync(); // Save to database
            return document;
        }

        public async Task<Document> UpdateAsync(Document document)
        {
            document.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(document);
            await _context.SaveChangesAsync(); // Save to database
            return document;
        }


        public async Task DeleteAsync(Guid id)
        {
            var document = await _dbSet.FindAsync(id);
            if (document != null)
            {
                _dbSet.Remove(document);
                await _context.SaveChangesAsync(); // Save to database
            }
        }

        public async Task SoftDeleteAsync(Guid id)
        {
            var document = await _dbSet.FindAsync(id);
            if (document != null)
            {
                document.IsDeleted = true;
                document.DeletedAt = DateTime.UtcNow;
                document.UpdatedAt = DateTime.UtcNow;
                _dbSet.Update(document);
                await _context.SaveChangesAsync(); // Save to database
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _dbSet.AnyAsync(d => d.Id == id);
        }

        public new async Task<int> CountAsync()
        {
            return await _dbSet.CountAsync();
        }

        public new async Task<int> CountAsync(Expression<Func<Document, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }

        public new async Task<IEnumerable<Document>> GetPagedAsync(int pageNumber, int pageSize)
        {
            var documentDtos = await _context.Database.SqlQueryRaw<DocumentDto>(
                "EXEC dbo.GetPagedDocuments @PageNumber = {0}, @PageSize = {1}", pageNumber, pageSize).ToListAsync();
            
            return documentDtos.Select(dto => dto.ToDocument()).ToList();
        }

    }
}