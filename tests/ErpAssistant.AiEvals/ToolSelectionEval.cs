using System.Globalization;
using System.Text;
using System.Text.Json;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Ai;
using ErpAssistant.Infrastructure.Ai;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace ErpAssistant.AiEvals;

/// <summary>
/// Gerçek modele karşı araç seçimi değerlendirmesi. Araçlar çalıştırılmaz; yalnızca modelin ilk yanıtındaki
/// araç adı ve parametreler beklenenle karşılaştırılır. Varsayılan olarak atlanır: <c>AI_EVAL=1</c> ile açılır.
/// Sağlayıcı ortam değişkenleriyle seçilir (ör. <c>Ai__Provider=Anthropic</c>, <c>Ai__Anthropic__ApiKey=...</c>).
/// </summary>
public sealed class ToolSelectionEval(ITestOutputHelper output)
{
    private const double PassRate = 0.90;

    private sealed class EvalUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public bool IsSystem => false;
        public string? UserId => "eval";
        public string? UserName => "Değerlendirme";
        public IReadOnlyCollection<string> Roles => [ErpAssistant.Application.Abstractions.Roles.Admin];
        public int? SalesRepresentativeId => null;
    }

    private sealed class NoSearch : IDocumentSearch
    {
        public Task<IReadOnlyList<DocumentHit>> SearchAsync(string q, IReadOnlyCollection<string>? d, int top, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DocumentHit>>([]);
    }

    [SkippableFact]
    public async Task Model_picks_the_right_tool_and_arguments()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("AI_EVAL") == "1", "AI_EVAL=1 değil; gerçek model değerlendirmesi atlandı.");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:Provider"] = "Ollama" })
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection().AddLogging().AddAi(configuration).BuildServiceProvider();
        var factory = services.GetRequiredService<IChatClientFactory>();
        using var chat = factory.Create();

        var user = new EvalUser();
        var today = DateTime.UtcNow.Date;
        var recorder = new ToolCallRecorder();
        var tools = ErpToolCatalog.Create(
            new ErpTools(null!, null!, null!, null!, user, TimeProvider.System, recorder),
            new DocumentTools(new NoSearch(), user, recorder));
        var options = new ChatOptions { Tools = [.. tools], ToolMode = ChatToolMode.Auto, Temperature = 0 };
        if (factory.Provider == "Ollama")
            options.AdditionalProperties = new() { ["num_ctx"] = 8192 };

        var cases = EvalCases.For(today);
        var report = new StringBuilder();
        report.AppendLine($"# Araç seçimi değerlendirmesi\n\nModel: `{factory.Provider}/{factory.Model}` · Tarih: {today:yyyy-MM-dd}\n");
        report.AppendLine("| # | Soru | Beklenen | Gelen | Sonuç |\n|---|---|---|---|---|");

        var passed = 0;
        for (var i = 0; i < cases.Count; i++)
        {
            var c = cases[i];
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, AssistantPrompt.Build(user, today)),
                new(ChatRole.User, c.Question),
            };

            string got;
            bool ok;
            try
            {
                var response = await chat.GetResponseAsync(messages, options);
                var call = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().FirstOrDefault();
                got = call is null ? "(araç yok)" : $"{call.Name} {JsonSerializer.Serialize(call.Arguments)}";
                ok = call is not null && call.Name == c.Tool && c.Args.All(kv => Matches(call.Arguments, kv.Key, kv.Value));
            }
            catch (Exception ex)
            {
                got = $"HATA: {ex.Message}";
                ok = false;
            }

            if (ok) passed++;
            output.WriteLine($"{(ok ? "✓" : "✗")} {c.Question}\n    beklenen: {c.Tool} {JsonSerializer.Serialize(c.Args)}\n    gelen:    {got}");
            report.AppendLine($"| {i + 1} | {c.Question} | `{c.Tool}` {Esc(JsonSerializer.Serialize(c.Args))} | {Esc(got)} | {(ok ? "✅" : "❌")} |");
        }

        var rate = passed / (double)cases.Count;
        report.AppendLine($"\n**Sonuç: {passed}/{cases.Count} (%{rate * 100:0})** — eşik %{PassRate * 100:0}");
        var path = Path.Combine(FindRepoRoot(), "docs", "ai-eval-results.md");
        await File.WriteAllTextAsync(path, report.ToString());
        output.WriteLine($"\n{passed}/{cases.Count} doğru → {path}");

        rate.Should().BeGreaterThanOrEqualTo(PassRate);
    }

    private static bool Matches(IDictionary<string, object?>? args, string key, object expected)
    {
        if (args is null || !args.TryGetValue(key, out var actual) || actual is null) return false;
        var text = actual is JsonElement je ? (je.ValueKind == JsonValueKind.String ? je.GetString()! : je.GetRawText()) : actual.ToString()!;

        return expected switch
        {
            string s when s.StartsWith('~') => new CultureInfo("tr-TR").CompareInfo.IndexOf(text, s[1..], CompareOptions.IgnoreCase) >= 0,
            string s => TolerantEnumConverterFactory.Normalize(text) == TolerantEnumConverterFactory.Normalize(s),
            bool b => string.Equals(text, b ? "true" : "false", StringComparison.OrdinalIgnoreCase),
            int n => int.TryParse(text, out var v) && v == n,
            _ => false,
        };
    }

    private static string Esc(string s) => s.Replace("|", "\\|");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ErpAssistant.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
