using Microsoft.AspNetCore.SignalR;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Web.Hubs;

namespace DocumentProcessor.Web.Services
{
    public interface INotificationService
    {
        Task NotifyDocumentStatusChange(Guid documentId, DocumentStatus status, string? message = null);
        Task NotifyProcessingProgress(Guid documentId, int percentage, string currentStep);
        Task NotifyProcessingComplete(Guid documentId, bool success, string? summary = null);
        Task NotifySystemEvent(string type, string message, string severity = "info");
        Task NotifyQueueUpdate(int queueLength, int processingCount);
    }

    public class NotificationService : INotificationService
    {
        private readonly IHubContext<DocumentProcessingHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IHubContext<DocumentProcessingHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyDocumentStatusChange(Guid documentId, DocumentStatus status, string? message = null)
        {
            try
            {
                await DocumentProcessingHub.Notifications.SendDocumentStatusUpdate(_hubContext, documentId, status, message);
                _logger.LogInformation("Sent status update for document {DocumentId}: {Status}", documentId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update for document {DocumentId}", documentId);
            }
        }

        public async Task NotifyProcessingProgress(Guid documentId, int percentage, string currentStep)
        {
            try
            {
                await DocumentProcessingHub.Notifications.SendProcessingProgress(_hubContext, documentId, percentage, currentStep);
                _logger.LogDebug("Sent progress update for document {DocumentId}: {Percentage}% - {CurrentStep}", documentId, percentage, currentStep);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send progress update for document {DocumentId}", documentId);
            }
        }

        public async Task NotifyProcessingComplete(Guid documentId, bool success, string? summary = null)
        {
            try
            {
                await DocumentProcessingHub.Notifications.SendProcessingComplete(_hubContext, documentId, success, summary);
                _logger.LogInformation("Sent completion notification for document {DocumentId}: Success={Success}", documentId, success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send completion notification for document {DocumentId}", documentId);
            }
        }

        public async Task NotifySystemEvent(string type, string message, string severity = "info")
        {
            try
            {
                await DocumentProcessingHub.Notifications.SendSystemNotification(_hubContext, type, message, severity);
                _logger.LogInformation("Sent system notification: {Type} - {Message} (Severity: {Severity})", type, message, severity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send system notification");
            }
        }

        public async Task NotifyQueueUpdate(int queueLength, int processingCount)
        {
            try
            {
                await DocumentProcessingHub.Notifications.SendQueueUpdate(_hubContext, queueLength, processingCount);
                _logger.LogDebug("Sent queue update: Length={QueueLength}, Processing={ProcessingCount}", queueLength, processingCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send queue update");
            }
        }
    }
}