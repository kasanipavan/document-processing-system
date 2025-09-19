using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DocumentProcessor.Infrastructure.BackgroundTasks
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<BackgroundWorkItem> _queue;
        private readonly ILogger<BackgroundTaskQueue> _logger;
        private readonly ConcurrentDictionary<string, BackgroundTaskStatus> _taskStatuses;
        private readonly SemaphoreSlim _signal;
        private int _itemCount;

        public BackgroundTaskQueue(ILogger<BackgroundTaskQueue> logger, int capacity = 100)
        {
            _logger = logger;
            
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            
            _queue = Channel.CreateBounded<BackgroundWorkItem>(options);
            _taskStatuses = new ConcurrentDictionary<string, BackgroundTaskStatus>();
            _signal = new SemaphoreSlim(0);
            _itemCount = 0;
        }

        public int Count => _itemCount;

        public async ValueTask QueueBackgroundWorkItemAsync(
            Func<CancellationToken, ValueTask> workItem,
            string? itemId = null,
            int priority = 0)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            itemId ??= Guid.NewGuid().ToString();
            
            var backgroundWorkItem = new BackgroundWorkItem
            {
                Id = itemId,
                WorkItem = workItem,
                Priority = priority,
                QueuedAt = DateTime.UtcNow
            };

            await _queue.Writer.WriteAsync(backgroundWorkItem);
            Interlocked.Increment(ref _itemCount);
            
            _taskStatuses[itemId] = BackgroundTaskStatus.Queued;
            _logger.LogInformation("Queued background work item {ItemId} with priority {Priority}", 
                itemId, priority);
                
            _signal.Release();
        }

        public async ValueTask<Func<CancellationToken, ValueTask>?> DequeueAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                // Wait for signal that an item is available
                await _signal.WaitAsync(cancellationToken);
                
                if (await _queue.Reader.WaitToReadAsync(cancellationToken))
                {
                    if (_queue.Reader.TryRead(out var workItem))
                    {
                        Interlocked.Decrement(ref _itemCount);
                        
                        _taskStatuses[workItem.Id] = BackgroundTaskStatus.Processing;
                        _logger.LogInformation("Dequeued background work item {ItemId}", workItem.Id);
                        
                        // Wrap the work item to update status on completion
                        return async (ct) =>
                        {
                            try
                            {
                                await workItem.WorkItem(ct);
                                UpdateStatus(workItem.Id, BackgroundTaskStatus.Completed);
                            }
                            catch (OperationCanceledException)
                            {
                                UpdateStatus(workItem.Id, BackgroundTaskStatus.Cancelled);
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing background work item {ItemId}", 
                                    workItem.Id);
                                UpdateStatus(workItem.Id, BackgroundTaskStatus.Failed);
                                throw;
                            }
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }

            return null;
        }

        public bool TryGetStatus(string itemId, out BackgroundTaskStatus status)
        {
            return _taskStatuses.TryGetValue(itemId, out status);
        }

        public void UpdateStatus(string itemId, BackgroundTaskStatus status)
        {
            _taskStatuses[itemId] = status;
            _logger.LogInformation("Updated status for work item {ItemId} to {Status}", 
                itemId, status);
        }

        private class BackgroundWorkItem
        {
            public string Id { get; set; } = string.Empty;
            public Func<CancellationToken, ValueTask> WorkItem { get; set; } = null!;
            public int Priority { get; set; }
            public DateTime QueuedAt { get; set; }
        }
    }

    public class PriorityBackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly SortedList<int, Queue<BackgroundWorkItem>> _queues;
        private readonly ILogger<PriorityBackgroundTaskQueue> _logger;
        private readonly ConcurrentDictionary<string, BackgroundTaskStatus> _taskStatuses;
        private readonly SemaphoreSlim _signal;
        private readonly object _lock = new object();
        private int _itemCount;

        public PriorityBackgroundTaskQueue(ILogger<PriorityBackgroundTaskQueue> logger)
        {
            _logger = logger;
            _queues = new SortedList<int, Queue<BackgroundWorkItem>>(
                Comparer<int>.Create((x, y) => y.CompareTo(x))); // Higher priority first
            _taskStatuses = new ConcurrentDictionary<string, BackgroundTaskStatus>();
            _signal = new SemaphoreSlim(0);
            _itemCount = 0;
        }

        public int Count => _itemCount;

        public ValueTask QueueBackgroundWorkItemAsync(
            Func<CancellationToken, ValueTask> workItem,
            string? itemId = null,
            int priority = 0)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            itemId ??= Guid.NewGuid().ToString();
            
            var backgroundWorkItem = new BackgroundWorkItem
            {
                Id = itemId,
                WorkItem = workItem,
                Priority = priority,
                QueuedAt = DateTime.UtcNow
            };

            lock (_lock)
            {
                if (!_queues.TryGetValue(priority, out var queue))
                {
                    queue = new Queue<BackgroundWorkItem>();
                    _queues[priority] = queue;
                }
                
                queue.Enqueue(backgroundWorkItem);
                Interlocked.Increment(ref _itemCount);
            }
            
            _taskStatuses[itemId] = BackgroundTaskStatus.Queued;
            _logger.LogInformation("Queued background work item {ItemId} with priority {Priority}", 
                itemId, priority);
                
            _signal.Release();
            
            return ValueTask.CompletedTask;
        }

        public async ValueTask<Func<CancellationToken, ValueTask>?> DequeueAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                await _signal.WaitAsync(cancellationToken);
                
                BackgroundWorkItem? workItem = null;
                
                lock (_lock)
                {
                    foreach (var kvp in _queues)
                    {
                        var queue = kvp.Value;
                        if (queue.Count > 0)
                        {
                            workItem = queue.Dequeue();
                            Interlocked.Decrement(ref _itemCount);
                            
                            if (queue.Count == 0)
                            {
                                _queues.Remove(kvp.Key);
                            }
                            
                            break;
                        }
                    }
                }
                
                if (workItem != null)
                {
                    _taskStatuses[workItem.Id] = BackgroundTaskStatus.Processing;
                    _logger.LogInformation("Dequeued background work item {ItemId} with priority {Priority}", 
                        workItem.Id, workItem.Priority);
                    
                    // Wrap the work item to update status on completion
                    return async (ct) =>
                    {
                        try
                        {
                            await workItem.WorkItem(ct);
                            UpdateStatus(workItem.Id, BackgroundTaskStatus.Completed);
                        }
                        catch (OperationCanceledException)
                        {
                            UpdateStatus(workItem.Id, BackgroundTaskStatus.Cancelled);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing background work item {ItemId}", 
                                workItem.Id);
                            UpdateStatus(workItem.Id, BackgroundTaskStatus.Failed);
                            throw;
                        }
                    };
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }

            return null;
        }

        public bool TryGetStatus(string itemId, out BackgroundTaskStatus status)
        {
            return _taskStatuses.TryGetValue(itemId, out status);
        }

        public void UpdateStatus(string itemId, BackgroundTaskStatus status)
        {
            _taskStatuses[itemId] = status;
            _logger.LogInformation("Updated status for work item {ItemId} to {Status}", 
                itemId, status);
        }

        private class BackgroundWorkItem
        {
            public string Id { get; set; } = string.Empty;
            public Func<CancellationToken, ValueTask> WorkItem { get; set; } = null!;
            public int Priority { get; set; }
            public DateTime QueuedAt { get; set; }
        }
    }
}