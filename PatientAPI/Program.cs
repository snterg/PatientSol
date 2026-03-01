using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using PatientSolution.Data;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"Connection String: {builder.Configuration.GetConnectionString("DefaultConnection")}");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Swagger configuration with dictionaries support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Patient API",
        Version = "v1",
        Description = "API for patient management"
    });

    // Add parameter filter for query parameters
    c.ParameterFilter<SwaggerParameterFilter>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Patient API v1");
        c.EnableTryItOutByDefault();
        c.DefaultModelsExpandDepth(0);
    });
}

app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    int retryCount = 0;
    int maxRetries = 10;

    while (retryCount < maxRetries)
    {
        try
        {
            Console.WriteLine($"Attempting to connect to SQL Server (attempt {retryCount + 1}/{maxRetries})...");

            // Create database if it doesn't exist
            dbContext.Database.EnsureCreated();
            Console.WriteLine("Database created/verified successfully!");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            retryCount++;

            if (retryCount < maxRetries)
            {
                Console.WriteLine("Waiting 5 seconds before next retry...");
                Thread.Sleep(5000);
            }
        }
    }
}

app.Run();

/// <summary>
/// Фильтр для Swagger, добавляющий примеры значений для параметров запроса
/// </summary>
public class SwaggerParameterFilter : IParameterFilter
{
    public void Apply(OpenApiParameter parameter, ParameterFilterContext context)
    {
        // Configuration for gender parameter
        if (parameter.Name == "gender")
        {
            parameter.Description = "Patient gender. Allowed values: male, female, other, unknown";
            parameter.Schema.Enum = new List<IOpenApiAny>
            {
                new OpenApiString("male"),
                new OpenApiString("female"),
                new OpenApiString("other"),
                new OpenApiString("unknown")
            };
            parameter.Schema.Example = new OpenApiString("male");
        }
        // Configuration for active parameter
        else if (parameter.Name == "active")
        {
            parameter.Description = "Active status. Allowed values: true, false";
            parameter.Schema.Enum = new List<IOpenApiAny>
            {
                new OpenApiBoolean(true),
                new OpenApiBoolean(false)
            };
            parameter.Schema.Example = new OpenApiBoolean(true);
        }
        // Configuration for birthDate parameter
        else if (parameter.Name == "birthDate")
        {
            parameter.Description = "Birth date in FHIR format. Examples: eq2024-01-13, gt2024-01-01, lt2024-12-31";
            parameter.Schema.Example = new OpenApiString("eq2024-01-13");
        }
    }
}