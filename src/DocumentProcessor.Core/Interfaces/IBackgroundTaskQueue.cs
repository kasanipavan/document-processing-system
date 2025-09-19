using System;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IBackgroundTaskQueue
    {
        ValueTask QueueBackgroundWorkItemAsync(
            Func<CancellationToken, ValueTask> workItem,
            string? itemId = null,
            int priority = 0);

        ValueTask<Func<CancellationToken, ValueTask>?> DequeueAsync(
            CancellationToken cancellationToken);

        int Count { get; }
        
        bool TryGetStatus(string itemId, out BackgroundTaskStatus status);
        
        void UpdateStatus(string itemId, BackgroundTaskStatus status);
    }

    public enum BackgroundTaskStatus
    {
        Queued,
        Processing,
        Completed,
        Failed,
        Cancelled
    }

    public interface IBackgroundTaskItem
    {
        string Id { get; }
        int Priority { get; }
        DateTime QueuedAt { get; }
        BackgroundTaskStatus Status { get; set; }
        string? ErrorMessage { get; set; }
        int RetryCount { get; set; }
        int MaxRetries { get; }
    }
}