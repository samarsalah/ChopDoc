using ChopDoc.Api.Serialization;
using ChopDoc.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "ChopDoc API",
        Version = "v1",
        Description =
            "Document conversion & splitting service.\n\n" +
            "Pipeline: PDF → HTML (intermediate) → split/validate → export (HTML / DOCX)."
    });
});
builder.Services.AddInfrastructure(builder.Configuration);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

// Swagger UI in the browser (all environments — convenient for the assessment demo)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ChopDoc API v1");
    options.DocumentTitle = "ChopDoc API";
    options.RoutePrefix = "swagger";
});

app.UseCors("Frontend");

// Opening http://localhost:5105/ lands on Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapControllers();

app.Run();
