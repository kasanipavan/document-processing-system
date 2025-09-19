using System;
using System.Threading.Tasks;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DocumentProcessor.Infrastructure.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IDocumentRepository Documents { get; }
        IDocumentTypeRepository DocumentTypes { get; }
        IClassificationRepository Classifications { get; }
        IProcessingQueueRepository ProcessingQueues { get; }
        IDocumentMetadataRepository DocumentMetadata { get; }
        
        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;

        private IDocumentRepository? _documentRepository;
        private IDocumentTypeRepository? _documentTypeRepository;
        private IClassificationRepository? _classificationRepository;
        private IProcessingQueueRepository? _processingQueueRepository;
        private IDocumentMetadataRepository? _documentMetadataRepository;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public IDocumentRepository Documents =>
            _documentRepository ??= new DocumentRepository(_context);

        public IDocumentTypeRepository DocumentTypes =>
            _documentTypeRepository ??= new DocumentTypeRepository(_context);

        public IClassificationRepository Classifications =>
            _classificationRepository ??= new ClassificationRepository(_context);

        public IProcessingQueueRepository ProcessingQueues =>
            _processingQueueRepository ??= new ProcessingQueueRepository(_context);

        public IDocumentMetadataRepository DocumentMetadata =>
            _documentMetadataRepository ??= new DocumentMetadataRepository(_context);

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                await SaveChangesAsync();
                if (_transaction != null)
                {
                    await _transaction.CommitAsync();
                }
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
        }

        public async Task RollbackTransactionAsync()
        {
            try
            {
                if (_transaction != null)
                {
                    await _transaction.RollbackAsync();
                }
            }
            finally
            {
                if (_transaction != null)
                {
                    await _transaction.DisposeAsync();
                    _transaction = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _transaction?.Dispose();
                _context.Dispose();
            }
        }
    }

    // Additional repository interfaces
    public interface IDocumentTypeRepository
    {
        Task<DocumentType?> GetByIdAsync(Guid id);
        Task<DocumentType?> GetByNameAsync(string name);
        Task<IEnumerable<DocumentType>> GetAllAsync();
        Task<IEnumerable<DocumentType>> GetActiveAsync();
        Task<DocumentType> AddAsync(DocumentType documentType);
        Task<DocumentType> UpdateAsync(DocumentType documentType);
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
    }

    public interface IClassificationRepository
    {
        Task<Classification?> GetByIdAsync(Guid id);
        Task<IEnumerable<Classification>> GetByDocumentIdAsync(Guid documentId);
        Task<IEnumerable<Classification>> GetByDocumentTypeIdAsync(Guid documentTypeId);
        Task<Classification> AddAsync(Classification classification);
        Task<Classification> UpdateAsync(Classification classification);
        Task DeleteAsync(Guid id);
    }

    public interface IProcessingQueueRepository
    {
        Task<ProcessingQueue?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProcessingQueue>> GetPendingAsync(int limit = 100);
        Task<IEnumerable<ProcessingQueue>> GetByStatusAsync(ProcessingStatus status);
        Task<IEnumerable<ProcessingQueue>> GetByDocumentIdAsync(Guid documentId);
        Task<ProcessingQueue> AddAsync(ProcessingQueue item);
        Task<ProcessingQueue> UpdateAsync(ProcessingQueue item);
        Task DeleteAsync(Guid id);
        Task<int> GetQueueLengthAsync();
        Task<bool> StartProcessingAsync(Guid id, string processorId);
        Task<bool> CompleteProcessingAsync(Guid id, string? resultData = null);
        Task<bool> FailProcessingAsync(Guid id, string errorMessage, string? errorDetails = null);
        Task<IEnumerable<ProcessingQueue>> GetStuckItemsAsync(int minutesThreshold = 30);
        Task<Dictionary<ProcessingStatus, int>> GetStatusCountsAsync();
        Task<Dictionary<ProcessingType, int>> GetProcessingTypeCountsAsync();
    }

    public interface IDocumentMetadataRepository
    {
        Task<DocumentMetadata?> GetByIdAsync(Guid id);
        Task<DocumentMetadata?> GetByDocumentIdAsync(Guid documentId);
        Task<DocumentMetadata> AddAsync(DocumentMetadata metadata);
        Task<DocumentMetadata> UpdateAsync(DocumentMetadata metadata);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<DocumentMetadata>> GetAllAsync();
        Task<IEnumerable<DocumentMetadata>> SearchByKeywordsAsync(string keywords);
        Task<IEnumerable<DocumentMetadata>> GetByAuthorAsync(string author);
        Task<IEnumerable<DocumentMetadata>> GetByLanguageAsync(string language);
        Task<IEnumerable<DocumentMetadata>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<bool> AddTagAsync(Guid metadataId, string key, string value);
        Task<bool> RemoveTagAsync(Guid metadataId, string key);
        Task<Dictionary<string, int>> GetTopAuthorsAsync(int limit = 10);
        Task<Dictionary<string, int>> GetLanguageDistributionAsync();
    }
}