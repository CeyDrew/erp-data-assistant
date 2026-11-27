using ErpAssistant.Application;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Infrastructure;
using ErpAssistant.Infrastructure.Documents;
using Serilog;

// Doküman işleme worker'ı: RabbitMQ'dan mesaj alır, metni ayıklar, parçalar, embedding üretir ve Qdrant'a yazar.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Worker kullanıcısızdır; satır filtresi uygulanmaz.
builder.Services.AddSingleton<ICurrentUser>(SystemUser.Instance);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDocumentPipeline(builder.Configuration).AddDocumentProcessing();

await builder.Build().RunAsync();
