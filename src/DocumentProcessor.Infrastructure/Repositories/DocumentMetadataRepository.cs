using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Infrastructure.Data;
using System.Text.Json;

namespace DocumentProcessor.Infrastructure.Repositories
{
    public class DocumentMetadataRepository : RepositoryBase<DocumentMetadata>, IDocumentMetadataRepository
    {
        public DocumentMetadataRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<DocumentMetadata?> GetByIdAsync(Guid id)
        {
            return await _dbSet
                .Include(m => m.Document)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<DocumentMetadata?> GetByDocumentIdAsync(Guid documentId)
        {
            return await _dbSet
                .Include(m => m.Document)
                .FirstOrDefaultAsync(m => m.DocumentId == documentId);
        }

        public async Task<DocumentMetadata> AddAsync(DocumentMetadata metadata)
        {
            if (metadata.Id == Guid.Empty)
                metadata.Id = Guid.NewGuid();

            metadata.CreatedAt = DateTime.UtcNow;
            metadata.UpdatedAt = DateTime.UtcNow;

            await _dbSet.AddAsync(metadata);
            await _context.SaveChangesAsync(); // Save to database
            return metadata;
        }

        public async Task<DocumentMetadata> UpdateAsync(DocumentMetadata metadata)
        {
            metadata.UpdatedAt = DateTime.UtcNow;
            
            // For owned entities stored as JSON, we need to force EF to detect changes
            // by detaching and re-attaching the entity
            var entry = _context.Entry(metadata);
            if (entry.State == EntityState.Detached)
            {
                _context.Attach(metadata);
            }
            
            // Mark the entire entity as modified
            entry.State = EntityState.Modified;
            
            await _context.SaveChangesAsync(); // Save to database
            return metadata;
        }

        public async Task DeleteAsync(Guid id)
        {
            var metadata = await _dbSet.FindAsync(id);
            if (metadata != null)
            {
                _dbSet.Remove(metadata);
                await _context.SaveChangesAsync(); // Save to database
            }
        }

        public async Task<IEnumerable<DocumentMetadata>> GetAllAsync()
        {
            return await _dbSet
                .Include(m => m.Document)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<DocumentMetadata>> SearchByKeywordsAsync(string keywords)
        {
            var keywordList = keywords.ToLower().Split(',', ' ')
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .ToList();

            if (!keywordList.Any())
                return new List<DocumentMetadata>();

            var query = _dbSet.Include(m => m.Document).AsQueryable();

            foreach (var keyword in keywordList)
            {
                query = query.Where(m => 
                    (m.Keywords != null && m.Keywords.ToLower().Contains(keyword)) ||
                    (m.Title != null && m.Title.ToLower().Contains(keyword)) ||
                    (m.Subject != null && m.Subject.ToLower().Contains(keyword)) ||
                    (m.Author != null && m.Author.ToLower().Contains(keyword)));
            }

            return await query.ToListAsync();
        }

        public async Task<IEnumerable<DocumentMetadata>> GetByAuthorAsync(string author)
        {
            return await _dbSet
                .Include(m => m.Document)
                .Where(m => m.Author != null && m.Author.ToLower().Contains(author.ToLower()))
                .OrderByDescending(m => m.CreationDate ?? m.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<DocumentMetadata>> GetByLanguageAsync(string language)
        {
            return await _dbSet
                .Include(m => m.Document)
                .Where(m => m.Language == language)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<DocumentMetadata>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbSet
                .Include(m => m.Document)
                .Where(m => m.CreationDate != null && 
                           m.CreationDate >= startDate && 
                           m.CreationDate <= endDate)
                .OrderByDescending(m => m.CreationDate)
                .ToListAsync();
        }

        public async Task<bool> AddTagAsync(Guid metadataId, string key, string value)
        {
            // Use tracking query to ensure EF Core tracks the entity
            var metadata = await _dbSet
                .Where(m => m.Id == metadataId)
                .FirstOrDefaultAsync();
                
            if (metadata != null)
            {
                // Ensure Tags is initialized
                if (metadata.Tags == null)
                {
                    metadata.Tags = new Dictionary<string, string>();
                }
                
                // Create a new dictionary to force change detection
                var newTags = new Dictionary<string, string>(metadata.Tags);
                newTags[key] = value;
                metadata.Tags = newTags;
                metadata.UpdatedAt = DateTime.UtcNow;
                
                // Force EF Core to detect the change
                _context.Update(metadata);
                
                var savedCount = await _context.SaveChangesAsync();
                return savedCount > 0;
            }
            return false;
        }

        public async Task<bool> RemoveTagAsync(Guid metadataId, string key)
        {
            // Use tracking query to ensure EF Core tracks the entity
            var metadata = await _dbSet
                .Where(m => m.Id == metadataId)
                .FirstOrDefaultAsync();
                
            if (metadata != null && metadata.Tags != null && metadata.Tags.ContainsKey(key))
            {
                // Create a new dictionary to force change detection
                var newTags = new Dictionary<string, string>(metadata.Tags);
                newTags.Remove(key);
                metadata.Tags = newTags;
                metadata.UpdatedAt = DateTime.UtcNow;
                
                // Force EF Core to detect the change
                _context.Update(metadata);
                
                var savedCount = await _context.SaveChangesAsync();
                return savedCount > 0;
            }
            return false;
        }

        public async Task<Dictionary<string, int>> GetTopAuthorsAsync(int limit = 10)
        {
            return await _dbSet
                .Where(m => m.Author != null)
                .GroupBy(m => m.Author)
                .Select(g => new { Author = g.Key!, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(limit)
                .ToDictionaryAsync(x => x.Author, x => x.Count);
        }

        public async Task<Dictionary<string, int>> GetLanguageDistributionAsync()
        {
            return await _dbSet
                .Where(m => m.Language != null)
                .GroupBy(m => m.Language)
                .Select(g => new { Language = g.Key!, Count = g.Count() })
                .ToDictionaryAsync(x => x.Language, x => x.Count);
        }
    }
}