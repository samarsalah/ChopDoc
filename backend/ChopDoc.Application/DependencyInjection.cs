using ChopDoc.Application.Options;
using ChopDoc.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChopDoc.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDocumentJobService, DocumentJobService>();
        return services;
    }

    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        Action<DocumentProcessingOptions> configure)
    {
        services.Configure(configure);
        return services.AddApplication();
    }
}
