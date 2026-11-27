using ErpAssistant.Infrastructure.Documents;
using FluentAssertions;

namespace ErpAssistant.UnitTests.Documents;

public class TextExtractionTests
{
    private static string Sample(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ErpAssistant.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "samples", "documents", name);
    }

    private static IReadOnlyList<PageText> Extract(string name)
    {
        using var stream = File.OpenRead(Sample(name));
        return TextExtractor.Extract(stream, name);
    }

    [Fact]
    public void Pdf_keeps_page_numbers_and_turkish_characters()
    {
        var pages = Extract("garanti-ve-iade-sartnamesi.pdf");

        pages.Select(p => p.Page).Should().Equal(1, 2, 3);
        pages[1].Text.Should().Contain("İade süresi").And.Contain("14");
        pages[0].Text.Should().Contain("ŞARTNAMESİ");
    }

    [Fact]
    public void Docx_splits_pages_on_page_breaks()
    {
        var pages = Extract("depo-ve-sevkiyat-proseduru.docx");

        pages.Should().HaveCount(2);
        pages[0].Text.Should().Contain("1,80 metre");
        pages[1].Page.Should().Be(2);
        pages[1].Text.Should().Contain("15:00");
    }

    [Fact]
    public void Xlsx_rows_become_pipe_separated_text()
    {
        var pages = Extract("bayi-fiyat-listesi-2026.xlsx");

        pages.Single().Text.Should().Contain("Rulman | %12 | 50 adet");
    }

    [Fact]
    public void Unsupported_extension_throws()
    {
        var act = () => TextExtractor.Extract(new MemoryStream([1, 2, 3]), "virus.exe");
        act.Should().Throw<NotSupportedException>();
    }
}

public class TextChunkerTests
{
    [Fact]
    public void Chunks_respect_size_overlap_and_page_boundaries()
    {
        var sentence = "Bu bir deneme cümlesidir ve parçalama davranışını ölçer. ";
        var pages = new[]
        {
            new PageText(1, string.Concat(Enumerable.Repeat(sentence, 200))),
            new PageText(2, "Kısa ikinci sayfa."),
        };

        var chunks = TextChunker.Chunk(pages, maxTokens: 200, overlapTokens: 40);

        chunks.Should().OnlyContain(c => c.Text.Length <= 200 * TextChunker.CharsPerToken + 200);
        chunks.Select(c => c.Index).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        chunks.Last().Page.Should().Be(2);
        chunks.Last().Text.Should().Be("Kısa ikinci sayfa.");
        chunks.Where(c => c.Page == 1).Should().HaveCountGreaterThan(5);

        // Örtüşme: ardışık parçalar ortak metin paylaşır.
        var head = chunks[1].Text[..30];
        chunks[0].Text[^(40 * TextChunker.CharsPerToken + 40)..].Should().Contain(head);
    }

    [Fact]
    public void Empty_pages_produce_no_chunks()
    {
        TextChunker.Chunk([new PageText(1, "   ")]).Should().BeEmpty();
    }
}
