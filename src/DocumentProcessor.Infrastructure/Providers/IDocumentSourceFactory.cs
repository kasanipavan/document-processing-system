using DocumentProcessor.Core.Interfaces;

namespace DocumentProcessor.Infrastructure.Providers;

/// <summary>
/// Interface for document source factory
/// </summary>
public interface IDocumentSourceFactory
{
    /// <summary>
    /// Creates the default document source provider
    /// </summary>
    IDocumentSourceProvider CreateProvider();

    /// <summary>
    /// Creates a specific document source provider by name
    /// </summary>
    IDocumentSourceProvider CreateProvider(string providerName);

    /// <summary>
    /// Gets all available provider names
    /// </summary>
    IEnumerable<string> GetAvailableProviders();

    /// <summary>
    /// Checks if a provider is available
    /// </summary>
    bool IsProviderAvailable(string providerName);

    /// <summary>
    /// Creates a provider based on document source type
    /// </summary>
    IDocumentSourceProvider CreateProviderForSource(string sourcePath);

    /// <summary>
    /// Registers a custom provider type
    /// </summary>
    void RegisterProvider(string name, Type providerType);
}