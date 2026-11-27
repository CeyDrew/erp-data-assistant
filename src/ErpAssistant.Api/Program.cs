using System.Text.Json.Serialization;
using Asp.Versioning;
using ErpAssistant.Api.Infrastructure;
using ErpAssistant.Application;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Infrastructure;
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

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

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
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
        await app.Services.InitializeDatabaseAsync();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("ERP Veri Asistanı API"));

    app.MapControllers();
    app.MapHealthChecks("/health");

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
