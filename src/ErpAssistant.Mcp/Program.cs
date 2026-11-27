using ErpAssistant.Application;
using ErpAssistant.Infrastructure;
using ErpAssistant.Infrastructure.Ai;
using ErpAssistant.Infrastructure.Documents;
using ErpAssistant.Infrastructure.Identity;
using ErpAssistant.Infrastructure.Observability;
using ErpAssistant.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

// MCP sunucusu: ERP araçlarını Model Context Protocol üzerinden (Streamable HTTP) ajanlara açar.
// Kimlik: Authorization: Bearer erp_pat_... (paneldeki "Erişim token'ları" sayfasından).
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.AddObservability("erp-mcp");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAi(builder.Configuration);
builder.Services.AddDocumentPipeline(builder.Configuration);
builder.Services.AddScoped<PersonalAccessTokenService>();

builder.Services
    .AddAuthentication(PersonalAccessTokenHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, PersonalAccessTokenHandler>(PersonalAccessTokenHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "erp-assistant", Title = "ERP Veri Asistanı", Version = "1.0.0" })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools(McpToolRegistration.CreateTools())
    .WithRequestFilters(f => f.AddCallToolFilter(McpToolRegistration.LogCalls));

builder.Services.AddErpHealthChecks(documents: true, ai: false);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp").RequireAuthorization();
app.MapErpHealthChecks();

await app.RunAsync();

