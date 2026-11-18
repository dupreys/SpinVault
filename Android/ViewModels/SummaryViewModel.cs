using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpinVaultMobile.ViewModels;

public sealed record SummaryLine(string Label, string Value);

/// <summary>Resumo da coleção: totais, gastos e aniversários de lançamento.</summary>
public sealed partial class SummaryViewModel : ObservableObject
{
    [ObservableProperty] private string itemsText = "0";
    [ObservableProperty] private string valueText = "";
    [ObservableProperty] private string ticketText = "";
    [ObservableProperty] private string mediaText = "";
    [ObservableProperty] private IReadOnlyList<SummaryLine> spendByYear = Array.Empty<SummaryLine>();
    [ObservableProperty] private IReadOnlyList<SummaryLine> topArtists = Array.Empty<SummaryLine>();
    [ObservableProperty] private IReadOnlyList<SummaryLine> anniversaries = Array.Empty<SummaryLine>();
    [ObservableProperty] private string anniversaryTitle = "";
    [ObservableProperty] private bool hasAnniversaries;

    public void Reload()
    {
        var albums = AppData.Repo.GetAll(false);
        var data = InsightsService.Compute(albums, DateTime.Today, top: 8);

        ItemsText = data.Count.ToString();
        ValueText = Fmt.Money(data.TotalValue);
        TicketText = Fmt.Money(data.AverageTicket);
        MediaText = $"{data.Vinyl} {Loc.T("vinis")}  ·  {data.CD} CDs  ·  {data.Cassette} {Loc.T("fitas")}";

        SpendByYear = data.SpendByYear.Select(s => new SummaryLine(s.Label, Fmt.Money(s.Total))).ToList();
        TopArtists = data.TopArtistsByCount.Select(s => new SummaryLine(s.Label, s.Count.ToString())).ToList();

        Anniversaries = data.Anniversaries
            .Select(a => new SummaryLine($"{a.Album.Title} — {a.Album.Artist}", $"{a.YearsText}  ·  {Loc.F("dia {0}", a.Day)}"))
            .ToList();
        HasAnniversaries = Anniversaries.Count > 0;
        AnniversaryTitle = Loc.F("Este mês, alguns álbuns fazem aniversário  ·  {0}", data.MonthName);
    }
}
