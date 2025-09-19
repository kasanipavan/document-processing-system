using System;
using System.Collections.Generic;
using System.Linq;
using DocumentProcessor.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure.Providers;

/// <summary>
/// Factory for creating document source providers based on configuration
/// Implements strategy pattern for provider selection
/// </summary>
public class DocumentSourceFactory(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DocumentSourceFactory> logger)
    : IDocumentSourceFactory
{
    private readonly Dictionary<string, Type> _providerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LocalFileSystem"] = typeof(LocalFileSystemProvider),
        //["MockS3"] = typeof(MockS3Provider),
        //["S3"] = typeof(MockS3Provider), // Using MockS3Provider until real S3 is implemented
        ["FileShare"] = typeof(FileShareProvider),
        ["Network"] = typeof(FileShareProvider) // Alias for FileShare
    };

    // Register available provider types
    //["MockS3"] = typeof(MockS3Provider),
    //["S3"] = typeof(MockS3Provider), // Using MockS3Provider until real S3 is implemented
    // Alias for FileShare

    /// <summary>
    /// Creates a document source provider based on the configured default provider
    /// </summary>
    public IDocumentSourceProvider CreateProvider()
    {
        var defaultProviderName = configuration["DocumentSource:DefaultProvider"] ?? "LocalFileSystem";
        return CreateProvider(defaultProviderName);
    }

    /// <summary>
    /// Creates a specific document source provider by name
    /// </summary>
    public IDocumentSourceProvider CreateProvider(string providerName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));
            }

            if (!_providerTypes.TryGetValue(providerName, out var providerType))
            {
                throw new NotSupportedException($"Document source provider '{providerName}' is not supported.");
            }

            logger.LogInformation("Creating document source provider: {ProviderName}", providerName);

            // Use dependency injection to create the provider with proper dependencies
            var provider = (IDocumentSourceProvider)serviceProvider.GetRequiredService(providerType);
                
            logger.LogInformation("Successfully created {ProviderName} provider", provider.ProviderName);
            return provider;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating document source provider: {ProviderName}", providerName);
            throw;
        }
    }

    /// <summary>
    /// Gets all available provider names
    /// </summary>
    public IEnumerable<string> GetAvailableProviders()
    {
        return _providerTypes.Keys;
    }

    /// <summary>
    /// Checks if a provider is available
    /// </summary>
    public bool IsProviderAvailable(string providerName)
    {
        return !string.IsNullOrWhiteSpace(providerName) && 
               _providerTypes.ContainsKey(providerName);
    }

    /// <summary>
    /// Creates a provider based on document source type
    /// Useful for automatic provider selection based on path patterns
    /// </summary>
    public IDocumentSourceProvider CreateProviderForSource(string sourcePath)
    {
        try
        {
            // Determine provider based on path pattern
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return CreateProvider(); // Use default
            }

            // Check for S3 URL pattern
            if (sourcePath.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ||
                sourcePath.StartsWith("https://s3", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Detected S3 path pattern, using S3 provider");
                return CreateProvider("S3");
            }

            // Check for UNC path pattern (network share)
            if (sourcePath.StartsWith("\\\\") || 
                sourcePath.StartsWith("//") ||
                sourcePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Detected network path pattern, using FileShare provider");
                return CreateProvider("FileShare");
            }

            // Check for absolute local path
            if (Path.IsPathRooted(sourcePath))
            {
                logger.LogInformation("Detected local path pattern, using LocalFileSystem provider");
                return CreateProvider("LocalFileSystem");
            }

            // Default to configured provider
            logger.LogInformation("Using default provider for path: {Path}", sourcePath);
            return CreateProvider();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error determining provider for source: {SourcePath}", sourcePath);
            throw;
        }
    }

    /// <summary>
    /// Registers a custom provider type
    /// Useful for extending with additional providers at runtime
    /// </summary>
    public void RegisterProvider(string name, Type providerType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Provider name cannot be null or empty.", nameof(name));
        }

        if (providerType == null)
        {
            throw new ArgumentNullException(nameof(providerType));
        }

        if (!typeof(IDocumentSourceProvider).IsAssignableFrom(providerType))
        {
            throw new ArgumentException(
                $"Provider type must implement {nameof(IDocumentSourceProvider)}.", 
                nameof(providerType));
        }

        _providerTypes[name] = providerType;
        logger.LogInformation("Registered custom provider: {Name} -> {Type}", name, providerType.Name);
    }
}