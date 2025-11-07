using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using IoTDetectorApi.Data;
using IoTDetectorApi.Models;
using IoTDetectorApi.Services;
using IoTDetectorApi.Middleware;
using AspNetCoreRateLimit;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/iot-detector-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext} ({ThreadId}): {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting IoT Detector API");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Add Response Caching
builder.Services.AddResponseCaching();

// Add Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
        name: "database",
        tags: new[] { "db", "sql", "postgresql" });

// Add background services
builder.Services.AddHostedService<IoTHubListenerService>();

// Add CORS with security headers
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:5173",
                  "http://localhost:5174",
                  "http://localhost:3000",
                  "http://localhost:3001",
                  "http://localhost:3002",
                  "https://iot-presence-detector-azure-fronten-nine.vercel.app")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("X-Pagination", "X-Total-Count");
    });
});

// Add Security Headers
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

// Add Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.ClientIdHeader = "X-ClientId";
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/login",
            Period = "1m",
            Limit = 5
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/register",
            Period = "1h",
            Limit = 3
        }
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Add DbContext with PostgreSQL database (Neon)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string 'DefaultConnection' is not configured. " +
        "Please set it in appsettings.Development.json or via environment variable: " +
        "ConnectionStrings__DefaultConnection");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "JWT Key is not configured. " +
        "Please set 'Jwt:Key' in appsettings.Development.json or via environment variable. " +
        "The key must be at least 32 characters long. " +
        "Generate one with: openssl rand -base64 48");
}

if (jwtKey.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT Key must be at least 32 characters long. Current length: {jwtKey.Length}");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "IoTDetectorApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IoTDetectorClient";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        // Apply pending migrations automatically
        logger.LogInformation("Checking for pending database migrations...");
        context.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully");

        // Seed initial data only in development
        if (app.Environment.IsDevelopment())
        {
            logger.LogInformation("Seeding initial data (Development environment)...");
            SeedData(context, logger);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Use HSTS in production
    app.UseHsts();
}

// Enable response compression (must be before other middleware that might send responses)
app.UseResponseCompression();

// Enable response caching
app.UseResponseCaching();

// Add security headers middleware
app.Use(async (context, next) =>
{
    // Security headers
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

    // Content Security Policy (strict for API)
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'none'; frame-ancestors 'none'; base-uri 'self';");

    await next();
});

app.UseCors("AllowFrontend");

// Serilog request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp != null)
        {
            diagnosticContext.Set("RemoteIpAddress", remoteIp.ToString());
        }
    };
});

// Rate limiting middleware (must be before authentication)
app.UseIpRateLimiting();

// Input sanitization middleware for XSS and SQL injection protection
app.UseInputSanitization();

app.UseAuthentication();
app.UseAuthorization();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(result);
    }
});

    app.MapControllers();

    Log.Information("IoT Detector API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Shutting down IoT Detector API");
    Log.CloseAndFlush();
}

void SeedData(ApplicationDbContext context, Microsoft.Extensions.Logging.ILogger logger)
{
    // Seed users (DEVELOPMENT ONLY - DO NOT USE IN PRODUCTION)
    if (!context.Users.Any())
    {
        logger.LogWarning("⚠️  SEEDING DEFAULT USERS - FOR DEVELOPMENT ONLY!");
        logger.LogWarning("⚠️  Default password: Admin123! (change immediately in production)");

        var users = new[]
        {
            new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                Role = "admin"
            },
            new User
            {
                Username = "user",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("User123!"),
                Role = "user"
            }
        };
        context.Users.AddRange(users);
        context.SaveChanges();

        logger.LogInformation("✅ Created 2 default users: admin/Admin123! and user/User123!");
    }

    // Seed devices
    if (!context.Devices.Any())
    {
        logger.LogInformation("Seeding sample devices...");

        var devices = new[]
        {
            new Device
            {
                Id = "esp32-pir-01",
                Name = "ESP32-Sensor-01",
                Type = "ESP32",
                Status = "active",
                LastSeen = DateTime.UtcNow.AddMinutes(-2)
            },
            new Device
            {
                Id = "photon2-pir-01",
                Name = "Photon2-Hub-01",
                Type = "Photon2",
                Status = "active",
                LastSeen = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        context.Devices.AddRange(devices);
        context.SaveChanges();

        logger.LogInformation("✅ Created {Count} sample devices", devices.Length);
    }
}
