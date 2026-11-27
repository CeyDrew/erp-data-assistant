using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using UglyToad.PdfPig;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ErpAssistant.Infrastructure.Documents;

public sealed record PageText(int Page, string Text);

/// <summary>Dosyadan sayfa numarasıyla metin çıkarır. Kaynak gösterimi için sayfa bilgisi korunur.</summary>
public static class TextExtractor
{
    public static IReadOnlyList<PageText> Extract(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var pages = extension switch
        {
            ".pdf" => Pdf(stream),
            ".docx" => Docx(stream),
            ".xlsx" => Xlsx(stream),
            ".txt" or ".md" => [new PageText(1, new StreamReader(stream, Encoding.UTF8).ReadToEnd())],
            _ => throw new NotSupportedException($"Desteklenmeyen dosya türü: {extension}"),
        };
        return pages.Where(p => !string.IsNullOrWhiteSpace(p.Text)).ToList();
    }

    private static List<PageText> Pdf(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        return pdf.GetPages()
            .Select(p => new PageText(p.Number, UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(p)))
            .ToList();
    }

    /// <summary>
    /// DOCX'te sabit sayfa yoktur; Word'ün kaydettiği sayfa sonu işaretleri (elle sayfa sonu ve
    /// <c>lastRenderedPageBreak</c>) sayfa numarası olarak kullanılır.
    /// </summary>
    private static List<PageText> Docx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return [];

        var pages = new List<PageText>();
        var page = 1;
        var current = new StringBuilder();
        foreach (var paragraph in body.Descendants<W.Paragraph>())
        {
            var breaks = paragraph.Descendants<W.Break>().Count(b => b.Type?.Value == W.BreakValues.Page)
                         + paragraph.Descendants<W.LastRenderedPageBreak>().Count();
            if (breaks > 0 && current.Length > 0)
            {
                pages.Add(new PageText(page, current.ToString()));
                current.Clear();
                page += breaks;
            }
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text)) current.AppendLine(text);
        }
        if (current.Length > 0) pages.Add(new PageText(page, current.ToString()));
        return pages;
    }

    /// <summary>XLSX'te her sayfa (sheet) bir "sayfa"; satırlar sekmeyle ayrılmış metne dönüşür.</summary>
    private static List<PageText> Xlsx(Stream stream)
    {
        using var doc = SpreadsheetDocument.Open(stream, false);
        var workbook = doc.WorkbookPart!;
        var shared = workbook.SharedStringTablePart?.SharedStringTable;
        var pages = new List<PageText>();
        var index = 0;

        foreach (var sheet in workbook.Workbook.Sheets!.Elements<Sheet>())
        {
            index++;
            var part = (WorksheetPart)workbook.GetPartById(sheet.Id!);
            var sb = new StringBuilder($"Sayfa: {sheet.Name}\n");
            foreach (var row in part.Worksheet.Descendants<Row>())
            {
                var cells = row.Elements<Cell>().Select(c => CellText(c, shared));
                sb.AppendLine(string.Join(" | ", cells));
            }
            pages.Add(new PageText(index, sb.ToString()));
        }
        return pages;
    }

    private static string CellText(Cell cell, SharedStringTable? shared)
    {
        var value = cell.CellValue?.Text ?? cell.InnerText;
        if (cell.DataType?.Value == CellValues.SharedString && shared is not null && int.TryParse(value, out var i))
            return shared.ElementAt(i).InnerText;
        return value;
    }
}
