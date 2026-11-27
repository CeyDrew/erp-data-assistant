using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ErpAssistant.Application.Ai;

public sealed record ToolDefinition(string Name, MethodInfo Method);

/// <summary>
/// Araçların tek tanımı. Hem sohbet asistanı (function calling) hem MCP sunucusu bu listeyi kullanır;
/// JSON Schema metot imzası ve <c>[Description]</c> özniteliklerinden üretilir, iki yerde elle tutulmaz.
/// </summary>
public static class ErpToolCatalog
{
    public static readonly IReadOnlyList<ToolDefinition> Definitions =
    [
        new("satis_ozeti", Method<ErpTools>(nameof(ErpTools.SatisOzeti))),
        new("en_cok_satanlar", Method<ErpTools>(nameof(ErpTools.EnCokSatanlar))),
        new("musteri_ara", Method<ErpTools>(nameof(ErpTools.MusteriAra))),
        new("musteri_siparisleri", Method<ErpTools>(nameof(ErpTools.MusteriSiparisleri))),
        new("stok_durumu", Method<ErpTools>(nameof(ErpTools.StokDurumu))),
        new("geciken_tahsilatlar", Method<ErpTools>(nameof(ErpTools.GecikenTahsilatlar))),
        new("dokumanda_ara", Method<DocumentTools>(nameof(DocumentTools.DokumandaAra))),
    ];

    /// <summary>Argüman okuma ayarları: Türkçe yazımlı enum değerlerini tolere eder.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = CreateJson();

    public static IReadOnlyList<AIFunction> Create(ErpTools tools, DocumentTools? documents = null) =>
        Definitions
            .Select(d => (d, target: d.Method.DeclaringType == typeof(ErpTools) ? (object?)tools : documents))
            .Where(x => x.target is not null)
            .Select(x => AIFunctionFactory.Create(x.d.Method, x.target,
                new AIFunctionFactoryOptions { Name = x.d.Name, SerializerOptions = SerializerOptions }))
            .ToList();

    private static MethodInfo Method<T>(string name) =>
        typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"{typeof(T).Name}.{name} bulunamadı");

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions);
        options.Converters.Insert(0, new TolerantEnumConverterFactory());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
