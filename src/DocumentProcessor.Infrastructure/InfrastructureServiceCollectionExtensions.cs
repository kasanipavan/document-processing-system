using DocumentProcessor.Core.Interfaces;
using DocumentProcessor.Infrastructure.AI;
using DocumentProcessor.Infrastructure.BackgroundTasks;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Infrastructure.Providers;
using DocumentProcessor.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentProcessor.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Entity Framework
        // To switch to Local SQL Server, comment out the line below and uncomment the next line
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        //services.AddDbContext<ApplicationDbContext>(options =>   options.UseSqlServer(configuration.GetConnectionString("LocalSqlServer")));

        // Register repositories
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentTypeRepository, DocumentTypeRepository>();
        services.AddScoped<IClassificationRepository, ClassificationRepository>();
        services.AddScoped<IProcessingQueueRepository, ProcessingQueueRepository>();
        services.AddScoped<IDocumentMetadataRepository, DocumentMetadataRepository>();
            
        // Register Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Register document source providers
        services.AddSingleton<LocalFileSystemProvider>();
        services.AddSingleton<FileShareProvider>();
        services.AddSingleton<IDocumentSourceFactory, DocumentSourceFactory>();
        services.AddScoped<IDocumentSourceProvider>(provider =>
        {
            var factory = provider.GetRequiredService<IDocumentSourceFactory>();
            var sourceType = configuration.GetValue<string>("DocumentStorage:Provider") ?? "LocalFileSystem";
            return factory.CreateProvider(sourceType);
        });

        // Register AI processing services
        // Change to Scoped to support scoped dependencies
        services.AddScoped<IAIProcessorFactory, AIProcessorFactory>();
        
        // Register DocumentContentExtractor with service provider for transcription support
        services.AddScoped<DocumentContentExtractor>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DocumentContentExtractor>>();
            return new DocumentContentExtractor(logger, provider);
        });
        
        // Register Amazon Transcribe service for MP3 transcription
        services.AddScoped<AmazonTranscribeService>();

        // Use database-backed queue instead of in-memory queue
        services.AddSingleton<IAIProcessingQueue, DatabaseProcessingQueue>();
            
        // Register Bedrock configuration
        var bedrockSection = configuration.GetSection("Bedrock");
        services.Configure<BedrockOptions>(options =>
        {
            bedrockSection.Bind(options);
        });
            
        // Register background task services
        var usePriorityQueue = configuration.GetValue<bool>("BackgroundTasks:UsePriorityQueue", true);
        if (usePriorityQueue)
        {
            services.AddSingleton<IBackgroundTaskQueue, PriorityBackgroundTaskQueue>();
        }
        else
        {
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        }
            
        // Register hosted services
        var maxConcurrency = configuration.GetValue<int>("BackgroundTasks:MaxConcurrency", 3);
            
        // Register the base QueuedHostedService
        services.AddHostedService(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<QueuedHostedService>>();
            var queue = provider.GetRequiredService<IBackgroundTaskQueue>();
            logger.LogInformation("Creating QueuedHostedService");
            return new QueuedHostedService(queue, logger);
        });
            
        // Register the DocumentProcessingHostedService
        services.AddHostedService(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DocumentProcessingHostedService>>();
            var queue = provider.GetRequiredService<IBackgroundTaskQueue>();
            logger.LogInformation("Creating DocumentProcessingHostedService with max concurrency: {MaxConcurrency}", maxConcurrency);
            return new DocumentProcessingHostedService(queue, logger, maxConcurrency);
        });
            
        // Register the AI Queue Processing Service for database-backed queue
        services.AddHostedService<AIQueueProcessingService>();
            
        // Note: IDocumentProcessingService is registered in the Application layer

        return services;
    }

    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database")
            .AddCheck("document-storage", () =>
            {
                // Simple health check for document storage
                var provider = configuration.GetValue<string>("DocumentStorage:Provider");
                if (string.IsNullOrEmpty(provider))
                {
                    return HealthCheckResult.Unhealthy("No document storage provider configured");
                }
                return HealthCheckResult.Healthy($"Document storage provider: {provider}");
            });

        return services;
    }

    public static async Task EnsureDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            logger.LogInformation("Ensuring database exists and is up to date...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Database is ready");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database");
            throw;
        }
    }
}