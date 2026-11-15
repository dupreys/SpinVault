using System.Globalization;
using ColecaoMidias.Models;

namespace ColecaoMidias.Services;

/// <summary>Relatório em PDF (A4) com o resumo da coleção: valor, rankings e aniversários do mês.</summary>
public static class SummaryPdf
{
    private const double PageW = 595.28, PageH = 841.89, Margin = 42, Bottom = 56;
    private static readonly PdfColor Ink = PdfColor.Hex("#1B1F2A");
    private static readonly PdfColor Muted = PdfColor.Hex("#6B7280");
    private static readonly PdfColor Track = PdfColor.Hex("#ECEEF3");
    private static readonly PdfColor Amber = PdfColor.Hex("#F59E0B");
    private static readonly PdfColor AmberDark = PdfColor.Hex("#B45309");
    private static readonly PdfColor Night = PdfColor.Hex("#0E1016");
    private static readonly PdfColor Card = PdfColor.Hex("#F5F6F9");
    private static readonly PdfColor White = PdfColor.Hex("#FFFFFF");

    private sealed record Row(string Label, string Value, double Fraction, string Detail = "");

    private sealed class Cursor
    {
        public PdfWriter Pdf = null!;
        public PdfPage Page = null!;
        public double Y;
        public int Number;
    }

    public static byte[] Build(InsightsData data, string title = "Minha coleção de mídias")
    {
        var cursor = new Cursor { Pdf = new PdfWriter { Title = $"{title} - SpinVault" } };
        NewPage(cursor, first: true, data, title);

        Kpis(cursor, data);

        Section(cursor, "Valor da coleção — gasto por ano de compra", data.SpendByYear.Count > 0 ? 2 : 0, data, title);
        BarSection(cursor, data.SpendByYear.Select(x => (x.Label, x.Total, x.Count)), money: true, data, title, "Sem compras registradas.");

        Section(cursor, "Gasto por mídia", 2, data, title);
        BarSection(cursor, data.SpendByMedia.Select(x => (x.Label, x.Total, x.Count)), money: true, data, title, "Sem itens.");

        Section(cursor, "Onde mais gastei — top artistas por valor", 2, data, title);
        BarSection(cursor, data.SpendByArtist.Select(x => (x.Label, x.Total, x.Count)), money: true, data, title, "Nenhum valor informado.");

        Section(cursor, "Ranking — artistas com mais álbuns", 2, data, title);
        BarSection(cursor, data.TopArtistsByCount.Select(x => (x.Label, (decimal)x.Count, x.Count)), money: false, data, title, "Sem itens.",
            detail: data.TopArtistsByCount.ToDictionary(x => x.Label, x => Fmt.Money(x.Total)));

        Anniversaries(cursor, data, title);

        Footer(cursor);
        return cursor.Pdf.ToBytes();
    }

    public static void Save(string path, InsightsData data, string title = "Minha coleção de mídias") =>
        File.WriteAllBytes(path, Build(data, title));

    // ---------- Páginas ----------

    private static void NewPage(Cursor c, bool first, InsightsData data, string title)
    {
        if (c.Page is not null)
            Footer(c);

        c.Page = c.Pdf.AddPage(PageW, PageH);
        c.Number++;

        if (first)
        {
            // Faixa de cabeçalho escura com a marca
            c.Page.FillRect(0, 0, PageW, 96, Night);
            c.Page.FillRoundRect(Margin, 28, 40, 40, 10, Amber);
            c.Page.FillRoundRect(Margin + 12, 40, 16, 16, 8, Night);
            c.Page.FillRoundRect(Margin + 17.5, 45.5, 5, 5, 2.5, Amber);
            c.Page.Text(Margin + 54, 47, "SpinVault", 20, White, bold: true);
            c.Page.Text(Margin + 54, 63, title, 10.5, PdfColor.Hex("#9EA6BB"));
            c.Page.TextRight(PageW - Margin, 47, data.Today.ToString("dd 'de' MMMM 'de' yyyy", Fmt.PtBr), 10, PdfColor.Hex("#9EA6BB"));
            c.Y = 124;
        }
        else
        {
            c.Page.Text(Margin, 40, "SpinVault", 10, AmberDark, bold: true);
            c.Page.Text(Margin + 56, 40, title, 10, Muted);
            c.Page.Line(Margin, 48, PageW - Margin, 48, Track);
            c.Y = 78;
        }
    }

    private static void Footer(Cursor c)
    {
        c.Page.Line(Margin, PageH - 40, PageW - Margin, PageH - 40, Track);
        c.Page.Text(Margin, PageH - 26, "Gerado pelo SpinVault", 8.5, Muted);
        c.Page.TextRight(PageW - Margin, PageH - 26, $"Página {c.Number}", 8.5, Muted);
    }

    /// <summary>Garante espaço para <paramref name="needed"/> pontos; senão abre uma nova página.</summary>
    private static void Ensure(Cursor c, double needed, InsightsData data, string title)
    {
        if (c.Y + needed > PageH - Bottom)
            NewPage(c, first: false, data, title);
    }

