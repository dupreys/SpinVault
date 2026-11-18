using System.Collections.ObjectModel;
using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

/// <summary>Linha da lista "este mês há X anos".</summary>
public sealed record AnniversaryRow(Album Album, string Title, string Subtitle, string DayText, string YearsText, bool IsToday, bool IsRound, string DateText);

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
    [ObservableProperty] private bool hasMoreAnniversaries;
    [ObservableProperty] private bool hasItems;

    public bool HasNoAnniversaries => !HasAnniversaries;
    public bool HasNoItems => !HasItems;

    public ObservableCollection<BarItem> SpendByYear { get; } = new();
    public ObservableCollection<BarItem> SpendByMedia { get; } = new();
    public ObservableCollection<BarItem> SpendByArtist { get; } = new();
    public ObservableCollection<BarItem> TopArtists { get; } = new();
    public ObservableCollection<AnniversaryRow> Anniversaries { get; } = new();
    private List<AnniversaryRow> _allAnniversaries = new();

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

        _allAnniversaries = _data.Anniversaries.Select(a => new AnniversaryRow(
            a.Album, a.Album.Title,
            $"{a.Album.Artist}  ·  {MediaFormatInfo.Label(a.Album.Format)}",
            a.IsToday ? Loc.T("HOJE") : Loc.F("dia {0}", a.Day),
            a.YearsText, a.IsToday, a.IsRound,
            Fmt.Date(a.Album.ReleaseDate))).ToList();

        Anniversaries.Clear();
        foreach (var row in _allAnniversaries.Take(MaxAnniversaries))
            Anniversaries.Add(row);

        HasAnniversaries = Anniversaries.Count > 0;
        var hidden = _allAnniversaries.Count - MaxAnniversaries;
        HasMoreAnniversaries = hidden > 0;
        MoreText = hidden > 0 ? (hidden == 1 ? Loc.T("Ver mais 1 álbum…") : Loc.F("Ver mais {0} álbuns…", hidden)) : "";
        AnniversaryTitle = Loc.F("Este mês, alguns álbuns fazem aniversário  ·  {0}", _data.MonthName);
        AnniversaryHint = HasItems
            ? Loc.F("Nenhum álbum seu foi lançado em {0} (só contam datas completas; 01/01 é tratado como \"só o ano\").", _data.MonthName)
            : Loc.T("Cadastre ou importe álbuns para ver os aniversários de lançamento.");
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

    /// <summary>Abre uma janela pequena com todos os álbuns que fazem aniversário no mês.</summary>
    [RelayCommand]
    private void ShowAllAnniversaries()
    {
        if (_allAnniversaries.Count == 0)
            return;
        var count = _allAnniversaries.Count;
        var subtitle = count == 1
            ? Loc.F("1 álbum lançado em {0}", _data.MonthName)
            : Loc.F("{0} álbuns lançados em {1}", count, _data.MonthName);
        Dialogs.ShowAnniversaries(AnniversaryTitle, subtitle, _allAnniversaries);
    }

    // ---------- Compartilhar ----------

    [RelayCommand]
    private void ExportImage()
    {
        if (!HasItems)
        {
            Dialogs.Info(Loc.T("Compartilhar"), Loc.T("A coleção está vazia. Cadastre ou importe álbuns primeiro."));
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
            Dialogs.Error(Loc.T("Não consegui gerar a imagem"), ex.Message);
        }
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (!HasItems)
        {
            Dialogs.Info(Loc.T("Compartilhar"), Loc.T("A coleção está vazia. Cadastre ou importe álbuns primeiro."));
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
            Dialogs.Error(Loc.T("Não consegui gerar o PDF"), ex.Message);
        }
    }

    private static void OfferToOpen(string path)
    {
        if (Dialogs.Confirm(Loc.T("Resumo salvo"), Loc.F("Arquivo salvo em:\n{0}\n\nAbrir agora?", path)))
            Dialogs.OpenFile(path);
    }
}
