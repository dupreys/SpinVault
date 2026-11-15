using System.IO.Compression;
using System.Xml.Linq;

namespace ColecaoMidias.Services;

/// <summary>
/// Leitor mínimo de .xlsx, sem dependências: abre o ZIP e lê só a aba pedida.
/// Ignora tabelas dinâmicas, gráficos e estilos (por isso não quebra com planilhas "enfeitadas").
/// Cada linha vira um dicionário "letra da coluna → texto da célula" (números em formato invariante).
/// </summary>
public static class XlsxReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public sealed record Row(int Number, IReadOnlyDictionary<string, string> Cells)
    {
        public string Get(string column) => Cells.TryGetValue(column, out var v) ? v : "";
    }

    public static IReadOnlyList<Row> ReadSheet(Stream xlsx, string sheetName)
    {
        using var zip = new ZipArchive(xlsx, ZipArchiveMode.Read, leaveOpen: true);

        var workbook = Load(zip, "xl/workbook.xml");
        var sheets = workbook.Descendants(Main + "sheet").ToList();
        var sheet = sheets.FirstOrDefault(s =>
                        string.Equals((string?)s.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase))
                    ?? sheets.FirstOrDefault()
                    ?? throw new InvalidDataException("A planilha não tem nenhuma aba.");

        var relId = (string?)sheet.Attribute(Rel + "id");
        var rels = Load(zip, "xl/_rels/workbook.xml.rels");
        var target = rels.Descendants(PkgRel + "Relationship")
                         .FirstOrDefault(r => (string?)r.Attribute("Id") == relId)
                         ?.Attribute("Target")?.Value
                     ?? throw new InvalidDataException("Não foi possível localizar a aba dentro do arquivo.");

        var sheetPath = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        var sharedStrings = ReadSharedStrings(zip);

        var entry = zip.GetEntry(sheetPath) ?? throw new InvalidDataException($"Parte \"{sheetPath}\" não encontrada.");
        using var sheetStream = entry.Open();

        var rows = new List<Row>();
        // Lê em fluxo: planilhas com tabelas gigantes (ex.: A2:F1048575) não carregam tudo na memória.
        using var reader = System.Xml.XmlReader.Create(sheetStream, new System.Xml.XmlReaderSettings { IgnoreWhitespace = true });
        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.LocalName == "row")
            {
                // ReadFrom consome o elemento inteiro e já posiciona o leitor no nó seguinte.
                rows.Add(ParseRow((XElement)XNode.ReadFrom(reader), rows.Count + 1, sharedStrings));
            }
            else
            {
                reader.Read();
            }
        }

        return rows;
    }

    private static Row ParseRow(XElement rowElement, int fallbackNumber, IReadOnlyList<string> sharedStrings)
    {
        var number = (int?)rowElement.Attribute("r") ?? fallbackNumber;
        var cells = new Dictionary<string, string>();

        foreach (var c in rowElement.Elements(Main + "c"))
        {
            var reference = (string?)c.Attribute("r") ?? "";
            var column = new string(reference.TakeWhile(char.IsLetter).ToArray());
            if (column.Length == 0)
                continue;

            var text = CellText(c, sharedStrings);
            if (text.Length > 0)
                cells[column] = text;
        }

        return new Row(number, cells);
    }

    private static string CellText(XElement c, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)c.Attribute("t");

        if (type == "inlineStr")
            return string.Concat(c.Descendants(Main + "t").Select(t => t.Value)).Trim();

        var raw = c.Element(Main + "v")?.Value;
        if (raw is null)
            return "";

        if (type == "s")
            return int.TryParse(raw, out var index) && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index].Trim()
                : "";

        return raw.Trim(); // números, datas (série), booleanos e resultados de fórmulas de texto
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return new List<string>();

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);

        // <si> pode ter texto "rico" (vários <r><t>) e fonética (<rPh>), que não faz parte do texto.
        return doc.Root!.Elements(Main + "si")
            .Select(si => string.Concat(si.Descendants(Main + "t")
                .Where(t => t.Parent?.Name != Main + "rPh")
                .Select(t => t.Value)))
            .ToList();
    }

    private static XDocument Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException($"Arquivo inválido: \"{path}\" não encontrado.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
