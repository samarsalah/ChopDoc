using ChopDoc.Application;
using ChopDoc.Application.Options;
using ChopDoc.Domain.Abstractions;
using ChopDoc.Infrastructure.Conversion;
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
        services.AddScoped<IDocumentConverter, PdfToHtmlConverter>();
        services.AddScoped<IDocumentSplitter, HtmlDocumentSplitter>();
        services.AddScoped<IOutputValidator, DocumentOutputValidator>();

        services.AddApplication();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChopDocDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
