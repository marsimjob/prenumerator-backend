using Api.Endpoints.Auth;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Api.Endpoints.Credentials;
using Api.Endpoints.Groups;
using Api.Endpoints.Subscriptions;
using Api.Hubs;
using Api.Middleware;
using Api.Services;
using Application;
using Application.Common.Interfaces;
using Infrastructure;
using Serilog;

LoadEnvFile();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day));

    builder.Services.AddOpenApi();
    builder.Services.AddSignalR();

    var corsOrigin = builder.Configuration["CORS_ORIGIN"]
        ?? Environment.GetEnvironmentVariable("CORS_ORIGIN")
        ?? "http://localhost:5173";
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(corsOrigin, "https://thriving-hamster-3df49e.netlify.app")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddScoped<IFeedNotifier, SignalRFeedNotifier>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        // Step 1: DDL changes — add/drop columns and indexes
        db.Database.ExecuteSqlRaw(@"
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Username' AND object_id = OBJECT_ID('Users'))
                DROP INDEX IX_Users_Username ON Users;
            IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Username' AND Object_ID = OBJECT_ID('Users'))
                ALTER TABLE Users DROP COLUMN Username;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Email' AND Object_ID = OBJECT_ID('Users'))
                ALTER TABLE Users ADD Email nvarchar(256) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsEmailVerified' AND Object_ID = OBJECT_ID('Users'))
                ALTER TABLE Users ADD IsEmailVerified bit NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'VerificationCode' AND Object_ID = OBJECT_ID('Users'))
                ALTER TABLE Users ADD VerificationCode nvarchar(8) NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'VerificationCodeExpiry' AND Object_ID = OBJECT_ID('Users'))
                ALTER TABLE Users ADD VerificationCodeExpiry datetime2 NULL;
        ");

        // Step 2: Data fix and index — runs after columns exist
        db.Database.ExecuteSqlRaw(@"
            EXEC sp_executesql N'UPDATE Users SET Email = CONCAT(''legacy_'', CAST(Id AS NVARCHAR(36)), ''@placeholder.invalid'') WHERE Email = ''''';
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('Users'))
                CREATE UNIQUE INDEX IX_Users_Email ON Users (Email);
        ");
    }

    app.UseCors();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/health", (IConfiguration cfg) => Results.Ok(new {
        status = "ok",
        hasSendGridKey = !string.IsNullOrEmpty(cfg["SENDGRID_API_KEY"] ?? Environment.GetEnvironmentVariable("SENDGRID_API_KEY")),
        hasFromEmail   = !string.IsNullOrEmpty(cfg["SENDGRID_FROM_EMAIL"] ?? Environment.GetEnvironmentVariable("SENDGRID_FROM_EMAIL")),
    }));

    app.MapAuthEndpoints();
    app.MapGroupEndpoints();
    app.MapSubscriptionEndpoints();
    app.MapCredentialEndpoints();
    app.MapHub<FeedHub>("/hubs/feed");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Walks up from the working directory to find the nearest .env file.
static void LoadEnvFile()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, ".env");
        if (File.Exists(candidate))
        {
            foreach (var line in File.ReadAllLines(candidate))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                var idx = trimmed.IndexOf('=');
                if (idx < 0) continue;
                var key = trimmed[..idx].Trim();
                var val = trimmed[(idx + 1)..].Trim();
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, val);
            }
            return;
        }
        dir = dir.Parent;
    }
}
