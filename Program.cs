using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TradingSignalsApi.Data;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using System.IO;
using TradingSignalsApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Configure Database (PostgreSQL for Heroku, SQLite for local)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Check if running on Heroku (with DATABASE_URL)
    string herokuConnectionString = DatabaseUtils.GetHerokuConnectionString();
    if (herokuConnectionString != null)
    {
        // Use PostgreSQL when deployed to Heroku
        options.UseNpgsql(herokuConnectionString);
    }
    else
    {
        // Use SQLite for local development
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=signals.db");
    }
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Trading Signals API", Version = "v1" });
    
    // Add API Key authentication for signals endpoint
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key for accessing signals",
        Name = "ApiKey",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });
    
    // Add API Key authentication for config endpoints
    c.AddSecurityDefinition("ConfigApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key for accessing webhook configurations",
        Name = "ConfigApiKey",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ConfigApiKey"
    });

    var apiKeySecurityScheme = new OpenApiSecurityScheme
    {
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" },
        In = ParameterLocation.Header
    };
    
    var configApiKeySecurityScheme = new OpenApiSecurityScheme
    {
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ConfigApiKey" },
        In = ParameterLocation.Header
    };

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { apiKeySecurityScheme, new List<string>() },
        { configApiKeySecurityScheme, new List<string>() }
    });
});

// Add HTTP context accessor for logging
builder.Services.AddHttpContextAccessor();

// Add HttpClient for external API calls
builder.Services.AddHttpClient();

// Register Services
builder.Services.AddSingleton<TradingSignalsApi.Services.MetaApiService>();

// Register Background Services
// Signal Monitoring Service runs every 1 minute to process and auto-resolve signals
builder.Services.AddHostedService<TradingSignalsApi.Services.SignalMonitoringService>();

var app = builder.Build();

// Apply migrations automatically
if (app.Environment.IsProduction())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        try
        {
            logger.LogInformation("Applying database migrations...");
            context.Database.Migrate();
            logger.LogInformation("Database migrations applied successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database.");
            throw; // Re-throw to prevent app from starting with wrong schema
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Trading Signals API v1");
    });
}

// Serve static files from wwwroot
app.UseStaticFiles();

// Optional: HTTP request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("HTTP {Method} {Path} from {IP}", 
        context.Request.Method, 
        context.Request.Path, 
        context.Connection.RemoteIpAddress);
    
    await next();
    
    logger.LogInformation("HTTP {Method} {Path} responded {StatusCode}", 
        context.Request.Method, 
        context.Request.Path, 
        context.Response.StatusCode);
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Set up a default route to the index.html
app.MapFallbackToFile("/index.html");
app.MapFallbackToFile("/", "index.html");

app.Run();
