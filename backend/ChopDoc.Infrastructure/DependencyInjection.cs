using ChopDoc.Application;
using ChopDoc.Application.Options;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Infrastructure.Conversion;
using ChopDoc.Infrastructure.Export;
using ChopDoc.Infrastructure.Options;
using ChopDoc.Infrastructure.Persistence;
using ChopDoc.Infrastructure.Processing;
using ChopDoc.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChopDoc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DocumentProcessingOptions>(
            configuration.GetSection(DocumentProcessingOptions.SectionName));

        services.Configure<StorageOptions>(
            configuration.GetSection(StorageOptions.SectionName));

        var connectionString = configuration.GetConnectionString("ChopDoc")
            ?? "Data Source=chopdoc.db";

        services.AddDbContext<ChopDocDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentConverter, PdfDocumentConverter>();
        services.AddScoped<IDocumentSplitter, MarkedDocumentSplitter>();
        services.AddScoped<IOutputValidator, DocumentOutputValidator>();
        services.AddScoped<IOutputExporter, HtmlOutputExporter>();

        services.AddApplication();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("ChopDoc")
            ?? "Data Source=chopdoc.db";

        EnsureSqliteDirectoryExists(connectionString);

        var storageRoot = configuration.GetSection(StorageOptions.SectionName)["RootPath"];
        if (!string.IsNullOrWhiteSpace(storageRoot))
            Directory.CreateDirectory(Path.GetFullPath(storageRoot));

        var db = scope.ServiceProvider.GetRequiredService<ChopDocDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    private static void EnsureSqliteDirectoryExists(string connectionString)
    {
        const string prefix = "Data Source=";
        var start = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return;

        var pathPart = connectionString[(start + prefix.Length)..].Trim().Trim('"');
        var semicolon = pathPart.IndexOf(';');
        if (semicolon >= 0)
            pathPart = pathPart[..semicolon];

        var fullPath = Path.GetFullPath(pathPart);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }
}
