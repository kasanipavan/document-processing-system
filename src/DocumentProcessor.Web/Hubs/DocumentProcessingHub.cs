using Microsoft.AspNetCore.SignalR;
using DocumentProcessor.Core.Entities;

namespace DocumentProcessor.Web.Hubs;

public class DocumentProcessingHub(ILogger<DocumentProcessingHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Subscribe to document updates
    public async Task SubscribeToDocument(string documentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"doc-{documentId}");
        logger.LogInformation("Client {ConnectionId} subscribed to document {DocumentId}", Context.ConnectionId, documentId);
    }

    // Unsubscribe from document updates
    public async Task UnsubscribeFromDocument(string documentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"doc-{documentId}");
        logger.LogInformation("Client {ConnectionId} unsubscribed from document {DocumentId}", Context.ConnectionId, documentId);
    }

    // Subscribe to all document updates
    public async Task SubscribeToAllDocuments()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-documents");
        logger.LogInformation("Client {ConnectionId} subscribed to all documents", Context.ConnectionId);
    }

    // Server-side methods to send notifications (called from services)
    public static class Notifications
    {
        public static async Task SendDocumentStatusUpdate(IHubContext<DocumentProcessingHub> hubContext, 
            Guid documentId, DocumentStatus status, string? message = null)
        {
            await hubContext.Clients.Group($"doc-{documentId}").SendAsync("DocumentStatusUpdated", new
            {
                DocumentId = documentId,
                Status = status.ToString(),
                Message = message,
                Timestamp = DateTime.UtcNow
            });

            await hubContext.Clients.Group("all-documents").SendAsync("DocumentStatusUpdated", new
            {
                DocumentId = documentId,
                Status = status.ToString(),
                Message = message,
                Timestamp = DateTime.UtcNow
            });
        }

        public static async Task SendProcessingProgress(IHubContext<DocumentProcessingHub> hubContext,
            Guid documentId, int percentage, string currentStep)
        {
            await hubContext.Clients.Group($"doc-{documentId}").SendAsync("ProcessingProgress", new
            {
                DocumentId = documentId,
                Percentage = percentage,
                CurrentStep = currentStep,
                Timestamp = DateTime.UtcNow
            });
        }

        public static async Task SendProcessingComplete(IHubContext<DocumentProcessingHub> hubContext,
            Guid documentId, bool success, string? summary = null)
        {
            await hubContext.Clients.Group($"doc-{documentId}").SendAsync("ProcessingComplete", new
            {
                DocumentId = documentId,
                Success = success,
                Summary = summary,
                Timestamp = DateTime.UtcNow
            });

            await hubContext.Clients.Group("all-documents").SendAsync("ProcessingComplete", new
            {
                DocumentId = documentId,
                Success = success,
                Summary = summary,
                Timestamp = DateTime.UtcNow
            });
        }

        public static async Task SendSystemNotification(IHubContext<DocumentProcessingHub> hubContext,
            string type, string message, string severity = "info")
        {
            await hubContext.Clients.All.SendAsync("SystemNotification", new
            {
                Type = type,
                Message = message,
                Severity = severity,
                Timestamp = DateTime.UtcNow
            });
        }

        public static async Task SendQueueUpdate(IHubContext<DocumentProcessingHub> hubContext,
            int queueLength, int processingCount)
        {
            await hubContext.Clients.Group("all-documents").SendAsync("QueueUpdate", new
            {
                QueueLength = queueLength,
                ProcessingCount = processingCount,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}