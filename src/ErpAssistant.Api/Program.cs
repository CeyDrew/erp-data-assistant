using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using ErpAssistant.Api.Infrastructure;
using ErpAssistant.Application;
using ErpAssistant.Infrastructure;
using ErpAssistant.Infrastructure.Ai;
using ErpAssistant.Infrastructure.Documents;
using ErpAssistant.Infrastructure.Observability;
using ErpAssistant.Infrastructure.Persistence;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.AddObservability("erp-api");
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddAi(builder.Configuration);
    builder.Services.AddDocumentPipeline(builder.Configuration);
    builder.Services.AddHostedService<SampleDocumentImporter>();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorization(Policies.Configure);

    var authPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 20);
    var chatPerMinute = builder.Configuration.GetValue("RateLimiting:ChatPerMinute", 20);
    builder.Services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) }));
        o.AddPolicy("chat", ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = chatPerMinute, Window = TimeSpan.FromMinutes(1) }));
    });

    var webOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"];
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(webOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

    builder.Services.AddRouting(o => o.LowercaseUrls = true);
    builder.Services
        .AddControllers(o => o.Filters.Add<ValidationFilter>())
        .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services
        .AddApiVersioning(o =>
        {
            o.DefaultApiVersion = new ApiVersion(1);
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ReportApiVersions = true;
        })
        .AddMvc()
        .AddApiExplorer(o =>
        {
            o.GroupNameFormat = "'v'V";
            o.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
    builder.Services.AddExceptionHandler<ApiExceptionHandler>();

    builder.Services.AddOpenApi("v1");
    builder.Services.AddErpHealthChecks(documents: true, ai: true);

    var app = builder.Build();

    if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
        await app.Services.InitializeDatabaseAsync();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("ERP Veri Asistanı API"));

    app.UseCors();
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapControllers();
    app.MapErpHealthChecks();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "API başlatılamadı");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
