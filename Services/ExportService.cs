using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>
/// Exporta a coleção para Excel (.xlsx) ou CSV. A planilha sai no mesmo formato da sua original
/// (aba "Base De Dados", cabeçalho na linha 2), então dá para importá-la de volta no app.
/// </summary>
public static class ExportService
{
    private static readonly string[] Headers = { "Album", "Artista", "Mídia", "Lançamento", "Ano de Compra", "Valor (R$)" };

    public static void Export(string path, IEnumerable<Album> albums)
    {
        var list = albums.ToList();
        if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
            WriteCsv(path, list);
        else
            WriteXlsx(path, list);
    }

    // ---------- CSV (separador ";" e UTF-8 com BOM: abre certo no Excel em português) ----------

    internal static string BuildCsv(IReadOnlyList<Album> albums)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', Headers.Select(CsvField)));

        foreach (var a in albums)
        {
            builder.AppendLine(string.Join(';', new[]
            {
                CsvField(a.Title),
                CsvField(a.Artist),
                CsvField(MediaFormatInfo.Label(a.Format)),
                CsvField(a.ReleaseDate.HasValue ? Fmt.Date(a.ReleaseDate) : ""),
                CsvField(a.PurchaseYear?.ToString(CultureInfo.InvariantCulture) ?? ""),
                CsvField(a.Price.ToString("0.00", Fmt.PtBr))
            }));
        }
        return builder.ToString();
    }

    private static void WriteCsv(string path, IReadOnlyList<Album> albums) =>
        File.WriteAllText(path, BuildCsv(albums), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private static string CsvField(string text)
    {
        // Protege contra "injeção de fórmula" quando o CSV é aberto no Excel.
        if (text.Length > 0 && text[0] is '=' or '+' or '@')
            text = "'" + text;

        return text.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }

    // ---------- XLSX (escrito à mão: um .xlsx é um zip com alguns XMLs) ----------

    private static void WriteXlsx(string path, IReadOnlyList<Album> albums)
    {
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create);

        Add(zip, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            </Types>
            """);

        Add(zip, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);

        Add(zip, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Base De Dados" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);

        Add(zip, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """);

        // Estilos: 0 normal, 1 cabeçalho em negrito, 2 data dd/mm/aaaa, 3 valor com 2 casas.
        Add(zip, "xl/styles.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
              <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
              <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
              <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
              <cellXfs count="4">
                <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
                <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
                <xf numFmtId="14" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
                <xf numFmtId="2" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>
              </cellXfs>
            </styleSheet>
            """);

        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sheet.Append("<cols><col min=\"1\" max=\"2\" width=\"34\" customWidth=\"1\"/><col min=\"3\" max=\"6\" width=\"16\" customWidth=\"1\"/></cols>");
        sheet.Append("<sheetData>");

        // Linha 1: título; linha 2: cabeçalho (igual à planilha original).
        sheet.Append("<row r=\"1\">").Append(Text("A1", "Minha coleção de mídias", 1)).Append("</row>");
        sheet.Append("<row r=\"2\">");
        for (var c = 0; c < Headers.Length; c++)
            sheet.Append(Text($"{(char)('A' + c)}2", Headers[c], 1));
        sheet.Append("</row>");

        var rowNumber = 3;
        foreach (var a in albums)
        {
            var r = rowNumber++;
            sheet.Append($"<row r=\"{r}\">");
            sheet.Append(Text($"A{r}", a.Title));
            sheet.Append(Text($"B{r}", a.Artist));
            sheet.Append(Text($"C{r}", MediaFormatInfo.Label(a.Format)));
            if (a.ReleaseDate is { } date)
                sheet.Append($"<c r=\"D{r}\" s=\"2\"><v>{date.Date.ToOADate().ToString(CultureInfo.InvariantCulture)}</v></c>");
            if (a.PurchaseYear is { } year)
                sheet.Append($"<c r=\"E{r}\"><v>{year}</v></c>");
            sheet.Append($"<c r=\"F{r}\" s=\"3\"><v>{a.Price.ToString(CultureInfo.InvariantCulture)}</v></c>");
            sheet.Append("</row>");
        }

        sheet.Append("</sheetData></worksheet>");
        Add(zip, "xl/worksheets/sheet1.xml", sheet.ToString());
    }

    private static string Text(string cell, string value, int style = 0) =>
        $"<c r=\"{cell}\" t=\"inlineStr\"{(style > 0 ? $" s=\"{style}\"" : "")}><is><t xml:space=\"preserve\">{SecurityElement.Escape(StripInvalid(value))}</t></is></c>";

    // Caracteres de controle quebram o XML.
    private static string StripInvalid(string text) =>
        new(text.Where(c => c >= ' ' || c is '\t' or '\n' or '\r').ToArray());

    private static void Add(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.TrimStart());
    }
}
