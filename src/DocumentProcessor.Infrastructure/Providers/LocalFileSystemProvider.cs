using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Providers
{
    public class LocalFileSystemProvider : IDocumentSourceProvider
    {
        private readonly ILogger<LocalFileSystemProvider> _logger;
        private readonly string _basePath;
        private readonly string _tempPath;

        public string ProviderName => "LocalFileSystem";

        public LocalFileSystemProvider(ILogger<LocalFileSystemProvider> logger, IConfiguration configuration)
        {
            _logger = logger;
            _basePath = configuration["DocumentProcessing:StoragePath"] ?? "uploads";
            _tempPath = configuration["DocumentProcessing:TempPath"] ?? "temp";

            // Ensure directories exist
            EnsureDirectoryExists(_basePath);
            EnsureDirectoryExists(_tempPath);
        }

        public async Task<Stream> GetDocumentStreamAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Document not found at path: {path}");
                }

                // Use FileShare.ReadWrite to allow other processes to access the file while we're reading it
                // This helps prevent file locking issues when the same file is accessed multiple times
                return await Task.FromResult(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document stream for path: {Path}", path);
                throw;
            }
        }

        public async Task<byte[]> GetDocumentBytesAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Document not found at path: {path}");
                }

                return await File.ReadAllBytesAsync(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document bytes for path: {Path}", path);
                throw;
            }
        }

        public async Task<string> SaveDocumentAsync(Stream documentStream, string fileName)
        {
            try
            {
                // Generate unique file path
                var uniqueFileName = GenerateUniqueFileName(fileName);
                var relativePath = Path.Combine(DateTime.UtcNow.ToString("yyyy/MM/dd"), uniqueFileName);
                var fullPath = GetFullPath(relativePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    EnsureDirectoryExists(directory);
                }

                // Save file
                using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await documentStream.CopyToAsync(fileStream);
                }

                _logger.LogInformation("Document saved successfully at: {Path}", relativePath);
                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving document: {FileName}", fileName);
                throw;
            }
        }

        public async Task<string> SaveDocumentAsync(byte[] documentBytes, string fileName)
        {
            try
            {
                // Generate unique file path
                var uniqueFileName = GenerateUniqueFileName(fileName);
                var relativePath = Path.Combine(DateTime.UtcNow.ToString("yyyy/MM/dd"), uniqueFileName);
                var fullPath = GetFullPath(relativePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    EnsureDirectoryExists(directory);
                }

                // Save file
                await File.WriteAllBytesAsync(fullPath, documentBytes);

                _logger.LogInformation("Document saved successfully at: {Path}", relativePath);
                return relativePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving document: {FileName}", fileName);
                throw;
            }
        }

        public async Task<bool> DeleteDocumentAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    // Implement retry logic with a maximum number of attempts
                    int maxRetries = 3;
                    int delayMs = 500;
                    
                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            // Try to delete the file
                            File.Delete(fullPath);
                            _logger.LogInformation("Document deleted: {Path}", path);
                            return true;
                        }
                        catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                        {
                            if (attempt < maxRetries)
                            {
                                _logger.LogWarning("File is in use, waiting before retry {Attempt}/{MaxRetries}: {Path}",
                                    attempt, maxRetries, path);
                                await Task.Delay(delayMs * attempt);
                            }
                            else
                            {
                                _logger.LogWarning("File is still in use after {MaxRetries} attempts, cannot delete: {Path}", maxRetries, path);
                                // Return false but don't throw on the final attempt
                                return false;
                            }
                        }
                    }
                    
                    // Should never reach here due to return statements above
                    return false;
                }

                _logger.LogWarning("Document not found for deletion: {Path}", path);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document: {Path}", path);
                throw;
            }
        }

        public async Task<bool> DocumentExistsAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                return await Task.FromResult(File.Exists(fullPath));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking document existence: {Path}", path);
                return false;
            }
        }

        public async Task<DocumentInfo> GetDocumentInfoAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Document not found at path: {path}");
                }

                var fileInfo = new FileInfo(fullPath);
                var documentInfo = new DocumentInfo
                {
                    FileName = fileInfo.Name,
                    Path = path,
                    Size = fileInfo.Length,
                    CreatedAt = fileInfo.CreationTimeUtc,
                    ModifiedAt = fileInfo.LastWriteTimeUtc,
                    ContentType = GetContentType(fileInfo.Extension)
                };

                // Add file attributes as metadata
                documentInfo.Metadata["IsReadOnly"] = fileInfo.IsReadOnly.ToString();
                documentInfo.Metadata["Extension"] = fileInfo.Extension;
                documentInfo.Metadata["Directory"] = fileInfo.DirectoryName ?? string.Empty;

                return await Task.FromResult(documentInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting document info: {Path}", path);
                throw;
            }
        }

        public async Task<IEnumerable<DocumentInfo>> ListDocumentsAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                if (!Directory.Exists(fullPath))
                {
                    return new List<DocumentInfo>();
                }

                var documents = new List<DocumentInfo>();
                var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var relativePath = GetRelativePath(file);
                    
                    documents.Add(new DocumentInfo
                    {
                        FileName = fileInfo.Name,
                        Path = relativePath,
                        Size = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTimeUtc,
                        ModifiedAt = fileInfo.LastWriteTimeUtc,
                        ContentType = GetContentType(fileInfo.Extension)
                    });
                }

                return await Task.FromResult(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing documents in path: {Path}", path);
                throw;
            }
        }

        public async Task<string> GetDownloadUrlAsync(string path, TimeSpan expiration)
        {
            // For local file system, return the direct file path
            // In production, this could return a secured URL
            var fullPath = GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Document not found at path: {path}");
            }

            // Return a file URI for local access
            return await Task.FromResult($"file:///{fullPath.Replace('\\', '/')}");
        }


        private string GetFullPath(string relativePath)
        {
            // Ensure the path is safe and within the base directory
            var fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));
            var baseFullPath = Path.GetFullPath(_basePath);

            if (!fullPath.StartsWith(baseFullPath))
            {
                throw new UnauthorizedAccessException("Access to path outside of base directory is not allowed");
            }

            return fullPath;
        }

        private string GetRelativePath(string fullPath)
        {
            var baseFullPath = Path.GetFullPath(_basePath);
            if (fullPath.StartsWith(baseFullPath))
            {
                return fullPath.Substring(baseFullPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return fullPath;
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                _logger.LogDebug("Created directory: {Path}", path);
            }
        }

        private string GenerateUniqueFileName(string fileName)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8);

            return $"{fileNameWithoutExtension}_{timestamp}_{guid}{extension}";
        }

        private string GetContentType(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".ogg" => "audio/ogg",
                ".zip" => "application/zip",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".eml" => "message/rfc822",
                ".msg" => "application/vnd.ms-outlook",
                _ => "application/octet-stream"
            };
        }
    }
}