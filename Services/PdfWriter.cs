using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace ColecaoMidias.Services;

/// <summary>Cor RGB com componentes de 0 a 255.</summary>
public readonly record struct PdfColor(int R, int G, int B)
{
    public static PdfColor Hex(string hex) =>
        new(Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));

    internal string Ops =>
        string.Create(CultureInfo.InvariantCulture, $"{R / 255.0:0.###} {G / 255.0:0.###} {B / 255.0:0.###}");
}

/// <summary>Uma página do PDF. As coordenadas usam o canto superior esquerdo como origem, em pontos (1 pt = 1/72 pol).</summary>
public sealed class PdfPage
{
    private readonly StringBuilder _ops = new();

    public double Width { get; }
    public double Height { get; }

    internal PdfPage(double width, double height)
    {
        Width = width;
        Height = height;
    }

    internal string Content => _ops.ToString();

    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public void FillRect(double x, double y, double w, double h, PdfColor color)
    {
        if (w <= 0 || h <= 0)
            return;
        _ops.Append($"{color.Ops} rg {N(x)} {N(Height - y - h)} {N(w)} {N(h)} re f\n");
    }

    /// <summary>Retângulo com cantos arredondados.</summary>
    public void FillRoundRect(double x, double y, double w, double h, double r, PdfColor color)
    {
        if (w <= 0 || h <= 0)
            return;
        r = Math.Min(r, Math.Min(w, h) / 2);
        const double k = 0.5522847498;
        var by = Height - y - h;       // base (em coordenadas do PDF)
        var ty = by + h;               // topo
        var c = r * k;
        var sb = _ops;
        sb.Append($"{color.Ops} rg {N(x + r)} {N(by)} m ");
        sb.Append($"{N(x + w - r)} {N(by)} l {N(x + w - r + c)} {N(by)} {N(x + w)} {N(by + r - c)} {N(x + w)} {N(by + r)} c ");
        sb.Append($"{N(x + w)} {N(ty - r)} l {N(x + w)} {N(ty - r + c)} {N(x + w - r + c)} {N(ty)} {N(x + w - r)} {N(ty)} c ");
        sb.Append($"{N(x + r)} {N(ty)} l {N(x + r - c)} {N(ty)} {N(x)} {N(ty - r + c)} {N(x)} {N(ty - r)} c ");
        sb.Append($"{N(x)} {N(by + r)} l {N(x)} {N(by + r - c)} {N(x + r - c)} {N(by)} {N(x + r)} {N(by)} c f\n");
    }

    public void Line(double x1, double y1, double x2, double y2, PdfColor color, double width = 0.6)
    {
        _ops.Append($"{color.Ops} RG {N(width)} w {N(x1)} {N(Height - y1)} m {N(x2)} {N(Height - y2)} l S\n");
    }

    /// <summary>Escreve texto; (x, y) é o início da linha de base do texto.</summary>
    public void Text(double x, double y, string text, double size, PdfColor color, bool bold = false)
    {
        if (text.Length == 0)
            return;
        _ops.Append($"BT /{(bold ? "F2" : "F1")} {N(size)} Tf {color.Ops} rg {N(x)} {N(Height - y)} Td ({PdfWriter.Escape(text)}) Tj ET\n");
    }

    public double TextWidth(string text, double size, bool bold = false) => PdfWriter.TextWidth(text, size, bold);

    /// <summary>Texto alinhado à direita: termina em <paramref name="right"/>.</summary>
    public void TextRight(double right, double y, string text, double size, PdfColor color, bool bold = false) =>
        Text(right - TextWidth(text, size, bold), y, text, size, color, bold);

    /// <summary>Corta o texto com "…" para caber em <paramref name="maxWidth"/>.</summary>
    public string Fit(string text, double maxWidth, double size, bool bold = false)
    {
        if (TextWidth(text, size, bold) <= maxWidth)
            return text;

        var cut = text;
        while (cut.Length > 1 && TextWidth(cut + "…", size, bold) > maxWidth)
            cut = cut[..^1];
        return cut.TrimEnd() + "…";
    }
}

/// <summary>
/// Gerador de PDF mínimo (sem bibliotecas): páginas com texto (Helvetica) e formas simples.
/// Usa só as fontes padrão do PDF, então não precisa embutir nada e o arquivo sai pequeno.
/// </summary>
public sealed class PdfWriter
{
    private readonly List<PdfPage> _pages = new();

    public string Title { get; set; } = "";

