using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocumentProcessor.Infrastructure.AI
{
    public class DatabaseProcessingQueue : IAIProcessingQueue
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DatabaseProcessingQueue> _logger;

        public DatabaseProcessingQueue(
            IServiceProvider serviceProvider,
            ILogger<DatabaseProcessingQueue> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<Guid> EnqueueDocumentAsync(Guid documentId, ProcessingPriority priority = ProcessingPriority.Normal)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            var queueItem = new ProcessingQueue
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ProcessingType = ProcessingType.CustomProcessing, // Default processing type
                Status = ProcessingStatus.Pending,
                Priority = (int)priority,
                RetryCount = 0,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await repository.AddAsync(queueItem);
            
            _logger.LogInformation("Document {DocumentId} enqueued with ID {QueueId} and priority {Priority}", documentId, result.Id, priority);
            return result.Id;
        }

        public async Task<ProcessingQueueStatus> GetQueueStatusAsync(Guid queueId)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            var item = await repository.GetByIdAsync(queueId);
            if (item == null)
            {
                throw new KeyNotFoundException($"Queue item with ID {queueId} not found");
            }

            return new ProcessingQueueStatus
            {
                QueueId = item.Id,
                DocumentId = item.DocumentId,
                State = MapProcessingStatus(item.Status),
                Priority = (ProcessingPriority)item.Priority,
                EnqueuedAt = item.CreatedAt,
                StartedAt = item.StartedAt,
                CompletedAt = item.CompletedAt,
                ErrorMessage = item.ErrorMessage,
                RetryCount = item.RetryCount,
                Metadata = new Dictionary<string, object>()
            };
        }

        public async Task CancelProcessingAsync(Guid queueId)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            var item = await repository.GetByIdAsync(queueId);
            if (item != null)
            {
                if (item.Status == ProcessingStatus.Pending || item.Status == ProcessingStatus.Retrying)
                {
                    item.Status = ProcessingStatus.Cancelled;
                    item.CompletedAt = DateTime.UtcNow;
                    item.UpdatedAt = DateTime.UtcNow;
                    await repository.UpdateAsync(item);
                    _logger.LogInformation("Processing cancelled for queue item {QueueId}", queueId);
                }
                else
                {
                    _logger.LogWarning("Cannot cancel queue item {QueueId} in state {Status}", queueId, item.Status);
                }
            }
        }

        public async Task<IEnumerable<ProcessingQueueItem>> GetPendingItemsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            var pendingItems = await repository.GetPendingAsync(100);
            
            return pendingItems.Select(item => new ProcessingQueueItem
            {
                QueueId = item.Id,
                DocumentId = item.DocumentId,
                Priority = (ProcessingPriority)item.Priority,
                EnqueuedAt = item.CreatedAt,
                RetryCount = item.RetryCount,
                LastError = item.ErrorMessage
            });
        }

        public async Task<ProcessingQueueItem?> DequeueNextAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            // Get pending items ordered by priority and creation date
            var pendingItems = await repository.GetPendingAsync(1);
            var item = pendingItems.FirstOrDefault();
            
            if (item != null)
            {
                // Mark as in progress
                var processorId = $"Processor_{Environment.MachineName}_{Guid.NewGuid():N}";
                var started = await repository.StartProcessingAsync(item.Id, processorId);
                
                if (started)
                {
                    _logger.LogInformation("Dequeued item {QueueId} for processing", item.Id);
                    return new ProcessingQueueItem
                    {
                        QueueId = item.Id,
                        DocumentId = item.DocumentId,
                        Priority = (ProcessingPriority)item.Priority,
                        EnqueuedAt = item.CreatedAt,
                        RetryCount = item.RetryCount,
                        LastError = item.ErrorMessage
                    };
                }
            }
            
            return null;
        }

        // Helper method to map between entities
        private ProcessingState MapProcessingStatus(ProcessingStatus status)
        {
            return status switch
            {
                ProcessingStatus.Pending => ProcessingState.Queued,
                ProcessingStatus.InProgress => ProcessingState.Processing,
                ProcessingStatus.Completed => ProcessingState.Completed,
                ProcessingStatus.Failed => ProcessingState.Failed,
                ProcessingStatus.Cancelled => ProcessingState.Cancelled,
                ProcessingStatus.Retrying => ProcessingState.Retrying,
                _ => ProcessingState.Queued
            };
        }

        // Additional helper methods that the background service might use
        public async Task UpdateStatusAsync(Guid queueId, ProcessingState newState, string? errorMessage = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            var item = await repository.GetByIdAsync(queueId);
            if (item != null)
            {
                item.Status = MapProcessingState(newState);
                
                if (newState == ProcessingState.Completed || newState == ProcessingState.Failed || newState == ProcessingState.Cancelled)
                {
                    item.CompletedAt = DateTime.UtcNow;
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    item.ErrorMessage = errorMessage;
                }

                item.UpdatedAt = DateTime.UtcNow;
                await repository.UpdateAsync(item);
                
                _logger.LogInformation("Updated status for queue item {QueueId} to {NewState}", queueId, newState);
            }
        }

        public async Task CompleteProcessingAsync(Guid queueId, string? resultData = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            await repository.CompleteProcessingAsync(queueId, resultData);
        }

        public async Task FailProcessingAsync(Guid queueId, string errorMessage, string? errorDetails = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            await repository.FailProcessingAsync(queueId, errorMessage, errorDetails);
        }

        private ProcessingStatus MapProcessingState(ProcessingState state)
        {
            return state switch
            {
                ProcessingState.Queued => ProcessingStatus.Pending,
                ProcessingState.Processing => ProcessingStatus.InProgress,
                ProcessingState.Completed => ProcessingStatus.Completed,
                ProcessingState.Failed => ProcessingStatus.Failed,
                ProcessingState.Cancelled => ProcessingStatus.Cancelled,
                ProcessingState.Retrying => ProcessingStatus.Retrying,
                _ => ProcessingStatus.Pending
            };
        }

        /// <summary>
        /// Cleans up stuck processing queue items that have been in progress for too long
        /// </summary>
        public async Task CleanupStuckDocumentsAsync(TimeSpan maxProcessingTime)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IProcessingQueueRepository>();
            
            var cutoffTime = DateTime.UtcNow.Subtract(maxProcessingTime);
            
            var stuckItems = await repository.GetStuckItemsAsync((int)maxProcessingTime.TotalMinutes);
            
            foreach (var item in stuckItems)
            {
                _logger.LogWarning("Cleaning up stuck processing item {QueueId} for document {DocumentId} that has been processing since {StartTime}", 
                    item.Id, item.DocumentId, item.StartedAt);
                
                item.Status = ProcessingStatus.Failed;
                item.ErrorMessage = $"Processing timeout - exceeded maximum processing time of {maxProcessingTime.TotalMinutes} minutes";
                item.CompletedAt = DateTime.UtcNow;
                item.UpdatedAt = DateTime.UtcNow;
                
                await repository.UpdateAsync(item);
                
                // Also update the document status to failed
                using var docScope = _serviceProvider.CreateScope();
                var docRepository = docScope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var document = await docRepository.GetByIdAsync(item.DocumentId);
                if (document != null)
                {
                    document.Status = DocumentStatus.Failed;
                    document.ProcessedAt = DateTime.UtcNow;
                    document.UpdatedAt = DateTime.UtcNow;
                    await docRepository.UpdateAsync(document);
                }
            }
            
            if (stuckItems.Any())
            {
                _logger.LogInformation("Cleaned up {Count} stuck processing items", stuckItems.Count());
            }
        }
    }
}