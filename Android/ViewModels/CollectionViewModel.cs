using System.Globalization;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SpinVaultMobile.ViewModels;

/// <summary>Botão de filtro por mídia (Todos, Vinil, CD, Cassete).</summary>
public sealed partial class FormatChip : ObservableObject
{
    public FormatChip(MediaFormat? format, string label)
    {
        Format = format;
        Label = label;
    }

    public MediaFormat? Format { get; }
    public string Label { get; }

    [ObservableProperty] private bool isSelected;
}

/// <summary>Seção da lista quando a coleção está agrupada (por gênero, década, artista...).</summary>
public sealed class AlbumGroup : List<Album>
{
    public AlbumGroup(string name) => Name = name;

    public string Name { get; }
    public string Title => $"{Name}  ·  {Count}";
}

/// <summary>Lista de álbuns da coleção (ou da lista de desejos), com busca e filtro por mídia.</summary>
public sealed partial class CollectionViewModel : ObservableObject
{
    private readonly bool _wishlist;
    private List<Album> _all = new();
    private MediaFormat? _format;
    private CancellationTokenSource? _debounce;

    public CollectionViewModel(bool wishlist)
    {
        _wishlist = wishlist;
        Chips = new[]
        {
            new FormatChip(null, Loc.T("Todos")) { IsSelected = true },
            new FormatChip(MediaFormat.Vinyl, MediaFormatInfo.Label(MediaFormat.Vinyl)),
            new FormatChip(MediaFormat.CD, MediaFormatInfo.Label(MediaFormat.CD)),
            new FormatChip(MediaFormat.Cassette, MediaFormatInfo.Label(MediaFormat.Cassette)),
        };
    }

    public bool IsWishlist => _wishlist;
    public string PageTitle => _wishlist ? Loc.T("Desejos") : Loc.T("Coleção");
    public string EmptyText => _wishlist ? Loc.T("Sua lista de desejos está vazia") : Loc.T("Sua coleção está vazia");
    public IReadOnlyList<FormatChip> Chips { get; }

    // Álbuns soltos, ou seções (AlbumGroup) quando há agrupamento.
    [ObservableProperty] private IEnumerable<object> items = Array.Empty<object>();
    [ObservableProperty] private bool isGrouped;
    [ObservableProperty] private bool isListView = Preferences.Get("view_list", false);
    [ObservableProperty] private string summary = "";
    [ObservableProperty] private string emptyMessage = "";
    [ObservableProperty] private bool isEmpty;
    [ObservableProperty] private string searchText = "";

    partial void OnSearchTextChanged(string value) => ApplyLater();

    partial void OnIsListViewChanged(bool value)
    {
        Preferences.Set("view_list", value);
        OnPropertyChanged(nameof(ViewToggleText));
    }

    /// <summary>O botão mostra para onde ele leva: ☰ abre a lista, ▦ volta para as capas.</summary>
    public string ViewToggleText => IsListView ? "▦" : "☰";

    public string SortKey => CollectionOrdering.NormalizeSort(SettingsStore.Current.CollectionSort);
    public string GroupKey => CollectionOrdering.NormalizeGroup(SettingsStore.Current.CollectionGroup);

    public void SetSort(string key)
    {
        SettingsStore.Current.CollectionSort = key;
        SettingsStore.Save();
        Apply();
    }

    public void SetGroup(string key)
    {
        SettingsStore.Current.CollectionGroup = key;
        SettingsStore.Save();
        Apply();
    }

    public void Refresh() => Apply();

    /// <summary>Lê o banco e atualiza a tela (chamado sempre que a página aparece).</summary>
    public void Reload()
    {
        _all = AppData.Repo.GetAll(_wishlist).ToList();
        Apply();
    }

    public void SelectChip(FormatChip chip)
    {
        foreach (var c in Chips)
            c.IsSelected = ReferenceEquals(c, chip);
        _format = chip.Format;
        Apply();
    }

    // Espera o usuário parar de digitar um instante antes de filtrar.
    private async void ApplyLater()
    {
        _debounce?.Cancel();
        var source = _debounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(250, source.Token);
            Apply();
        }
        catch (TaskCanceledException)
        {
            // veio outra digitação: a busca anterior não vale mais
        }
    }

    private void Apply()
    {
        var query = _all.AsEnumerable();
        if (_format is { } format)
            query = query.Where(a => a.Format == format);

        var text = SearchText.Trim();
        if (text.Length > 0)
            query = query.Where(a => Matches(a.Title, text) || Matches(a.Artist, text));

        var group = GroupKey;
        var list = CollectionOrdering.Sort(query, SortKey, group);
        if (group == CollectionOrdering.GroupNone)
        {
            IsGrouped = false;
            Items = list.Cast<object>().ToList();
        }
        else
        {
            var sections = new List<AlbumGroup>();
            foreach (var album in list)
            {
                var name = CollectionOrdering.GroupLabel(album, group) ?? "";
                if (sections.Count == 0 || sections[^1].Name != name)
                    sections.Add(new AlbumGroup(name));
                sections[^1].Add(album);
            }
            IsGrouped = true;
            Items = sections;
        }

        var total = list.Sum(a => a.Price);
        Summary = _wishlist
            ? Loc.F("{0} álbuns · {1} em preços-alvo", list.Count, Fmt.Money(total))
            : Loc.F("{0} de {1} itens  ·  {2}", list.Count, _all.Count, Fmt.Money(total));

        IsEmpty = list.Count == 0;
        EmptyMessage = _all.Count == 0 ? EmptyText : Loc.T("Nenhum item encontrado");
    }

    // Busca sem diferenciar maiúsculas nem acentos ("cafe" acha "Café").
    private static bool Matches(string text, string search) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, search,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
