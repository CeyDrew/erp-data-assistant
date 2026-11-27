using System.Text.Json;
using ErpAssistant.Application.Abstractions;
using ErpAssistant.Application.Ai;
using FluentAssertions;

namespace ErpAssistant.UnitTests.Application;

public class ToolCatalogTests
{
    private static IReadOnlyList<Microsoft.Extensions.AI.AIFunction> Catalog() =>
        ErpToolCatalog.Create(new ErpTools(null!, null!, null!, null!, SystemUser.Instance, TimeProvider.System, new ToolCallRecorder()));

    [Fact]
    public void Catalog_exposes_whitelisted_tools_only()
    {
        Catalog().Select(t => t.Name).Should().BeEquivalentTo(
            "satis_ozeti", "en_cok_satanlar", "musteri_ara", "musteri_siparisleri", "stok_durumu", "geciken_tahsilatlar");
    }

    [Fact]
    public void Schemas_carry_descriptions_and_enum_values()
    {
        var tool = Catalog().Single(t => t.Name == "satis_ozeti");
        var schema = tool.JsonSchema.GetRawText();

        tool.Description.Should().Contain("satış");
        schema.Should().Contain("baslangic").And.Contain("yyyy-MM-dd");
        schema.Should().Contain("Kategori").And.Contain("Temsilci");
        tool.JsonSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString())
            .Should().BeEquivalentTo("baslangic", "bitis");
    }

    [Fact]
    public void Every_tool_has_a_description()
    {
        Catalog().Should().OnlyContain(t => t.Description.Length > 20);
    }
}

public class AssistantPromptTests
{
    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public bool IsSystem => false;
        public string? UserId => "1";
        public string? UserName => "Ayşe";
        public IReadOnlyCollection<string> Roles => [ErpAssistant.Application.Abstractions.Roles.SalesRep];
        public int? SalesRepresentativeId => 1;
    }

    [Fact]
    public void Prompt_resolves_relative_periods()
    {
        var prompt = AssistantPrompt.Build(new User(), new DateTime(2026, 3, 15));

        prompt.Should().Contain("geçen ay: 2026-02-01 → 2026-02-28");
        prompt.Should().Contain("bu yıl: 2026-01-01 → 2026-03-15");
        prompt.Should().Contain("Satış temsilcisi");
        prompt.Should().Contain("VERİDİR");
    }
}
