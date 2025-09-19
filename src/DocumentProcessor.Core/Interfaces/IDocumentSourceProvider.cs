using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DocumentProcessor.Core.Interfaces
{
    public interface IDocumentSourceProvider
    {
        string ProviderName { get; }
        Task<Stream> GetDocumentStreamAsync(string path);
        Task<byte[]> GetDocumentBytesAsync(string path);
        Task<string> SaveDocumentAsync(Stream documentStream, string fileName);
        Task<string> SaveDocumentAsync(byte[] documentBytes, string fileName);
        Task<bool> DeleteDocumentAsync(string path);
        Task<bool> DocumentExistsAsync(string path);
        Task<DocumentInfo> GetDocumentInfoAsync(string path);
        Task<IEnumerable<DocumentInfo>> ListDocumentsAsync(string path);
        Task<string> GetDownloadUrlAsync(string path, TimeSpan expiration);
    }

    public class DocumentInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}