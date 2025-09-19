using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Infrastructure.Data;

namespace DocumentProcessor.Infrastructure.Repositories
{
    public class ProcessingQueueRepository : RepositoryBase<ProcessingQueue>, IProcessingQueueRepository
    {
        public ProcessingQueueRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<ProcessingQueue?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(pq => pq.Document)
                    .ThenInclude(d => d.DocumentType)
                .FirstOrDefaultAsync(pq => pq.Id == id);
        }

        public async Task<IEnumerable<ProcessingQueue>> GetPendingAsync(int limit = 100)
        {
            return await _dbSet
                .Include(pq => pq.Document)
                    .ThenInclude(d => d.DocumentType)
                .Where(pq => pq.Status == ProcessingStatus.Pending || 
                            (pq.Status == ProcessingStatus.Retrying && pq.NextRetryAt <= DateTime.UtcNow))
                .OrderByDescending(pq => pq.Priority)
                .ThenBy(pq => pq.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProcessingQueue>> GetByStatusAsync(ProcessingStatus status)
        {
            return await _dbSet
                .Include(pq => pq.Document)
                    .ThenInclude(d => d.DocumentType)
                .Where(pq => pq.Status == status)
                .OrderByDescending(pq => pq.Priority)
                .ThenBy(pq => pq.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProcessingQueue>> GetByDocumentIdAsync(Guid documentId)
        {
            return await _dbSet
                .Include(pq => pq.Document)
                .Where(pq => pq.DocumentId == documentId)
                .OrderByDescending(pq => pq.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProcessingQueue> AddAsync(ProcessingQueue item)
        {
            if (item.Id == Guid.Empty)
                item.Id = Guid.NewGuid();

            item.Status = ProcessingStatus.Pending;
            item.CreatedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            await _dbSet.AddAsync(item);
            await _context.SaveChangesAsync(); // Save to database
            return item;
        }

        public async Task<ProcessingQueue> UpdateAsync(ProcessingQueue item)
        {
            item.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(item);
            await _context.SaveChangesAsync(); // Save to database
            return item;
        }

        public async Task DeleteAsync(Guid id)
        {
            var item = await _dbSet.FindAsync(id);
            if (item != null)
            {
                _dbSet.Remove(item);
                await _context.SaveChangesAsync(); // Save to database
            }
        }

        public async Task<int> GetQueueLengthAsync()
        {
            return await _dbSet
                .CountAsync(pq => pq.Status == ProcessingStatus.Pending || 
                                 pq.Status == ProcessingStatus.InProgress ||
                                 pq.Status == ProcessingStatus.Retrying);
        }

        public async Task<bool> StartProcessingAsync(Guid id, string processorId)
        {
            var item = await _dbSet.FindAsync(id);
            if (item != null && item.Status == ProcessingStatus.Pending)
            {
                item.Status = ProcessingStatus.InProgress;
                item.StartedAt = DateTime.UtcNow;
                item.ProcessorId = processorId;
                item.UpdatedAt = DateTime.UtcNow;
                _dbSet.Update(item);
                await _context.SaveChangesAsync(); // Save to database
                return true;
            }
            return false;
        }

        public async Task<bool> CompleteProcessingAsync(Guid id, string? resultData = null)
        {
            var item = await _dbSet.FindAsync(id);
            if (item != null && item.Status == ProcessingStatus.InProgress)
            {
                item.Status = ProcessingStatus.Completed;
                item.CompletedAt = DateTime.UtcNow;
                item.ResultData = resultData;
                item.UpdatedAt = DateTime.UtcNow;
                _dbSet.Update(item);
                await _context.SaveChangesAsync(); // Save to database
                return true;
            }
            return false;
        }

        public async Task<bool> FailProcessingAsync(Guid id, string errorMessage, string? errorDetails = null)
        {
            var item = await _dbSet.FindAsync(id);
            if (item != null)
            {
                item.RetryCount++;
                
                if (item.RetryCount < item.MaxRetries)
                {
                    item.Status = ProcessingStatus.Retrying;
                    // Exponential backoff: 1 min, 2 min, 4 min, etc.
                    var delayMinutes = Math.Pow(2, item.RetryCount - 1);
                    item.NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
                }
                else
                {
                    item.Status = ProcessingStatus.Failed;
                    item.CompletedAt = DateTime.UtcNow;
                }

                item.ErrorMessage = errorMessage;
                item.ErrorDetails = errorDetails;
                item.UpdatedAt = DateTime.UtcNow;
                _dbSet.Update(item);
                await _context.SaveChangesAsync(); // Save to database
                return true;
            }
            return false;
        }

        public async Task<Dictionary<ProcessingStatus, int>> GetStatusCountsAsync()
        {
            return await _dbSet
                .GroupBy(pq => pq.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<Dictionary<ProcessingType, int>> GetProcessingTypeCountsAsync()
        {
            return await _dbSet
                .Where(pq => pq.Status == ProcessingStatus.Pending || pq.Status == ProcessingStatus.InProgress)
                .GroupBy(pq => pq.ProcessingType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count);
        }

        public async Task<IEnumerable<ProcessingQueue>> GetStuckItemsAsync(int minutesThreshold = 30)
        {
            var thresholdTime = DateTime.UtcNow.AddMinutes(-minutesThreshold);
            return await _dbSet
                .Include(pq => pq.Document)
                .Where(pq => pq.Status == ProcessingStatus.InProgress && 
                           pq.StartedAt.HasValue && 
                           pq.StartedAt.Value < thresholdTime)
                .OrderBy(pq => pq.StartedAt)
                .ToListAsync();
        }
    }
}