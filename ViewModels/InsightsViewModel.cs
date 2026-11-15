using System.Collections.ObjectModel;
using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

/// <summary>Linha da lista "este mês há X anos".</summary>
public sealed record AnniversaryRow(Album Album, string Title, string Subtitle, string DayText, string YearsText, bool IsToday, bool IsRound);

/// <summary>Página "Resumo": valor da coleção, rankings, aniversários de lançamento e compartilhamento.</summary>
public partial class InsightsViewModel : ObservableObject
{
    private const int MaxAnniversaries = 12;

    private readonly AlbumRepository _repository;
    private List<Album> _albums = new();
    private InsightsData _data = InsightsService.Compute(Array.Empty<Album>(), DateTime.Today);

    [ObservableProperty] private string totalValueText = "—";
    [ObservableProperty] private string itemsText = "0";
    [ObservableProperty] private string averageText = "—";
    [ObservableProperty] private string anniversaryTitle = "";
    [ObservableProperty] private string anniversaryHint = "";
    [ObservableProperty] private string moreText = "";
    [ObservableProperty] private bool hasAnniversaries;
    [ObservableProperty] private bool hasItems;

    public bool HasNoAnniversaries => !HasAnniversaries;
    public bool HasNoItems => !HasItems;

    public ObservableCollection<BarItem> SpendByYear { get; } = new();
    public ObservableCollection<BarItem> SpendByMedia { get; } = new();
    public ObservableCollection<BarItem> SpendByArtist { get; } = new();
    public ObservableCollection<BarItem> TopArtists { get; } = new();
    public ObservableCollection<AnniversaryRow> Anniversaries { get; } = new();

    public InsightsViewModel(AlbumRepository repository)
    {
        _repository = repository;
        Refresh();
    }

    partial void OnHasAnniversariesChanged(bool value) => OnPropertyChanged(nameof(HasNoAnniversaries));
    partial void OnHasItemsChanged(bool value) => OnPropertyChanged(nameof(HasNoItems));

    /// <summary>Relê o banco e recalcula (também muda o "este mês" se o mês virou).</summary>
    public void Refresh()
    {
        _albums = _repository.GetAll().ToList();
        _data = InsightsService.Compute(_albums, DateTime.Today);

        HasItems = _data.Count > 0;
        TotalValueText = HasItems ? Fmt.Money(_data.TotalValue) : "—";
        ItemsText = _data.Count.ToString();
        AverageText = HasItems ? Fmt.Money(_data.AverageTicket) : "—";

        Fill(SpendByYear, _data.SpendByYear, money: true);
        Fill(SpendByMedia, _data.SpendByMedia, money: true);
        Fill(SpendByArtist, _data.SpendByArtist, money: true);
        FillCounts(TopArtists, _data.TopArtistsByCount);

        Anniversaries.Clear();
        foreach (var a in _data.Anniversaries.Take(MaxAnniversaries))
        {
            Anniversaries.Add(new AnniversaryRow(
                a.Album, a.Album.Title,
                $"{a.Album.Artist}  ·  {MediaFormatInfo.Label(a.Album.Format)}",
                a.IsToday ? "HOJE" : $"dia {a.Day}",
                a.YearsText, a.IsToday, a.IsRound));
        }
        HasAnniversaries = Anniversaries.Count > 0;
        MoreText = _data.Anniversaries.Count > MaxAnniversaries ? $"e mais {_data.Anniversaries.Count - MaxAnniversaries} álbuns" : "";
        AnniversaryTitle = $"Este mês há X anos  ·  {_data.MonthName}";
        AnniversaryHint = HasItems
            ? $"Nenhum álbum seu foi lançado em {_data.MonthName} (só contam datas completas; 01/01 é tratado como \"só o ano\")."
            : "Cadastre ou importe álbuns para ver os aniversários de lançamento.";
    }

    private static void Fill(ObservableCollection<BarItem> target, IReadOnlyList<SpendItem> items, bool money)
    {
        target.Clear();
        var max = items.Count == 0 ? 1 : (double)Math.Max(items.Max(i => i.Total), 0.01m);
        foreach (var item in items)
            target.Add(new BarItem(item.Label, (double)item.Total, max, Fmt.Money(item.Total)));
    }

    private static void FillCounts(ObservableCollection<BarItem> target, IReadOnlyList<SpendItem> items)
    {
        target.Clear();
        var max = items.Count == 0 ? 1 : items.Max(i => i.Count);
        foreach (var item in items)
            target.Add(new BarItem(item.Label, item.Count, max, item.Count.ToString()));
    }

    // ---------- Compartilhar ----------

    [RelayCommand]
    private void ExportImage()
    {
        if (!HasItems)
        {
            Dialogs.Info("Compartilhar", "A coleção está vazia. Cadastre ou importe álbuns primeiro.");
            return;
        }

        var path = Dialogs.PickShareFile(pdf: false);
        if (path is null)
            return;

        try
        {
            Refresh();
            ShareImageService.SavePng(path, ShareImageService.BuildModel(_albums, _data));
            OfferToOpen(path);
        }
        catch (Exception ex)
        {
            Dialogs.Error("Não consegui gerar a imagem", ex.Message);
        }
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (!HasItems)
        {
            Dialogs.Info("Compartilhar", "A coleção está vazia. Cadastre ou importe álbuns primeiro.");
            return;
        }

        var path = Dialogs.PickShareFile(pdf: true);
        if (path is null)
            return;

        try
        {
            Refresh();
            SummaryPdf.Save(path, _data);
            OfferToOpen(path);
        }
        catch (Exception ex)
        {
            Dialogs.Error("Não consegui gerar o PDF", ex.Message);
        }
    }

    private static void OfferToOpen(string path)
    {
        if (Dialogs.Confirm("Resumo salvo", $"Arquivo salvo em:\n{path}\n\nAbrir agora?"))
            Dialogs.OpenFile(path);
    }
}