    public PdfPage AddPage(double width = 595.28, double height = 841.89)
    {
        var page = new PdfPage(width, height);
        _pages.Add(page);
        return page;
    }

    public int PageCount => _pages.Count;

    // ---------- Texto ----------

    // Caracteres do Windows-1252 que ficam fora do bloco Latin-1.
    private static readonly Dictionary<char, byte> Cp1252 = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87,
        ['ˆ'] = 0x88, ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C, ['Ž'] = 0x8E,
        ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93, ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97,
        ['˜'] = 0x98, ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B, ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F
    };

    internal static byte[] ToWinAnsi(string text)
    {
        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is >= ' ' and < (char)0x7F || c is >= (char)0xA0 and <= (char)0xFF)
                bytes[i] = (byte)c;
            else if (Cp1252.TryGetValue(c, out var mapped))
                bytes[i] = mapped;
            else
                bytes[i] = (byte)'?';
        }
        return bytes;
    }

    internal static string Escape(string text)
    {
        var sb = new StringBuilder();
        foreach (var b in ToWinAnsi(text))
        {
            if (b is (byte)'(' or (byte)')' or (byte)'\\')
                sb.Append('\\').Append((char)b);
            else if (b < 32 || b > 126)
                sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));   // byte fora do ASCII → escape octal
            else
                sb.Append((char)b);
        }
        return sb.ToString();
    }

    public static double TextWidth(string text, double size, bool bold = false)
    {
        var total = 0;
        foreach (var b in ToWinAnsi(text))
            total += PdfFontMetrics.Width(b, bold);
        return total * size / 1000.0;
    }

    // ---------- Gravação ----------

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        Write(stream);
        return stream.ToArray();
    }

    public void Save(string path) => File.WriteAllBytes(path, ToBytes());

    public void Write(Stream output)
    {
        if (_pages.Count == 0)
            AddPage();

        // Objetos: 1 catálogo, 2 páginas, 3 fonte normal, 4 fonte negrito, 5 info; depois (página, conteúdo) de cada página.
        var offsets = new List<long>();
        var latin = Encoding.Latin1;
        long position = 0;

        void Write(byte[] data)
        {
            output.Write(data, 0, data.Length);
            position += data.Length;
        }
        void WriteText(string s) => Write(latin.GetBytes(s));
        void BeginObject(int id)
        {
            offsets.Add(position);
            WriteText($"{id} 0 obj\n");
        }

        WriteText("%PDF-1.4\n");
        Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        var firstPageObject = 6;
        var kids = string.Join(" ", Enumerable.Range(0, _pages.Count).Select(i => $"{firstPageObject + i * 2} 0 R"));

        BeginObject(1);
        WriteText("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        BeginObject(2);
        WriteText($"<< /Type /Pages /Kids [{kids}] /Count {_pages.Count} >>\nendobj\n");

        BeginObject(3);
        WriteText("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(4);
        WriteText("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(5);
        WriteText($"<< /Title ({Escape(Title)}) /Producer (SpinVault) /Creator (SpinVault) >>\nendobj\n");

        for (var i = 0; i < _pages.Count; i++)
        {
            var page = _pages[i];
            var pageId = firstPageObject + i * 2;
            var contentId = pageId + 1;

            BeginObject(pageId);
            WriteText($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {page.Width.ToString("0.##", CultureInfo.InvariantCulture)} {page.Height.ToString("0.##", CultureInfo.InvariantCulture)}] " +
                      $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentId} 0 R >>\nendobj\n");

            // Conteúdo comprimido (Flate). Os operadores usam só ASCII; o texto já vai em WinAnsi escapado.
            byte[] compressed;
            using (var memory = new MemoryStream())
            {
                using (var z = new ZLibStream(memory, CompressionLevel.Optimal, leaveOpen: true))
                {
                    var raw = latin.GetBytes(page.Content);
                    z.Write(raw, 0, raw.Length);
                }
                compressed = memory.ToArray();
            }

            BeginObject(contentId);
            WriteText($"<< /Length {compressed.Length} /Filter /FlateDecode >>\nstream\n");
            Write(compressed);
            WriteText("\nendstream\nendobj\n");
        }

        var xrefStart = position;
        var objectCount = offsets.Count + 1;
        WriteText($"xref\n0 {objectCount}\n");
        WriteText("0000000000 65535 f \n");
        foreach (var offset in offsets)
            WriteText($"{offset:D10} 00000 n \n");
        WriteText($"trailer\n<< /Size {objectCount} /Root 1 0 R /Info 5 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");
    }
}
