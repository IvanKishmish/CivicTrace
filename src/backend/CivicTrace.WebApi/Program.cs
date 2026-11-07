using System.Text;
using CivicTrace.Application;
using CivicTrace.Application.Common.Options;
using CivicTrace.Persistence;
using CivicTrace.Persistence.Extensions;
using CivicTrace.Persistence.Identity;
using CivicTrace.Persistence.Prozorro;
using CivicTrace.Persistence.Seeding;
using CivicTrace.WebApi;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

Env.TraversePath().Load();

// У Docker головне — консоль (docker logs). Файли писати необов'язково: щоб вони не зникали разом з контейнером,
// змонтуйте том на теку з LOG_DIRECTORY.
var logDirectory = Environment.GetEnvironmentVariable("LOG_DIRECTORY") ?? "logs";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        theme: AnsiConsoleTheme.Code,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(logDirectory, "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Без цього рядка повідомлення ILogger<T> (зокрема з LoggingBehaviour) не потрапляють у Serilog
    builder.Host.UseSerilog();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddInfrastructureIdentity()
        .AddDefaultTokenProviders();
    builder.Services.AddProzorroClient(builder.Configuration);
    builder.Services.AddPresentation(builder.Configuration);

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddControllers(options =>
    {
        options.OutputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.StringOutputFormatter>();
    });
    builder.Services.AddOpenApi();

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
        throw new InvalidOperationException("Jwt:Secret is not configured. Set the environment variable 'Jwt__Secret' (at least 32 characters).");

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

    // CORS потрібен лише якщо фронтенд відкривається з іншого походження (наприклад, Live Server на :5500).
    // Якщо фронтенд віддає сам API (Frontend:RootPath), CORS не потрібен.
    var corsOriginString = builder.Configuration["CORS_ALLOWED_ORIGINS"];
    var corsEnabled = !string.IsNullOrWhiteSpace(corsOriginString);

    if (corsEnabled)
    {
        var allowedOrigins = corsOriginString!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }
    else
    {
        Log.Warning("CORS_ALLOWED_ORIGINS is not set: cross-origin browser requests will be blocked.");
    }

    var app = builder.Build();

    var applyMigrations = app.Environment.IsDevelopment()
                          || app.Configuration.GetValue<bool>("APPLY_MIGRATIONS");

    if (applyMigrations)
        await app.Services.ApplyMigrationsAsync();

    if (app.Configuration.GetValue<bool>("Seed:ImportProcurements"))
    {
        var importResult = await app.Services.SeedProcurementsAsync(
            app.Configuration,
            app.Environment.ContentRootPath);
        Log.Information(
            "Procurement seed import finished: {Imported} imported, {Skipped} already present, {Replaced} replaced.",
            importResult.Imported,
            importResult.Skipped,
            importResult.Replaced);
    }

    await app.Services.SeedIdentityAsync();

    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();
    app.UseResponseCompression();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();

    // Необов'язково: API віддає статичний фронтенд із вказаної теки (відносно ContentRoot)
    var frontendRoot = app.Configuration["Frontend:RootPath"];

    if (!string.IsNullOrWhiteSpace(frontendRoot))
    {
        var fullPath = Path.GetFullPath(frontendRoot, app.Environment.ContentRootPath);

        if (Directory.Exists(fullPath))
        {
            var fileProvider = new PhysicalFileProvider(fullPath);

            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });

            Log.Information("Serving frontend from {FrontendPath}", fullPath);
        }
        else
        {
            Log.Warning("Frontend:RootPath '{FrontendPath}' does not exist, static files are not served.", fullPath);
        }
    }

    if (corsEnabled)
        app.UseCors();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRateLimiter();

    app.MapControllers();

    Log.Information("Starting CivicTrace.WebApi");

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name != "HostAbortedException")
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;