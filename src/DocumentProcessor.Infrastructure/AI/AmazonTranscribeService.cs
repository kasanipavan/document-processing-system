using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using DocumentProcessor.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.AI
{
    /// <summary>
    /// Service for transcribing audio files using Amazon Transcribe
    /// </summary>
    public class AmazonTranscribeService
    {
        private readonly ILogger<AmazonTranscribeService> _logger;
        private readonly IAmazonTranscribeService _transcribeClient;
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _region;

        public AmazonTranscribeService(
            ILogger<AmazonTranscribeService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _region = configuration["Bedrock:Region"] ?? "us-east-1";
            _bucketName = configuration["Transcribe:S3BucketName"] ?? "document-processor-transcribe";
            
            var awsProfile = configuration["Bedrock:AwsProfile"];
            
            if (!string.IsNullOrWhiteSpace(awsProfile))
            {
                var credentials = new Amazon.Runtime.StoredProfileAWSCredentials(awsProfile);
                _transcribeClient = new AmazonTranscribeServiceClient(credentials, RegionEndpoint.GetBySystemName(_region));
                _s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(_region));
            }
            else
            {
                _transcribeClient = new AmazonTranscribeServiceClient(RegionEndpoint.GetBySystemName(_region));
                _s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(_region));
            }
        }

        /// <summary>
        /// Transcribe an MP3 file to text using Amazon Transcribe
        /// </summary>
        public async Task<string> TranscribeAudioAsync(Document document, Stream audioStream)
        {
            if (document == null || audioStream == null)
            {
                throw new ArgumentNullException("Document and audio stream cannot be null");
            }

            var jobName = $"transcribe-{document.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            
            try
            {
                _logger.LogInformation("Starting transcription job for document {DocumentId}", document.Id);

                // Upload audio file to S3 (required for Transcribe)
                var s3Key = $"audio/{document.Id}/{document.FileName}";
                await UploadToS3Async(audioStream, s3Key);

                // Start transcription job
                var transcriptionJob = await StartTranscriptionJobAsync(jobName, s3Key);

                // Wait for job completion
                var completedJob = await WaitForJobCompletionAsync(jobName);

                if (completedJob.TranscriptionJobStatus == TranscriptionJobStatus.COMPLETED)
                {
                    // Download and parse transcript
                    var transcript = await DownloadTranscriptAsync(completedJob.Transcript.TranscriptFileUri);
                    
                    // Clean up S3 object
                    await DeleteFromS3Async(s3Key);
                    
                    _logger.LogInformation("Successfully transcribed document {DocumentId}", document.Id);
                    return transcript;
                }
                else
                {
                    _logger.LogError("Transcription job failed for document {DocumentId}: {Status}", 
                        document.Id, completedJob.TranscriptionJobStatus);
                    
                    // Clean up S3 object
                    await DeleteFromS3Async(s3Key);
                    
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transcribing audio for document {DocumentId}", document.Id);
                throw;
            }
            finally
            {
                // Clean up transcription job
                try
                {
                    await _transcribeClient.DeleteTranscriptionJobAsync(new DeleteTranscriptionJobRequest
                    {
                        TranscriptionJobName = jobName
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete transcription job {JobName}", jobName);
                }
            }
        }

        private async Task UploadToS3Async(Stream audioStream, string key)
        {
            try
            {
                // Ensure bucket exists
                await EnsureBucketExistsAsync();

                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = audioStream,
                    ContentType = "audio/mpeg"
                };

                await _s3Client.PutObjectAsync(putRequest);
                _logger.LogDebug("Uploaded audio file to S3: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload audio file to S3");
                throw;
            }
        }

        private async Task EnsureBucketExistsAsync()
        {
            try
            {
                await _s3Client.EnsureBucketExistsAsync(_bucketName);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Creating S3 bucket {BucketName}", _bucketName);
                await _s3Client.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = _bucketName,
                    BucketRegion = S3Region.FindValue(_region)
                });
            }
        }

        private async Task<StartTranscriptionJobResponse> StartTranscriptionJobAsync(string jobName, string s3Key)
        {
            var request = new StartTranscriptionJobRequest
            {
                TranscriptionJobName = jobName,
                LanguageCode = LanguageCode.EnUS, // Can be made configurable
                MediaFormat = MediaFormat.Mp3,
                Media = new Media
                {
                    MediaFileUri = $"s3://{_bucketName}/{s3Key}"
                },
                OutputBucketName = _bucketName,
                Settings = new Settings
                {
                    ShowSpeakerLabels = true,
                    MaxSpeakerLabels = 10
                }
            };

            return await _transcribeClient.StartTranscriptionJobAsync(request);
        }

        private async Task<TranscriptionJob> WaitForJobCompletionAsync(string jobName, int maxWaitSeconds = 300)
        {
            var startTime = DateTime.UtcNow;
            
            while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitSeconds)
            {
                var response = await _transcribeClient.GetTranscriptionJobAsync(new GetTranscriptionJobRequest
                {
                    TranscriptionJobName = jobName
                });

                var job = response.TranscriptionJob;

                if (job.TranscriptionJobStatus == TranscriptionJobStatus.COMPLETED ||
                    job.TranscriptionJobStatus == TranscriptionJobStatus.FAILED)
                {
                    return job;
                }

                _logger.LogDebug("Transcription job {JobName} status: {Status}", jobName, job.TranscriptionJobStatus);
                
                // Wait before checking again
                await Task.Delay(5000); // 5 seconds
            }

            throw new TimeoutException($"Transcription job {jobName} did not complete within {maxWaitSeconds} seconds");
        }

        private async Task<string> DownloadTranscriptAsync(string transcriptUri)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var response = await httpClient.GetStringAsync(transcriptUri);
                
                // Parse the JSON response from Amazon Transcribe
                var transcriptData = System.Text.Json.JsonDocument.Parse(response);
                
                if (transcriptData.RootElement.TryGetProperty("results", out var results) &&
                    results.TryGetProperty("transcripts", out var transcripts))
                {
                    var transcriptArray = transcripts.EnumerateArray();
                    var fullTranscript = string.Empty;
                    
                    foreach (var transcript in transcriptArray)
                    {
                        if (transcript.TryGetProperty("transcript", out var text))
                        {
                            fullTranscript += text.GetString() + " ";
                        }
                    }
                    
                    return fullTranscript.Trim();
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download transcript from {Uri}", transcriptUri);
                throw;
            }
        }

        private async Task DeleteFromS3Async(string key)
        {
            try
            {
                await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key
                });
                
                _logger.LogDebug("Deleted S3 object: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete S3 object {Key}", key);
            }
        }
    }
}