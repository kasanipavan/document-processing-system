using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using DocumentProcessor.Core.Entities;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace DocumentProcessor.Infrastructure.AI;

/// <summary>
/// Amazon Bedrock implementation of the AI processor
/// </summary>
public class BedrockAIProcessor : IAIProcessor
{
    private readonly ILogger<BedrockAIProcessor> _logger;
    private readonly BedrockOptions _options;
    private readonly IAmazonBedrockRuntime _bedrockClient;
    private readonly DocumentContentExtractor _contentExtractor;

    public string ModelId => _options.ClassificationModelId;
    public string ProviderName => "Amazon Bedrock";

    [Obsolete("Obsolete")]
    public BedrockAIProcessor(
        ILogger<BedrockAIProcessor> logger,
        IOptions<BedrockOptions> options,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _options = options.Value;
            
        // Try to get DocumentContentExtractor from DI, or create a new instance
        var contentExtractorLogger = serviceProvider.GetService<ILogger<DocumentContentExtractor>>();
        _contentExtractor = serviceProvider.GetService<DocumentContentExtractor>() ??
                            new DocumentContentExtractor(contentExtractorLogger ??
                                                         new Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentContentExtractor>(),
                                                         serviceProvider);
            
        // Initialize Bedrock client
        var config = new AmazonBedrockRuntimeConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region)
        };

        // Use profile if specified (for development)
        if (!string.IsNullOrEmpty(_options.AwsProfile))
        {
            var credentials = new Amazon.Runtime.StoredProfileAWSCredentials(_options.AwsProfile);
            _bedrockClient = new AmazonBedrockRuntimeClient(credentials, config);
        }
        else
        {
            // Use default credentials (IAM role, environment variables, etc.)
            _bedrockClient = new AmazonBedrockRuntimeClient(config);
        }
    }

    public async Task<DocumentClassificationResult> ClassifyDocumentAsync(
        Document document, 
        Stream documentContent)
    {
        var startTime = DateTime.UtcNow;
            
        try
        {
            _logger.LogInformation("Classifying document {DocumentId} using Bedrock model: {Model}", 
                document.Id, _options.ClassificationModelId);

            // Extract content using the enhanced extractor
            var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
            string content = FormatExtractedContent(extractedContent);
                
            var prompt = BuildClassificationPrompt(content, document.FileName);
            var response = await InvokeModelAsync(
                _options.ClassificationModelId, 
                prompt, 
                CancellationToken.None);

            var result = ParseClassificationResponse(response);
            result.ProcessingTime = DateTime.UtcNow - startTime;
            return result;
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Model validation error for document {DocumentId}. Model: {Model}", document.Id, _options.ClassificationModelId);
            return new DocumentClassificationResult
            {
                PrimaryCategory = "Error: Invalid Model",
                ProcessingNotes = $"Model validation error: {ex.Message}. Please check if the model ID '{_options.ClassificationModelId}' is correct and enabled in your AWS account.",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (AmazonBedrockRuntimeException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError(ex, "AWS Bedrock access denied for document {DocumentId}. Please check AWS credentials and Bedrock permissions.", document.Id);
            return new DocumentClassificationResult
            {
                PrimaryCategory = "Error: AWS Configuration Required",
                ProcessingNotes = "AWS Bedrock access denied. Please configure: 1) AWS credentials (via AWS CLI or environment variables), 2) Enable Bedrock and the specific model in your AWS account, 3) Ensure the configured region supports Bedrock",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (Amazon.Runtime.AmazonServiceException ex)
        {
            _logger.LogError(ex, "AWS service error while classifying document {DocumentId}. Error Code: {ErrorCode}, Status: {StatusCode}, Request ID: {RequestId}", 
                document.Id, ex.ErrorCode, ex.StatusCode, ex.RequestId);
            
            string errorDetail = $"AWS Error: {ex.Message}";
            if (!string.IsNullOrEmpty(ex.ErrorCode))
            {
                errorDetail += $" (Error Code: {ex.ErrorCode})";
            }
            if (ex.StatusCode != System.Net.HttpStatusCode.OK)
            {
                errorDetail += $" (Status: {ex.StatusCode})";
            }
            
            return new DocumentClassificationResult
            {
                PrimaryCategory = "Error: AWS Service Issue",
                ProcessingNotes = errorDetail + ". Please check AWS credentials, permissions, and Bedrock model access.",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error classifying document {DocumentId} with Bedrock", document.Id);
            return new DocumentClassificationResult
            {
                PrimaryCategory = "Error: Processing Failed",
                ProcessingNotes = $"Error: {ex.Message}",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<DocumentExtractionResult> ExtractDataAsync(
        Document document, 
        Stream documentContent)
    {
        var startTime = DateTime.UtcNow;
            
        try
        {
            _logger.LogInformation("Extracting data from document {DocumentId} using Bedrock model: {Model}", 
                document.Id, _options.ExtractionModelId);

            var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
            string content = FormatExtractedContent(extractedContent);
                
            var prompt = BuildExtractionPrompt(content, document.DocumentType?.Name ?? "Document");
            var response = await InvokeModelAsync(
                _options.ExtractionModelId, 
                prompt, 
                CancellationToken.None);

            var result = ParseExtractionResponse(response);
            result.ProcessingTime = DateTime.UtcNow - startTime;
            return result;
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Model validation error for document {DocumentId}. Model: {Model}", document.Id, _options.ExtractionModelId);
            var result = new DocumentExtractionResult
            {
                ProcessingTime = DateTime.UtcNow - startTime
            };
            result.Metadata["error"] = $"Model validation error: {ex.Message}";
            return result;
        }
        catch (AmazonBedrockRuntimeException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError(ex, "AWS Bedrock access denied for document {DocumentId}", document.Id);
            var result = new DocumentExtractionResult
            {
                ProcessingTime = DateTime.UtcNow - startTime
            };
            result.Metadata["error"] = "AWS Bedrock access denied - check credentials and model permissions";
            return result;
        }
        catch (Amazon.Runtime.AmazonServiceException ex)
        {
            _logger.LogError(ex, "AWS service error while extracting from document {DocumentId}", document.Id);
            var result = new DocumentExtractionResult
            {
                ProcessingTime = DateTime.UtcNow - startTime
            };
            result.Metadata["error"] = $"AWS Error: {ex.Message}";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error extracting data from document {DocumentId}", document.Id);
            var result = new DocumentExtractionResult
            {
                ProcessingTime = DateTime.UtcNow - startTime
            };
            result.Metadata["error"] = ex.Message;
            return result;
        }
    }

    public async Task<DocumentSummaryResult> GenerateSummaryAsync(
        Document document,
        Stream documentContent)
    {
        var startTime = DateTime.UtcNow;
            
        try
        {
            _logger.LogInformation("Summarizing document {DocumentId} using Bedrock model: {Model}",
                document.Id, _options.SummarizationModelId);

            var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
            string content = FormatExtractedContent(extractedContent);
                
            // Use maxTokens from configuration instead of hardcoded value
            var prompt = BuildSummarizationPrompt(content, 1000, document.FileName, document.Id.ToString());
            var response = await InvokeModelAsync(
                _options.SummarizationModelId,
                prompt,
                CancellationToken.None);

            var result = ParseSummaryResponse(response);
            result.ProcessingTime = DateTime.UtcNow - startTime;
            return result;
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Model validation error for document {DocumentId}. Model: {Model}", document.Id, _options.SummarizationModelId);
            return new DocumentSummaryResult
            {
                Summary = $"Model validation error: {ex.Message}. Please check if the model ID '{_options.SummarizationModelId}' is correct and enabled.",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (AmazonBedrockRuntimeException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError(ex, "AWS Bedrock access denied for document {DocumentId}. Please check AWS credentials and Bedrock permissions.", document.Id);
            return new DocumentSummaryResult
            {
                Summary = "AWS Bedrock Configuration Required: Please ensure 1) AWS credentials are configured (AWS CLI or environment variables), 2) Amazon Bedrock and the specific model are enabled in your AWS account, 3) The configured region (check appsettings.json) supports Bedrock, 4) Your AWS account has permissions to invoke Bedrock models",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (Amazon.Runtime.AmazonServiceException ex)
        {
            _logger.LogError(ex, "AWS service error while summarizing document {DocumentId}. Error Code: {ErrorCode}, Status: {StatusCode}, Request ID: {RequestId}", 
                document.Id, ex.ErrorCode, ex.StatusCode, ex.RequestId);
            
            string errorDetail = $"AWS Service Error: {ex.Message}";
            if (!string.IsNullOrEmpty(ex.ErrorCode))
            {
                errorDetail += $" (Error Code: {ex.ErrorCode})";
            }
            if (ex.StatusCode != System.Net.HttpStatusCode.OK)
            {
                errorDetail += $" (Status: {ex.StatusCode})";
            }
            
            return new DocumentSummaryResult
            {
                Summary = errorDetail + ". Please check AWS credentials, permissions, and Bedrock model access.",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error summarizing document {DocumentId} with Bedrock", document.Id);
            return new DocumentSummaryResult
            {
                Summary = $"Error generating summary: {ex.Message}",
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<DocumentIntentResult> DetectIntentAsync(
        Document document, 
        Stream documentContent)
    {
        var startTime = DateTime.UtcNow;
            
        try
        {
            _logger.LogInformation("Detecting intent for document {DocumentId} using Bedrock model: {Model}", 
                document.Id, _options.IntentModelId);

            var extractedContent = await _contentExtractor.ExtractContentAsync(document, documentContent);
            string content = FormatExtractedContent(extractedContent);
                
            var prompt = BuildIntentPrompt(content);
            var response = await InvokeModelAsync(
                _options.IntentModelId, 
                prompt, 
                CancellationToken.None);

            var result = ParseIntentResponse(response);
            result.ProcessingTime = DateTime.UtcNow - startTime;
            return result;
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, "Model validation error for document {DocumentId}. Model: {Model}", document.Id, _options.IntentModelId);
            return new DocumentIntentResult
            {
                PrimaryIntent = "Model Error",
                SuggestedAction = $"Check model configuration: {ex.Message}",
                Confidence = 0,
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (AmazonBedrockRuntimeException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogError(ex, "AWS Bedrock access denied for document {DocumentId}", document.Id);
            return new DocumentIntentResult
            {
                PrimaryIntent = "Configuration Required",
                SuggestedAction = "Configure AWS Bedrock access and model permissions",
                Confidence = 0,
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (Amazon.Runtime.AmazonServiceException ex)
        {
            _logger.LogError(ex, "AWS service error while detecting intent for document {DocumentId}", document.Id);
            return new DocumentIntentResult
            {
                PrimaryIntent = "Service Error",
                SuggestedAction = $"Check AWS status: {ex.Message}",
                Confidence = 0,
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error detecting intent for document {DocumentId}", document.Id);
            return new DocumentIntentResult
            {
                PrimaryIntent = "Processing Error",
                SuggestedAction = "Check application logs",
                Confidence = 0,
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task<ProcessingCost> EstimateCostAsync(long fileSize, string contentType)
    {
        // Estimate tokens based on file size (rough approximation)
        // Assuming ~4 characters per token and some overhead for prompts
        var estimatedCharacters = fileSize * 0.8; // Assuming 80% of file is text
        var estimatedTokens = (int)(estimatedCharacters / 4) + 500; // Add 500 for prompt overhead
            
        // Claude 3 Haiku pricing: $0.25 per million input tokens, $1.25 per million output tokens
        // Using average for estimation
        var costPerMillionTokens = 0.75m;
        var estimatedCost = (estimatedTokens / 1000000m) * costPerMillionTokens;
            
        return await Task.FromResult(new ProcessingCost
        {
            EstimatedCost = estimatedCost,
            Currency = "USD",
            EstimatedTokens = estimatedTokens,
            ModelUsed = _options.ClassificationModelId,
            PricingTier = "Pay-as-you-go"
        });
    }

    private async Task<string> ReadStreamContent(Stream stream)
    {
        // Reset stream position if possible
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
            
        // Truncate content if it's too long for the model
        // Claude models have different token limits, using conservative character limit
        const int maxCharacters = 50000; // Approximately 12,500 tokens
        if (content.Length > maxCharacters)
        {
            _logger.LogWarning("Document content truncated from {OriginalLength} to {MaxCharacters} characters", content.Length, maxCharacters);
            content = content.Substring(0, maxCharacters) + "\n\n[Content truncated due to length...]";
        }
            
        return content;
    }

    private async Task<string> InvokeModelAsync(
        string modelId,
        string prompt,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;
        var delay = _options.RetryDelayMilliseconds;

        while (retryCount < _options.MaxRetries)
        {
            try
            {
                // Create message using the Converse API
                var message = new Message
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock>
                    {
                        new ContentBlock
                        {
                            Text = prompt
                        }
                    }
                };

                // Create the Converse request
                var request = new ConverseRequest
                {
                    ModelId = modelId,
                    Messages = new List<Message> { message },
                    InferenceConfig = new InferenceConfiguration
                    {
                        MaxTokens = _options.MaxTokens,
                        Temperature = (float)_options.Temperature,
                        TopP = (float)_options.TopP
                    }
                };

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("Bedrock Converse request for model {ModelId}: {Prompt}",
                        modelId, prompt.Substring(0, Math.Min(prompt.Length, 500)));
                }

                // Call the Converse API
                var response = await _bedrockClient.ConverseAsync(request, cancellationToken);

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogDebug("Bedrock Converse response received with {TokenCount} output tokens",
                        response.Usage?.OutputTokens ?? 0);
                }

                // Extract text from the response
                if (response.Output?.Message?.Content != null && response.Output.Message.Content.Count > 0)
                {
                    var textContent = response.Output.Message.Content
                        .Where(c => c.Text != null)
                        .Select(c => c.Text)
                        .FirstOrDefault();

                    return textContent ?? string.Empty;
                }

                return string.Empty;
            }
            catch (AmazonBedrockRuntimeException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                retryCount++;
                if (retryCount >= _options.MaxRetries)
                {
                    throw;
                }

                _logger.LogWarning("Bedrock API throttled, retrying in {Delay}ms", delay);
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }

        throw new InvalidOperationException($"Failed to invoke Bedrock model after {_options.MaxRetries} retries");
    }


    private string BuildClassificationPrompt(string content, string fileName)
    {
        // Content is already truncated in ReadStreamContent
        return $@"Analyze the following document and classify it.

Document name: {fileName}
Document content:
{content}

Respond with ONLY a valid JSON object (no additional text) containing:
- category: the document category (e.g., Invoice, Contract, Report, Letter, etc.)
- confidence: a number between 0 and 1
- tags: array of relevant tags

Example response:
{{""category"": ""Invoice"", ""confidence"": 0.95, ""tags"": [""financial"", ""billing""]}}";
    }

    private string BuildExtractionPrompt(string content, string documentType)
    {
        // Content is already truncated in ReadStreamContent
        return $@"Extract key entities from this {documentType} document.

Document content:
{content}

Respond with ONLY a valid JSON object (no additional text) containing an 'entities' array, where each entity has:
- type: entity type (e.g., DATE, PERSON, ORGANIZATION, AMOUNT, etc.)
- value: extracted value
- confidence: extraction confidence (0-1)

Focus on extracting relevant information for a {documentType}.";
    }

    private string BuildSummarizationPrompt(string content, int maxLength, string fileName = null, string documentId = null)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Provide a concise summary of the following document in approximately {maxLength} characters.");
            
        if (!string.IsNullOrEmpty(fileName))
        {
            promptBuilder.AppendLine($"Document name: {fileName}");
        }
            
        if (!string.IsNullOrEmpty(documentId))
        {
            promptBuilder.AppendLine($"Document ID: {documentId}");
        }
            
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Document content:");
        promptBuilder.AppendLine(content);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Create a clear, informative summary that captures the main points and purpose of the document.");
            
        return promptBuilder.ToString();
    }

    private string BuildIntentPrompt(string content)
    {
        // Content is already truncated in ReadStreamContent
        return $@"Analyze the intent or purpose of this document.

Document content:
{content}

Respond with ONLY a valid JSON object (no additional text) containing:
- primaryIntent: main purpose of the document
- confidence: overall confidence (0-1)
- suggestedAction: recommended action

Example response:
{{""primaryIntent"": ""Payment Request"", ""confidence"": 0.9, ""suggestedAction"": ""Process payment""}}";
    }

    private DocumentClassificationResult ParseClassificationResponse(string response)
    {
        try
        {
            // Clean the response by removing any markdown code formatting
            string cleanedResponse = CleanJsonResponse(response);
            
            _logger.LogDebug("Cleaned classification response: {CleanedResponse}", cleanedResponse);
            
            // Parse the cleaned JSON
            var json = JsonDocument.Parse(cleanedResponse);
            var root = json.RootElement;

            var result = new DocumentClassificationResult
            {
                PrimaryCategory = root.TryGetProperty("category", out var cat)
                    ? cat.GetString() ?? "Unknown" : "Unknown",
                ProcessingNotes = $"Model: {_options.ClassificationModelId}"
            };

            if (root.TryGetProperty("confidence", out var conf))
            {
                result.CategoryConfidences[result.PrimaryCategory] = conf.GetDouble();
            }

            if (root.TryGetProperty("tags", out var tags))
            {
                foreach (var tag in tags.EnumerateArray())
                {
                    result.Tags.Add(tag.GetString() ?? "");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse classification response: {Response}", response);
            return new DocumentClassificationResult
            {
                PrimaryCategory = "Unknown",
                ProcessingNotes = $"Parse error: {ex.Message}"
            };
        }
    }

    private DocumentExtractionResult ParseExtractionResponse(string response)
    {
        try
        {
            // Clean the response by removing any markdown code formatting or backticks
            string cleanedResponse = CleanJsonResponse(response);
            
            _logger.LogDebug("Cleaned response for parsing: {CleanedResponse}", cleanedResponse);
            
            // Parse the cleaned JSON
            var json = JsonDocument.Parse(cleanedResponse);
            var root = json.RootElement;

            var result = new DocumentExtractionResult();
                
            if (root.TryGetProperty("entities", out var entitiesArray))
            {
                foreach (var entity in entitiesArray.EnumerateArray())
                {
                    result.Entities.Add(new ExtractedEntity
                    {
                        Type = entity.TryGetProperty("type", out var type)
                            ? type.GetString() ?? "" : "",
                        Value = entity.TryGetProperty("value", out var val)
                            ? val.GetString() ?? "" : "",
                        Confidence = entity.TryGetProperty("confidence", out var conf)
                            ? conf.GetDouble() : 0.8
                    });
                }
            }

            result.Metadata["model"] = _options.ExtractionModelId;
            result.Metadata["entityCount"] = result.Entities.Count.ToString();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse extraction response: {Response}", response);
            return new DocumentExtractionResult();
        }
    }
    
    /// <summary>
    /// Cleans a response string by removing any markdown code formatting and other non-JSON content
    /// </summary>
    private string CleanJsonResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
        {
            return "{}";
        }
        
        // Remove markdown code block formatting
        var cleaned = response
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();
            
        // Find the first '{' and last '}' to extract just the JSON portion
        int startIndex = cleaned.IndexOf('{');
        int endIndex = cleaned.LastIndexOf('}');
        
        if (startIndex >= 0 && endIndex > startIndex)
        {
            cleaned = cleaned.Substring(startIndex, endIndex - startIndex + 1);
        }
        else
        {
            _logger.LogWarning("Could not find valid JSON object markers in response. Response: {Response}", response);
            return "{}";
        }
        
        return cleaned;
    }

    private DocumentSummaryResult ParseSummaryResponse(string response)
    {
        // Response is plain text summary
        var result = new DocumentSummaryResult
        {
            Summary = response.Trim(),
            Language = "en"
        };

        // Extract key points from summary (simple sentence splitting)
        var sentences = response.Split(new[] { '.', '!', '?' }, 
            StringSplitOptions.RemoveEmptyEntries);
            
        foreach (var sentence in sentences.Take(5))
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length > 20)
            {
                result.KeyPoints.Add(trimmed);
            }
        }

        return result;
    }

    private DocumentIntentResult ParseIntentResponse(string response)
    {
        try
        {
            // Clean the response by removing any markdown code formatting
            string cleanedResponse = CleanJsonResponse(response);
            
            _logger.LogDebug("Cleaned intent response: {CleanedResponse}", cleanedResponse);
            
            // Parse the cleaned JSON
            var json = JsonDocument.Parse(cleanedResponse);
            var root = json.RootElement;

            var result = new DocumentIntentResult
            {
                PrimaryIntent = root.TryGetProperty("primaryIntent", out var intent)
                    ? intent.GetString() ?? "Unknown" : "Unknown",
                Confidence = root.TryGetProperty("confidence", out var conf)
                    ? conf.GetDouble() : 0.5,
                SuggestedAction = root.TryGetProperty("suggestedAction", out var action)
                    ? action.GetString() ?? "" : ""
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse intent response: {Response}", response);
            return new DocumentIntentResult
            {
                PrimaryIntent = "Unknown",
                Confidence = 0
            };
        }
    }

    private string FormatExtractedContent(DocumentContent extractedContent)
    {
        var formatted = new StringBuilder();
            
        // Add content type header
        formatted.AppendLine($"[Document Type: {extractedContent.ContentType}]");
            
        // Add metadata if available
        if (extractedContent.Metadata.Any())
        {
            formatted.AppendLine("\n[Metadata]");
            foreach (var kvp in extractedContent.Metadata.Take(5)) // Limit metadata items
            {
                formatted.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
        }
            
        // Add main content
        formatted.AppendLine("\n[Content]");
        formatted.AppendLine(extractedContent.Text);
            
        // Add tables if extracted
        if (extractedContent.ExtractedTables.Any())
        {
            formatted.AppendLine("\n[Extracted Tables]");
            foreach (var table in extractedContent.ExtractedTables.Take(3)) // Limit tables
            {
                formatted.AppendLine(table);
            }
        }
            
        // Note if content was truncated
        if (extractedContent.IsTruncated)
        {
            formatted.AppendLine("\n[Note: Content was truncated due to length limits]");
        }
            
        return formatted.ToString();
    }

    public void Dispose()
    {
        _bedrockClient?.Dispose();
    }
}