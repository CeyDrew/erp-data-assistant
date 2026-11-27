using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ErpAssistant.Application.Common;

/// <summary>
/// Uygulamaya özgü izler ve metrikler (yalnızca BCL; dışa aktarım Infrastructure'da OpenTelemetry ile).
/// </summary>
public static class ErpTelemetry
{
    public const string Name = "ErpAssistant";

    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> ToolCalls =
        Meter.CreateCounter<long>("erp.ai.tool_calls", description: "Asistan/MCP araç çağrıları (araç ve sonuç etiketli)");

    public static readonly Histogram<double> ToolDuration =
        Meter.CreateHistogram<double>("erp.ai.tool_duration", unit: "ms", description: "Araç çalışma süresi");

    public static readonly Counter<long> DocumentsProcessed =
        Meter.CreateCounter<long>("erp.documents.processed", description: "İşlenen dokümanlar (sonuç etiketli)");

    public static readonly Histogram<double> DocumentDuration =
        Meter.CreateHistogram<double>("erp.documents.duration", unit: "ms", description: "Doküman işleme süresi");
}
