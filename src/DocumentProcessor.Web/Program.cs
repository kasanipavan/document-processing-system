using DocumentProcessor.Infrastructure;
using DocumentProcessor.Infrastructure.Data;
using DocumentProcessor.Web.Components;
using DocumentProcessor.Web.Hubs;
using DocumentProcessor.Web.Services;
using DocumentProcessor.Application;
using DocumentProcessor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add SignalR
builder.Services.AddSignalR();

// Add notification service
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add infrastructure services (Database, Repositories, Document Sources)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add application services (Document processing, Background services)
builder.Services.AddApplicationServices();


// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // Sign in settings
    options.SignIn.RequireConfirmedEmail = false; // Set to true in production
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure authentication cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

// Add authorization policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdminRole", policy => policy.RequireRole("Administrator"))
    .AddPolicy("RequireManagerRole", policy => policy.RequireRole("Administrator", "Manager"))
    .AddPolicy("RequireUserRole", policy => policy.RequireRole("Administrator", "Manager", "User"));

// Add health checks
builder.Services.AddInfrastructureHealthChecks(builder.Configuration);

// Add additional logging if needed
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Add rate limiting (simplified for now - can be enhanced later)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// Ensure database is created and migrations are applied
await app.Services.EnsureDatabaseAsync();

// Initialize stored procedures
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await DocumentProcessor.Infrastructure.Data.StoredProcedureInitializer.InitializeStoredProceduresAsync(context, logger);
}

// Seed test document types if none exist
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    if (!context.DocumentTypes.Any(dt => dt.IsActive))
    {
        logger.LogInformation("Seeding test document types...");
        
        var documentTypes = new[]
        {
            new DocumentType
            {
                Id = Guid.NewGuid(),
                Name = "PDF Documents",
                Description = "Portable Document Format files",
                Category = "General",
                IsActive = true,
                Priority = 1,
                FileExtensions = ".pdf",
                Keywords = "pdf,document,portable",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new DocumentType
            {
                Id = Guid.NewGuid(),
                Name = "Word Documents",
                Description = "Microsoft Word documents",
                Category = "General",
                IsActive = true,
                Priority = 2,
                FileExtensions = ".docx,.doc",
                Keywords = "word,document,microsoft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new DocumentType
            {
                Id = Guid.NewGuid(),
                Name = "Excel Spreadsheets",
                Description = "Microsoft Excel spreadsheet files",
                Category = "Data",
                IsActive = true,
                Priority = 3,
                FileExtensions = ".xlsx,.xls",
                Keywords = "excel,spreadsheet,data",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        
        context.DocumentTypes.AddRange(documentTypes);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Test document types seeded successfully");
    }
}


// Seed initial roles and admin user
// Temporarily commented out for testing
// await SeedIdentityData(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Development environment configuration
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Add response compression
app.UseResponseCompression();

// Add rate limiting
app.UseRateLimiter();

// Add authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Serve static files from wwwroot
app.UseStaticFiles();


// Serve files from the uploads directory
string uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");

// Ensure the uploads directory exists before creating the PhysicalFileProvider
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map SignalR hub
app.MapHub<DocumentProcessingHub>("/documentHub");

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// Add cleanup endpoint for stuck documents
app.MapGet("/admin/cleanup-stuck-documents", async (IServiceProvider services) =>
{
    using var scope = services.CreateScope();
    var backgroundService = scope.ServiceProvider.GetRequiredService<DocumentProcessor.Application.Services.IBackgroundDocumentProcessingService>();
    
    await backgroundService.CleanupStuckDocumentsAsync(30); // 30 minutes timeout
    
    return Results.Ok(new { message = "Stuck documents cleanup initiated" });
});

app.Run();