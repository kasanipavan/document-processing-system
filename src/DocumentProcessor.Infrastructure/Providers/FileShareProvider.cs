using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Providers
{
    /// <summary>
    /// Provider for network file shares (UNC paths)
    /// Handles documents stored on network shares with proper permission handling
    /// </summary>
    public class FileShareProvider : IDocumentSourceProvider
    {
        private readonly ILogger<FileShareProvider> _logger;
        private readonly string _networkPath;
        private readonly string? _username;
        private readonly string? _password;
        private readonly bool _useImpersonation;

        public string ProviderName => "FileShare";

        public FileShareProvider(ILogger<FileShareProvider> logger, IConfiguration configuration)
        {
            _logger = logger;
            _networkPath = configuration["FileShare:NetworkPath"] ?? @"\\server\documents";
            _username = configuration["FileShare:Username"];
            _password = configuration["FileShare:Password"];
            _useImpersonation = bool.TryParse(configuration["FileShare:UseImpersonation"], out var useImp) && useImp;

            // Ensure network path exists if accessible
            try
            {
                if (!Directory.Exists(_networkPath))
                {
                    _logger.LogWarning("Network path not accessible: {Path}", _networkPath);
                }
                else
                {
                    _logger.LogInformation("Connected to file share: {Path}", _networkPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing network path: {Path}", _networkPath);
            }
        }

        public async Task<Stream> GetDocumentStreamAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                ValidateNetworkPath(fullPath);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"File not found on network share: {path}");
                }

                _logger.LogInformation("Opening file from network share: {Path}", fullPath);
                return await Task.FromResult(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied to network file: {Path}", path);
                throw new UnauthorizedAccessException($"Access denied to network file: {path}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing network file: {Path}", path);
                throw;
            }
        }

        public async Task<byte[]> GetDocumentBytesAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                ValidateNetworkPath(fullPath);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"File not found on network share: {path}");
                }

                _logger.LogInformation("Reading file from network share: {Path}", fullPath);
                return await File.ReadAllBytesAsync(fullPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied to network file: {Path}", path);
                throw new UnauthorizedAccessException($"Access denied to network file: {path}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading network file: {Path}", path);
                throw;
            }
        }

        public async Task<string> SaveDocumentAsync(Stream documentStream, string fileName)
        {
            try
            {
                // Generate organized path structure
                var relativePath = GenerateRelativePath(fileName);
                var fullPath = GetFullPath(relativePath);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    SetDirectoryPermissions(directory);
                }

                // Save file
                using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await documentStream.CopyToAsync(fileStream);
                }

                SetFilePermissions(fullPath);
                _logger.LogInformation("File saved to network share: {Path}", fullPath);
                
                return relativePath;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied saving to network share: {FileName}", fileName);
                throw new UnauthorizedAccessException($"Access denied saving to network share: {fileName}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving to network share: {FileName}", fileName);
                throw;
            }
        }

        public async Task<string> SaveDocumentAsync(byte[] documentBytes, string fileName)
        {
            using var stream = new MemoryStream(documentBytes);
            return await SaveDocumentAsync(stream, fileName);
        }

        public async Task<bool> DeleteDocumentAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                ValidateNetworkPath(fullPath);

                if (File.Exists(fullPath))
                {
                    await Task.Run(() => File.Delete(fullPath));
                    _logger.LogInformation("File deleted from network share: {Path}", fullPath);
                    
                    // Try to clean up empty directories
                    CleanupEmptyDirectories(Path.GetDirectoryName(fullPath));
                    return true;
                }

                _logger.LogWarning("File not found on network share: {Path}", fullPath);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied deleting network file: {Path}", path);
                throw new UnauthorizedAccessException($"Access denied deleting network file: {path}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting network file: {Path}", path);
                throw;
            }
        }

        public async Task<bool> DocumentExistsAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                var exists = File.Exists(fullPath);
                
                _logger.LogDebug("Checking file existence on network share: {Path} - {Exists}", fullPath, exists);
                return await Task.FromResult(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking network file existence: {Path}", path);
                return false;
            }
        }

        public async Task<DocumentInfo> GetDocumentInfoAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                ValidateNetworkPath(fullPath);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"File not found on network share: {path}");
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

                // Add network share metadata
                documentInfo.Metadata["NetworkPath"] = _networkPath;
                documentInfo.Metadata["FullPath"] = fullPath;
                documentInfo.Metadata["IsReadOnly"] = fileInfo.IsReadOnly.ToString();
                
                // Try to get owner information (Windows-specific)
                if (OperatingSystem.IsWindows())
                {
                    try
                    {
                        var fileSecurity = new FileSecurity(fullPath, AccessControlSections.Owner);
                        var owner = fileSecurity.GetOwner(typeof(NTAccount));
                        if (owner != null)
                        {
                            documentInfo.Metadata["Owner"] = owner.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not retrieve owner information for: {Path}", fullPath);
                    }
                }

                return await Task.FromResult(documentInfo);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied to network file info: {Path}", path);
                throw new UnauthorizedAccessException($"Access denied to network file info: {path}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting network file info: {Path}", path);
                throw;
            }
        }

        public async Task<IEnumerable<DocumentInfo>> ListDocumentsAsync(string path)
        {
            try
            {
                var fullPath = GetFullPath(path);
                var documents = new List<DocumentInfo>();

                if (!Directory.Exists(fullPath))
                {
                    _logger.LogWarning("Directory not found on network share: {Path}", fullPath);
                    return documents;
                }

                var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    try
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
                            ContentType = GetContentType(fileInfo.Extension),
                            Metadata = new Dictionary<string, string>
                            {
                                ["NetworkPath"] = _networkPath,
                                ["IsReadOnly"] = fileInfo.IsReadOnly.ToString()
                            }
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _logger.LogWarning("Access denied to file: {File}", file);
                        // Skip files we can't access
                    }
                }

                _logger.LogInformation("Listed {Count} documents from network share: {Path}", documents.Count, fullPath);
                return await Task.FromResult(documents);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied listing network directory: {Path}", path);
                throw new UnauthorizedAccessException($"Access denied listing network directory: {path}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing network directory: {Path}", path);
                throw;
            }
        }

        public async Task<string> GetDownloadUrlAsync(string path, TimeSpan expiration)
        {
            try
            {
                var fullPath = GetFullPath(path);
                
                if (!await DocumentExistsAsync(path))
                {
                    throw new FileNotFoundException($"File not found on network share: {path}");
                }

                // For file shares, return the UNC path
                // In production, this might return a secure download link through a web service
                _logger.LogInformation("Generated network share path for: {Path}", fullPath);
                return await Task.FromResult($"file://{fullPath.Replace('\\', '/')}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating download URL for network file: {Path}", path);
                throw;
            }
        }

        public async Task<bool> MoveDocumentAsync(string sourcePath, string destinationPath)
        {
            try
            {
                var sourceFullPath = GetFullPath(sourcePath);
                var destFullPath = GetFullPath(destinationPath);
                
                ValidateNetworkPath(sourceFullPath);
                ValidateNetworkPath(destFullPath);

                if (!File.Exists(sourceFullPath))
                {
                    _logger.LogWarning("Source file not found on network share: {Path}", sourceFullPath);
                    return false;
                }

                // Ensure destination directory exists
                var destDirectory = Path.GetDirectoryName(destFullPath);
                if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                    SetDirectoryPermissions(destDirectory);
                }

                await Task.Run(() => File.Move(sourceFullPath, destFullPath, true));
                SetFilePermissions(destFullPath);

                _logger.LogInformation("File moved on network share from {Source} to {Dest}", sourceFullPath, destFullPath);
                
                // Try to clean up empty source directories
                CleanupEmptyDirectories(Path.GetDirectoryName(sourceFullPath));
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied moving network file from {Source} to {Dest}", sourcePath, destinationPath);
                throw new UnauthorizedAccessException($"Access denied moving network file", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving network file from {Source} to {Dest}", sourcePath, destinationPath);
                throw;
            }
        }

        public async Task<bool> CopyDocumentAsync(string sourcePath, string destinationPath)
        {
            try
            {
                var sourceFullPath = GetFullPath(sourcePath);
                var destFullPath = GetFullPath(destinationPath);
                
                ValidateNetworkPath(sourceFullPath);
                ValidateNetworkPath(destFullPath);

                if (!File.Exists(sourceFullPath))
                {
                    _logger.LogWarning("Source file not found on network share: {Path}", sourceFullPath);
                    return false;
                }

                // Ensure destination directory exists
                var destDirectory = Path.GetDirectoryName(destFullPath);
                if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                    SetDirectoryPermissions(destDirectory);
                }

                await Task.Run(() => File.Copy(sourceFullPath, destFullPath, true));
                SetFilePermissions(destFullPath);

                _logger.LogInformation("File copied on network share from {Source} to {Dest}", sourceFullPath, destFullPath);
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied copying network file from {Source} to {Dest}", sourcePath, destinationPath);
                throw new UnauthorizedAccessException($"Access denied copying network file", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying network file from {Source} to {Dest}", sourcePath, destinationPath);
                throw;
            }
        }

        private string GetFullPath(string relativePath)
        {
            // Remove any leading slashes
            relativePath = relativePath.TrimStart('/', '\\');
            return Path.Combine(_networkPath, relativePath);
        }

        private string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(_networkPath, fullPath).Replace('\\', '/');
        }

        private string GenerateRelativePath(string fileName)
        {
            var date = DateTime.UtcNow;
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
            var cleanFileName = Path.GetFileName(fileName);
            
            return Path.Combine(
                "documents",
                date.ToString("yyyy"),
                date.ToString("MM"),
                date.ToString("dd"),
                guid,
                cleanFileName
            ).Replace('\\', '/');
        }

        private void ValidateNetworkPath(string path)
        {
            // Ensure the path is within the allowed network share
            var fullPath = Path.GetFullPath(path);
            var basePath = Path.GetFullPath(_networkPath);
            
            if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Path is outside the allowed network share");
            }
        }

        private void SetFilePermissions(string filePath)
        {
            try
            {
                // Set appropriate permissions for the file
                var fileSecurity = new FileSecurity(filePath, AccessControlSections.Access);
                
                // Example: Grant read/write to authenticated users
                var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
                fileSecurity.AddAccessRule(new FileSystemAccessRule(
                    authenticatedUsers,
                    FileSystemRights.Read | FileSystemRights.Write,
                    AccessControlType.Allow));

                // In .NET 9, use FileSystemAclExtensions
                FileSystemAclExtensions.SetAccessControl(new FileInfo(filePath), fileSecurity);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not set file permissions for: {Path}", filePath);
            }
        }

        private void SetDirectoryPermissions(string directoryPath)
        {
            try
            {
                // Set appropriate permissions for the directory
                var directorySecurity = new DirectorySecurity(directoryPath, AccessControlSections.Access);
                
                // Example: Grant read/write to authenticated users
                var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
                directorySecurity.AddAccessRule(new FileSystemAccessRule(
                    authenticatedUsers,
                    FileSystemRights.ReadAndExecute | FileSystemRights.Write,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));

                // In .NET 9, use FileSystemAclExtensions
                FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(directoryPath), directorySecurity);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not set directory permissions for: {Path}", directoryPath);
            }
        }

        private void CleanupEmptyDirectories(string directoryPath)
        {
            try
            {
                if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                    return;

                // Don't delete the root network path
                if (directoryPath.Equals(_networkPath, StringComparison.OrdinalIgnoreCase))
                    return;

                // Check if directory is empty
                if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
                {
                    Directory.Delete(directoryPath);
                    _logger.LogDebug("Deleted empty directory: {Path}", directoryPath);
                    
                    // Recursively check parent directory
                    var parentDirectory = Path.GetDirectoryName(directoryPath);
                    if (!string.IsNullOrEmpty(parentDirectory))
                    {
                        CleanupEmptyDirectories(parentDirectory);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not cleanup directory: {Path}", directoryPath);
            }
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
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".json" => "application/json",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }
    }
}