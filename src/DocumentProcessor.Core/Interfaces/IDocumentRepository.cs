using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(Guid id);
        Task<IEnumerable<Document>> GetAllAsync();
        Task<IEnumerable<Document>> GetByStatusAsync(DocumentStatus status);
        Task<IEnumerable<Document>> GetByDocumentTypeAsync(Guid documentTypeId);
        Task<IEnumerable<Document>> GetByUserAsync(string userId);
        Task<IEnumerable<Document>> GetPendingDocumentsAsync(int limit = 100);
        Task<IEnumerable<Document>> FindAsync(Expression<Func<Document, bool>> predicate);
        Task<Document> AddAsync(Document document);
        Task<Document> UpdateAsync(Document document);
        Task DeleteAsync(Guid id);
        Task SoftDeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<Document, bool>> predicate);
        Task<IEnumerable<Document>> GetPagedAsync(int pageNumber, int pageSize);
    }
}