    private static void Section(Cursor c, string heading, int minRows, InsightsData data, string title)
    {
        // O título nunca fica sozinho no fim da página: reserva espaço para ele e algumas linhas.
        Ensure(c, 40 + minRows * 22, data, title);
        c.Y += 20;
        c.Page.FillRect(Margin, c.Y - 11, 3, 14, Amber);
        c.Page.Text(Margin + 10, c.Y, heading, 12.5, Ink, bold: true);
        c.Y += 16;
    }

    // ---------- Blocos ----------

    private static void Kpis(Cursor c, InsightsData data)
    {
        var items = new (string Label, string Value)[]
        {
            ("ITENS", data.Count.ToString(Fmt.PtBr)),
            ("VALOR INVESTIDO", Fmt.Money(data.TotalValue)),
            ("TICKET MÉDIO", Fmt.Money(data.AverageTicket)),
            ("VINIS · CDS · FITAS", $"{data.Vinyl} · {data.CD} · {data.Cassette}")
        };

        const double gap = 10;
        var width = (PageW - 2 * Margin - gap * (items.Length - 1)) / items.Length;
        for (var i = 0; i < items.Length; i++)
        {
            var x = Margin + i * (width + gap);
            c.Page.FillRoundRect(x, c.Y, width, 58, 10, Card);
            c.Page.Text(x + 12, c.Y + 19, c.Page.Fit(items[i].Label, width - 20, 7.5), 7.5, Muted, bold: true);
            c.Page.Text(x + 12, c.Y + 41, c.Page.Fit(items[i].Value, width - 20, 14, true), 14, Ink, bold: true);
        }
        c.Y += 58;
    }

    private static void BarSection(Cursor c, IEnumerable<(string Label, decimal Value, int Count)> source, bool money,
                                   InsightsData data, string title, string emptyText,
                                   Dictionary<string, string>? detail = null)
    {
        var list = source.ToList();
        if (list.Count == 0)
        {
            c.Page.Text(Margin + 10, c.Y + 12, emptyText, 10, Muted);
            c.Y += 22;
            return;
        }

        var max = (double)list.Max(x => x.Value);
        if (max <= 0)
            max = 1;

        const double labelW = 150, valueW = 170, rowH = 22;
        var barX = Margin + labelW + 10;
        var barW = PageW - Margin - valueW - barX - 8;

        foreach (var (label, value, count) in list)
        {
            Ensure(c, rowH, data, title);
            var y = c.Y + 14;

            c.Page.Text(Margin + 4, y, c.Page.Fit(label, labelW, 10), 10, Ink);
            c.Page.FillRoundRect(barX, y - 8, barW, 8, 4, Track);
            c.Page.FillRoundRect(barX, y - 8, Math.Max(barW * (double)value / max, 4), 8, 4, Amber);

            var main = money ? Fmt.Money(value) : value.ToString("0", Fmt.PtBr) + (value == 1 ? " álbum" : " álbuns");
            var side = money ? $"{count} {(count == 1 ? "item" : "itens")}" : (detail != null && detail.TryGetValue(label, out var d) ? d : "");

            c.Page.TextRight(PageW - Margin - 4, y, main, 10, Ink, bold: true);
            if (side.Length > 0)
                c.Page.TextRight(PageW - Margin - 4, y + 9, side, 7.5, Muted);

            c.Y += rowH;
        }
    }

    private static void Anniversaries(Cursor c, InsightsData data, string title)
    {
        Section(c, $"Este mês há X anos — lançamentos de {data.MonthName}", 2, data, title);

        if (data.Anniversaries.Count == 0)
        {
            Ensure(c, 40, data, title);
            c.Page.Text(Margin + 10, c.Y + 12, $"Nenhum álbum da coleção foi lançado em {data.MonthName} (com data completa).", 10, Muted);
            c.Y += 22;
            return;
        }

        foreach (var a in data.Anniversaries)
        {
            Ensure(c, 24, data, title);
            var y = c.Y + 14;

            var date = $"{a.Day:00}/{data.Today.Month:00}";
            c.Page.Text(Margin + 4, y, date, 10, a.IsToday ? AmberDark : Muted, bold: true);
            if (a.IsToday)
                c.Page.Text(Margin + 4, y + 9, "HOJE", 6.5, AmberDark, bold: true);

            var yearsW = c.Page.TextWidth(a.YearsText, 10, a.IsRound);
            var maxText = PageW - 2 * Margin - 60 - yearsW - 16;
            var line = $"{a.Album.Title} — {a.Album.Artist}";
            c.Page.Text(Margin + 50, y, c.Page.Fit(line, maxText, 10), 10, Ink);
            c.Page.TextRight(PageW - Margin - 4, y, a.YearsText, 10, a.IsRound ? AmberDark : Muted, a.IsRound);
            c.Y += 24;
        }
    }
}
