namespace DocumentProcessor.Infrastructure.AI
{
    /// <summary>
    /// Configuration options for Amazon Bedrock integration
    /// </summary>
    public class BedrockOptions
    {
        /// <summary>
        /// AWS Region for Bedrock service (e.g., "us-west-2")
        /// </summary>
        public string Region { get; set; } = "us-west-2";

        /// <summary>
        /// Model ID for document classification
        /// Default: Claude 3 Haiku for cost-effectiveness
        /// </summary>
        public string ClassificationModelId { get; set; } = "anthropic.claude-3-haiku-20240307-v1:0";

        /// <summary>
        /// Model ID for entity extraction
        /// Default: Claude 3 Sonnet for better accuracy
        /// </summary>
        public string ExtractionModelId { get; set; } = "anthropic.claude-3-sonnet-20240229-v1:0";

        /// <summary>
        /// Model ID for document summarization
        /// Default: Claude 3 Haiku for cost-effectiveness
        /// </summary>
        public string SummarizationModelId { get; set; } = "anthropic.claude-3-haiku-20240307-v1:0";

        /// <summary>
        /// Model ID for intent detection
        /// Default: Claude 3 Haiku for quick response
        /// </summary>
        public string IntentModelId { get; set; } = "anthropic.claude-3-haiku-20240307-v1:0";

        /// <summary>
        /// Maximum tokens for model responses
        /// </summary>
        public int MaxTokens { get; set; } = 2000;

        /// <summary>
        /// Temperature for model creativity (0.0 - 1.0)
        /// Lower values make output more deterministic
        /// </summary>
        public double Temperature { get; set; } = 0.3;

        /// <summary>
        /// Top-P for nucleus sampling
        /// </summary>
        public double TopP { get; set; } = 0.9;

        /// <summary>
        /// Maximum retries for API calls
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Base delay in milliseconds for exponential backoff
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Enable detailed logging of Bedrock interactions
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Use AWS profile for credentials (for development)
        /// </summary>
        public string? AwsProfile { get; set; }
    }
}