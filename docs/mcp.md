# MCP sunucusu

ERP araçları [Model Context Protocol](https://modelcontextprotocol.io) üzerinden de açılır. Claude Code,
Claude Desktop veya MCP destekleyen herhangi bir ajan bağlanıp aynı raporları çalıştırabilir.

| | |
|---|---|
| Adres | `http://localhost:5090/mcp` (Streamable HTTP, durumsuz) |
| Kimlik | `Authorization: Bearer erp_pat_...` — panelde **Erişim token'ları** sayfasından |
| Yetki | Token sahibinin rolü ve satır filtresi; temsilci token'ı yalnızca kendi müşterilerini görür |
| Araçlar | `satis_ozeti`, `en_cok_satanlar`, `musteri_ara`, `musteri_siparisleri`, `stok_durumu`, `geciken_tahsilatlar`, `dokumanda_ara` — hepsi `readOnlyHint: true` |
| Günlük | Her çağrı AI sorgu günlüğüne `mcp` kanalıyla yazılır |

## Claude Code'a ekleme

```bash
claude mcp add --transport http erp http://localhost:5090/mcp \
  --header "Authorization: Bearer erp_pat_..."
```

Ya da proje köküne `.mcp.json`:

```json
{
  "mcpServers": {
    "erp": {
      "type": "http",
      "url": "http://localhost:5090/mcp",
      "headers": { "Authorization": "Bearer ${ERP_MCP_TOKEN}" }
    }
  }
}
```

Örnek istek: *"erp aracıyla geçen ay müşterilere göre satış özetini çıkar ve ilk 5 müşteriyi tablo yap."*

## Ham JSON-RPC ile deneme

```bash
TOKEN=erp_pat_...
H=(-H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream")

curl -s localhost:5090/mcp "${H[@]}" -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'

curl -s localhost:5090/mcp "${H[@]}" -d '{"jsonrpc":"2.0","id":2,"method":"tools/call",
  "params":{"name":"satis_ozeti","arguments":{"baslangic":"2026-10-01","bitis":"2026-10-31","grupla":"Musteri"}}}'
```

## Tasarım

- **Kontrat tek yerde.** `ErpToolCatalog.Definitions` her aracın adını ve metodunu tutar. Sohbet asistanı
  bunlardan `AIFunction`, MCP sunucusu `McpServerTool` üretir; ikisi de JSON Schema'yı aynı metot imzası ve
  `[Description]` özniteliklerinden çıkarır.
- **Hedef nesne istek başına.** `McpServerTool.Create(method, ctx => ctx.Services.GetRequiredService(...))` —
  araç sınıfı isteğin DI kapsamından gelir; `DbContext` filtreleri token sahibinin kimliğiyle çalışır.
- **Salt okunur.** Yazma yapan araç yok; araç açıklamalarında `readOnly`, `idempotent` ipuçları var.
- **PAT doğrulaması** ayrı bir kimlik şeması (`PersonalAccessTokenHandler`); JWT ile aynı claim'leri üretir,
  böylece `ICurrentUser` ve yetki kuralları hiç değişmeden çalışır.
