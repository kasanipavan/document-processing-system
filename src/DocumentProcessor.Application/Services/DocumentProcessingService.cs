using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json;

namespace DocumentProcessor.Application.Services;

public class DocumentProcessingService(
    IAIProcessorFactory aiProcessorFactory,
    IAIProcessingQueue processingQueue,
    IDocumentSourceProvider documentSource,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<DocumentProcessingService> logger)
    : IDocumentProcessingService
{
    public async Task<Guid> QueueDocumentForProcessingAsync(Guid documentId, ProcessingPriority priority = ProcessingPriority.Normal)
    {
        // Use a separate service scope to avoid tracking conflicts
        using var scope = serviceScopeFactory.CreateScope();
        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        
        var document = await documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            throw new ArgumentException($"Document with ID {documentId} not found");
        }

        // Update document status using normal EF tracking in separate scope
        document.Status = DocumentStatus.Queued;
        document.UpdatedAt = DateTime.UtcNow;
        await documentRepository.UpdateAsync(document);

        // Add to processing queue
        var queueId = await processingQueue.EnqueueDocumentAsync(documentId, priority);
            
        logger.LogInformation("Document {DocumentId} queued for processing with queue ID {QueueId}", documentId, queueId);
        return queueId;
    }

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(Guid documentId, AIProviderType? providerType = null)
    {
        // Use a separate service scope to avoid tracking conflicts
        using var scope = serviceScopeFactory.CreateScope();
        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        
        var document = await documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            throw new ArgumentException($"Document with ID {documentId} not found");
        }

        var result = new DocumentProcessingResult
        {
            DocumentId = documentId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Update document status using normal EF tracking
            document.Status = DocumentStatus.Processing;
            document.UpdatedAt = DateTime.UtcNow;
            await documentRepository.UpdateAsync(document);

            // Get AI processor
            var processor = providerType.HasValue 
                ? aiProcessorFactory.CreateProcessor(providerType.Value)
                : aiProcessorFactory.GetDefaultProcessor();

            logger.LogInformation("Processing document {DocumentId} with {ProviderName}", documentId, processor.ProviderName);

            // Get document content - create separate streams for each AI operation
            // to avoid "Cannot access a closed file" errors
            await using var classificationStream = await documentSource.GetDocumentStreamAsync(document.StoragePath);
            await using var extractionStream = await documentSource.GetDocumentStreamAsync(document.StoragePath);
            await using var summaryStream = await documentSource.GetDocumentStreamAsync(document.StoragePath);
            await using var intentStream = await documentSource.GetDocumentStreamAsync(document.StoragePath);

            // Process with AI - each operation gets its own stream
            var classificationTask = processor.ClassifyDocumentAsync(document, classificationStream);
            var extractionTask = processor.ExtractDataAsync(document, extractionStream);
            var summaryTask = processor.GenerateSummaryAsync(document, summaryStream);
            var intentTask = processor.DetectIntentAsync(document, intentStream);

            // Wait for all tasks
            await Task.WhenAll(classificationTask, extractionTask, summaryTask, intentTask);

            // Get results
            result.Classification = await classificationTask;
            result.Extraction = await extractionTask;
            result.Summary = await summaryTask;
            result.Intent = await intentTask;

            // Update document with processing results using the same tracked instance
            document.Status = DocumentStatus.Processed;
            document.ProcessedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
                
            // Store the summary directly without prepending classification
            if (result.Summary != null && !string.IsNullOrEmpty(result.Summary.Summary))
            {
                document.Summary = result.Summary.Summary;
            }

            // Store extracted text from extraction results
            if (result.Extraction != null && result.Extraction.Entities != null && result.Extraction.Entities.Any())
            {
                // Combine all extracted entities into a text representation
                var extractedTexts = result.Extraction.Entities
                    .Select(e => $"{e.Type}: {e.Value}")
                    .ToList();
                document.ExtractedText = string.Join("; ", extractedTexts);
            }

            // Handle document type and classification
            if (result.Classification != null && !string.IsNullOrEmpty(result.Classification.PrimaryCategory))
            {
                // Look up or create DocumentType based on classification
                var documentType = await unitOfWork.DocumentTypes.GetByNameAsync(result.Classification.PrimaryCategory);
                if (documentType == null)
                {
                    documentType = new DocumentType
                    {
                        Id = Guid.NewGuid(),
                        Name = result.Classification.PrimaryCategory,
                        Description = $"Auto-generated type for {result.Classification.PrimaryCategory}",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await unitOfWork.DocumentTypes.AddAsync(documentType);
                }
                document.DocumentTypeId = documentType.Id;

                // Create classification record
                var classification = new Classification
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    DocumentTypeId = documentType.Id,
                    ConfidenceScore = result.Classification.CategoryConfidences?.FirstOrDefault().Value ?? 0.95,
                    ClassifiedAt = DateTime.UtcNow,
                    Method = ClassificationMethod.AI,
                    AIModelUsed = processor.ModelId,
                    AIResponse = JsonSerializer.Serialize(result.Classification),
                    ExtractedIntents = result.Intent != null ? JsonSerializer.Serialize(result.Intent) : null,
                    ExtractedEntities = result.Extraction?.Entities != null ? JsonSerializer.Serialize(result.Extraction.Entities) : null,
                    IsManuallyVerified = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await unitOfWork.Classifications.AddAsync(classification);

                // If we still don't have extracted text, add classification info
                if (string.IsNullOrEmpty(document.ExtractedText))
                {
                    document.ExtractedText = $"Classification: {result.Classification.PrimaryCategory}";
                    if (result.Classification.Tags.Any())
                    {
                        document.ExtractedText += $"; Tags: {string.Join(", ", result.Classification.Tags)}";
                    }
                }
            }

            // Create or update document metadata
            var existingMetadata = await unitOfWork.DocumentMetadata.GetByDocumentIdAsync(document.Id);
            if (existingMetadata == null)
            {
                var metadata = new DocumentMetadata
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Title = document.FileName,
                    Author = "System", // Default author
                    CreationDate = document.UploadedAt,
                    ModificationDate = DateTime.UtcNow,
                    Language = result.Summary?.Language ?? "en",
                    PageCount = 1, // Default page count
                    WordCount = document.ExtractedText?.Split(' ').Length ?? 0,
                    Keywords = result.Classification?.Tags != null ? string.Join(", ", result.Classification.Tags) : "",
                    Subject = result.Classification?.PrimaryCategory ?? "",
                    Tags = new Dictionary<string, string>(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Add extracted entities as tags
                if (result.Extraction?.Entities != null)
                {
                    foreach (var entity in result.Extraction.Entities.Take(10)) // Limit to 10 entities
                    {
                        metadata.Tags[entity.Type] = entity.Value;
                    }
                }

                await unitOfWork.DocumentMetadata.AddAsync(metadata);
            }
            else
            {
                // Update existing metadata
                existingMetadata.ModificationDate = DateTime.UtcNow;
                existingMetadata.Language = result.Summary?.Language ?? existingMetadata.Language;
                existingMetadata.WordCount = document.ExtractedText?.Split(' ').Length ?? existingMetadata.WordCount;
                existingMetadata.Keywords = result.Classification?.Tags != null ? string.Join(", ", result.Classification.Tags) : existingMetadata.Keywords;
                existingMetadata.Subject = result.Classification?.PrimaryCategory ?? existingMetadata.Subject;
                existingMetadata.UpdatedAt = DateTime.UtcNow;

                // Update tags with extracted entities
                if (result.Extraction?.Entities != null)
                {
                    foreach (var entity in result.Extraction.Entities.Take(10))
                    {
                        existingMetadata.Tags[entity.Type] = entity.Value;
                    }
                }

                await unitOfWork.DocumentMetadata.UpdateAsync(existingMetadata);
            }

            await documentRepository.UpdateAsync(document);
                
            // Save all changes through unit of work
            await unitOfWork.SaveChangesAsync();

            // Update any corresponding queue items to completed status
            await UpdateQueueItemsStatusAsync(documentId, true, null);

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
                
            logger.LogInformation("Successfully processed document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document {DocumentId}", documentId);
                
            document.Status = DocumentStatus.Failed;
            document.UpdatedAt = DateTime.UtcNow;
            await documentRepository.UpdateAsync(document);

            // Update any corresponding queue items to failed status
            await UpdateQueueItemsStatusAsync(documentId, false, ex.Message);

            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    public async Task<ProcessingQueueStatus> GetProcessingStatusAsync(Guid queueId)
    {
        return await processingQueue.GetQueueStatusAsync(queueId);
    }

    public async Task<IEnumerable<ProcessingQueueItem>> GetPendingDocumentsAsync()
    {
        return await processingQueue.GetPendingItemsAsync();
    }

    public async Task CancelProcessingAsync(Guid queueId)
    {
        await processingQueue.CancelProcessingAsync(queueId);
    }

    public async Task<ProcessingCost> EstimateProcessingCostAsync(Guid documentId, AIProviderType? providerType = null)
    {
        // Use a separate service scope to avoid tracking conflicts
        using var scope = serviceScopeFactory.CreateScope();
        var documentRepository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        
        var document = await documentRepository.GetByIdAsync(documentId);
        if (document == null)
        {
            throw new ArgumentException($"Document with ID {documentId} not found");
        }

        var processor = providerType.HasValue 
            ? aiProcessorFactory.CreateProcessor(providerType.Value)
            : aiProcessorFactory.GetDefaultProcessor();

        return await processor.EstimateCostAsync(document.FileSize, document.ContentType);
    }

    private async Task UpdateQueueItemsStatusAsync(Guid documentId, bool success, string? errorMessage)
    {
        try
        {
            // Use a separate scope to avoid tracking conflicts
            using var scope = serviceScopeFactory.CreateScope();
            var processingQueueRepo = scope.ServiceProvider.GetService<IProcessingQueueRepository>();
            
            if (processingQueueRepo != null)
            {
                // Find all queue items for this document
                var queueItems = await processingQueueRepo.GetByDocumentIdAsync(documentId);
                
                foreach (var queueItem in queueItems)
                {
                    // Only update items that are still pending or in progress
                    if (queueItem.Status == ProcessingStatus.Pending || queueItem.Status == ProcessingStatus.InProgress)
                    {
                        if (success)
                        {
                            logger.LogInformation("Marking queue item {QueueItemId} as completed for document {DocumentId}", queueItem.Id, documentId);
                            await processingQueueRepo.CompleteProcessingAsync(queueItem.Id, "Processed by direct call");
                        }
                        else
                        {
                            logger.LogInformation("Marking queue item {QueueItemId} as failed for document {DocumentId}", queueItem.Id, documentId);
                            await processingQueueRepo.FailProcessingAsync(queueItem.Id, errorMessage ?? "Processing failed", null);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating queue items status for document {DocumentId}", documentId);
            // Don't throw - this is a secondary operation and shouldn't fail the main processing
        }
    }
}