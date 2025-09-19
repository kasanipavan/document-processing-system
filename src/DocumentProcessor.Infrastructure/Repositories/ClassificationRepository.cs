using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Infrastructure.Data;

namespace DocumentProcessor.Infrastructure.Repositories
{
    public class ClassificationRepository : RepositoryBase<Classification>, IClassificationRepository
    {
        public ClassificationRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Classification?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(c => c.Document)
                .Include(c => c.DocumentType)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<IEnumerable<Classification>> GetByDocumentIdAsync(Guid documentId)
        {
            return await _dbSet
                .Include(c => c.DocumentType)
                .Where(c => c.DocumentId == documentId)
                .OrderByDescending(c => c.ConfidenceScore)
                .ToListAsync();
        }

        public async Task<IEnumerable<Classification>> GetByDocumentTypeIdAsync(Guid documentTypeId)
        {
            return await _dbSet
                .Include(c => c.Document)
                .Where(c => c.DocumentTypeId == documentTypeId)
                .OrderByDescending(c => c.ClassifiedAt)
                .ToListAsync();
        }

        public async Task<Classification> AddAsync(Classification classification)
        {
            if (classification.Id == Guid.Empty)
                classification.Id = Guid.NewGuid();

            classification.ClassifiedAt = DateTime.UtcNow;
            classification.CreatedAt = DateTime.UtcNow;
            classification.UpdatedAt = DateTime.UtcNow;

            await _dbSet.AddAsync(classification);
            await _context.SaveChangesAsync(); // Save to database
            return classification;
        }

        public async Task<Classification> UpdateAsync(Classification classification)
        {
            classification.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(classification);
            await _context.SaveChangesAsync(); // Save to database
            return classification;
        }

        public async Task DeleteAsync(Guid id)
        {
            var classification = await _dbSet.FindAsync(id);
            if (classification != null)
            {
                _dbSet.Remove(classification);
                await _context.SaveChangesAsync(); // Save to database
            }
        }

        public async Task<IEnumerable<Classification>> GetHighConfidenceClassificationsAsync(double minConfidence = 0.8)
        {
            return await _dbSet
                .Include(c => c.Document)
                .Include(c => c.DocumentType)
                .Where(c => c.ConfidenceScore >= minConfidence)
                .OrderByDescending(c => c.ClassifiedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Classification>> GetUnverifiedClassificationsAsync()
        {
            return await _dbSet
                .Include(c => c.Document)
                .Include(c => c.DocumentType)
                .Where(c => !c.IsManuallyVerified)
                .OrderByDescending(c => c.ClassifiedAt)
                .ToListAsync();
        }

        public async Task<bool> VerifyClassificationAsync(Guid id, string verifiedBy)
        {
            var classification = await _dbSet.FindAsync(id);
            if (classification != null)
            {
                classification.IsManuallyVerified = true;
                classification.VerifiedBy = verifiedBy;
                classification.VerifiedAt = DateTime.UtcNow;
                classification.UpdatedAt = DateTime.UtcNow;
                _dbSet.Update(classification);
                await _context.SaveChangesAsync(); // Save to database
                return true;
            }
            return false;
        }

        public async Task<IEnumerable<Classification>> GetByMethodAsync(ClassificationMethod method)
        {
            return await _dbSet
                .Include(c => c.Document)
                .Include(c => c.DocumentType)
                .Where(c => c.Method == method)
                .OrderByDescending(c => c.ClassifiedAt)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetClassificationStatsByModelAsync()
        {
            return await _dbSet
                .Where(c => c.AIModelUsed != null)
                .GroupBy(c => c.AIModelUsed)
                .Select(g => new { Model = g.Key!, Count = g.Count() })
                .ToDictionaryAsync(x => x.Model, x => x.Count);
        }
    }
}