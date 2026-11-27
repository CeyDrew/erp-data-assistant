using Microsoft.Extensions.AI;

namespace ErpAssistant.Application.Ai;

/// <summary>
/// Araçların tek tanımı. Hem sohbet asistanı (function calling) hem MCP sunucusu bu listeyi kullanır;
/// JSON Schema metot imzası ve <c>[Description]</c> özniteliklerinden üretilir, iki yerde elle tutulmaz.
/// </summary>
public static class ErpToolCatalog
{
    public static IReadOnlyList<AIFunction> Create(ErpTools tools, DocumentTools? documents = null)
    {
        var list = new List<AIFunction>
        {
            Make(tools.SatisOzeti, "satis_ozeti"),
            Make(tools.EnCokSatanlar, "en_cok_satanlar"),
            Make(tools.MusteriAra, "musteri_ara"),
            Make(tools.MusteriSiparisleri, "musteri_siparisleri"),
            Make(tools.StokDurumu, "stok_durumu"),
            Make(tools.GecikenTahsilatlar, "geciken_tahsilatlar"),
        };
        if (documents is not null)
            list.Add(Make(documents.DokumandaAra, "dokumanda_ara"));
        return list;
    }

    public static readonly IReadOnlyList<string> Names =
    [
        "satis_ozeti", "en_cok_satanlar", "musteri_ara", "musteri_siparisleri", "stok_durumu", "geciken_tahsilatlar",
        "dokumanda_ara",
    ];

    private static AIFunction Make(Delegate method, string name) =>
        AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { Name = name });
}

