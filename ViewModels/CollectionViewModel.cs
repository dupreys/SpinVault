using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using ColecaoMidias.Converters;
using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

public partial class CollectionViewModel : ObservableObject
{
    private readonly AlbumRepository _repository;

    // Lista completa, já ordenada (artista → lançamento → título).
    private List<Album> _all = new();

    // Texto de busca pré-processado de cada álbum (minúsculo e sem acentos), para comparar rápido.
    private readonly Dictionary<int, string> _searchKeys = new();
    private string[] _terms = Array.Empty<string>();

    /// <summary>
    /// O que aparece na tela (já filtrado). É atualizado de forma incremental: ao digitar na busca só
    /// entram/saem os itens que mudaram, em vez de reconstruir todos os cartões a cada letra.
    /// </summary>
    public ObservableCollection<Album> Albums { get; } = new();

    /// <summary>Disparado depois de recarregar do banco (a coleção mudou).</summary>
    public event Action? DataChanged;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private MediaFormat? formatFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(NoSelection))]
    [NotifyPropertyChangedFor(nameof(FavoriteLabel))]
    [NotifyPropertyChangedFor(nameof(LoanButtonText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleLoanCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand))]
    private Album? selectedAlbum;

    // ----- Filtros por gênero, década e lista (v1.1) -----
    private string? _genreFilter;
    private int? _decadeFilter;
    private string? _tagFilter;

    private string? _loanFilter;

    // ----- Ordenar e agrupar (v1.2) -----
    private string _sortKey;
    private string _groupKey;

    public ObservableCollection<ChoiceItem> SortChoices { get; } = new();
    public ObservableCollection<ChoiceItem> GroupChoices { get; } = new();

    [ObservableProperty] private bool isGrouped;
    [ObservableProperty] private string orderButtonText = Loc.T("⇅  Ordenar");
    [ObservableProperty] private string orderSummary = Loc.T("Ordenar a coleção e agrupar por gênero, década ou artista");

    // ----- Empréstimos (v1.2) -----
    public ObservableCollection<ChoiceItem> LoanOptions { get; } = new();
    [ObservableProperty] private bool hasLoanOptions;
    [ObservableProperty] private int loanedCount;
    [ObservableProperty] private bool hasLoaned;

    public string LoanButtonText => SelectedAlbum?.IsLoaned == true ? Loc.T("↩  Devolver") : Loc.T("⇄  Emprestar");

    public ObservableCollection<ChoiceItem> GenreOptions { get; } = new();
    public ObservableCollection<ChoiceItem> DecadeOptions { get; } = new();
    public ObservableCollection<ChoiceItem> TagOptions { get; } = new();

    /// <summary>Quantos filtros avançados (gênero, década, lista) estão ligados.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterButtonText))]
    [NotifyPropertyChangedFor(nameof(HasAdvancedFilters))]
    private int activeFilterCount;

    [ObservableProperty] private bool hasGenreOptions;
    [ObservableProperty] private bool hasDecadeOptions;
    [ObservableProperty] private bool hasTagOptions;
    [ObservableProperty] private bool hasNoFilterOptions = true;

    public bool HasAdvancedFilters => ActiveFilterCount > 0;
    public string FilterButtonText => ActiveFilterCount > 0 ? Loc.F("⚟  Filtros ({0})", ActiveFilterCount) : Loc.T("⚟  Filtros");
    public string FavoriteLabel => SelectedAlbum?.IsFavorite == true ? Loc.T("♥  Favorito") : Loc.T("♡  Favoritar");

    [ObservableProperty]
    private string summaryText = "";

    /// <summary>Quantidade de itens de cada mídia na coleção inteira (não muda com o filtro).</summary>
    [ObservableProperty] private int vinylCount;
    [ObservableProperty] private int cdCount;
    [ObservableProperty] private int cassetteCount;

    /// <summary>"all", "vinyl", "cd" ou "cassette" (ligado aos filtros da tela).</summary>
    [ObservableProperty]
    private string filterKey = "all";

    /// <summary>"gallery" (capas) ou "table" (lista).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGalleryMode))]
    [NotifyPropertyChangedFor(nameof(IsTableMode))]
    private string viewMode = "gallery";

    /// <summary>Não há nenhum item cadastrado (mostra a tela de boas-vindas).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItems))]
    private bool isLibraryEmpty;

    /// <summary>Há itens, mas o filtro/busca atual não encontrou nenhum.</summary>
    [ObservableProperty]
    private bool noResults;

    public bool HasItems => !IsLibraryEmpty;
    public bool IsGalleryMode => ViewMode == "gallery";
    public bool IsTableMode => ViewMode == "table";

    public bool HasSelection => SelectedAlbum != null;
    public bool NoSelection => SelectedAlbum == null;

    public CollectionViewModel(AlbumRepository repository)
    {
        _repository = repository;

        var settings = SettingsStore.Current;
        _sortKey = CollectionOrdering.NormalizeSort(settings.CollectionSort);
        _groupKey = CollectionOrdering.NormalizeGroup(settings.CollectionGroup);
        BuildOrderingChoices();

        Reload();
        ApplyGroupingToView();
        UpdateOrderingState();
    }

    partial void OnSearchTextChanged(string value)
    {
        _terms = Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        RefreshView();
    }

    partial void OnFormatFilterChanged(MediaFormat? value) => RefreshView();

    partial void OnFilterKeyChanged(string value) =>
        FormatFilter = value switch
        {
            "vinyl" => MediaFormat.Vinyl,
            "cd" => MediaFormat.CD,
            "cassette" => MediaFormat.Cassette,
            _ => null
        };

    // Cartões novos são criados aos poucos: a tela continua respondendo (o botão já muda de cor)
    // e as capas vão aparecendo, em vez de a janela congelar até criar todos de uma vez.
    private const int InsertsPerStep = 6;
    private int _refreshToken;

    /// <summary>
    /// Atualiza a lista mostrada mexendo só no que mudou, em lotes. Uma nova chamada cancela a anterior
    /// (quem clica rápido em Vinil, CD, Cassete não espera os cartões do filtro anterior).
    /// </summary>
    private void RefreshView(Action? whenDone = null)
    {
        UpdateSummary();   // o resumo já mostra o resultado final, mesmo com a lista ainda se completando

        var token = ++_refreshToken;
        ListSync.Purge(Albums, _all);

        var index = 0;
        var position = 0;

        void Step()
        {
            if (token != _refreshToken)
                return;

            if (ListSync.Step(Albums, _all, Matches, ref index, ref position, InsertsPerStep))
            {
                whenDone?.Invoke();
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null)
                Step();     // sem interface (testes): termina na hora
            else
                dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(Step));
        }

        Step();
    }

    private bool Matches(Album album)
    {
        if (FormatFilter is { } format && album.Format != format)
            return false;

        if (_genreFilter is { } genre && !string.Equals(album.Genre?.Trim(), genre, StringComparison.OrdinalIgnoreCase))
            return false;

        if (_decadeFilter is { } decade && album.Decade != decade)
            return false;

        if (_tagFilter is { } tag && !album.HasTag(tag))
            return false;

        if (_loanFilter == "out" && !album.IsLoaned)
            return false;

        if (_terms.Length == 0)
            return true;

        // Todas as palavras digitadas precisam aparecer no título ou no artista ("yes close" acha "Close to the edge – Yes").
        var key = _searchKeys.TryGetValue(album.Id, out var k) ? k : "";
        foreach (var term in _terms)
            if (!key.Contains(term, StringComparison.Ordinal))
                return false;

        return true;
    }

    // Minúsculo e sem acentos: "Coletânia" e "cole" se encontram.
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void UpdateSummary()
    {
        // Calculado a partir do filtro (e não do que já está na tela), para não piscar enquanto os cartões chegam.
        var count = 0;
        var total = 0m;
        foreach (var album in _all)
        {
            if (!Matches(album))
                continue;
            count++;
            total += album.Price;
        }

        SummaryText = Loc.F("{0} de {1} itens  ·  {2}", count, _all.Count, Fmt.Money(total));
        IsLibraryEmpty = _all.Count == 0;
        NoResults = _all.Count > 0 && count == 0;
    }

    /// <summary>Recarrega do banco e, opcionalmente, seleciona um álbum.</summary>
    public void Reload(int? selectId = null)
    {
        // Álbuns que não mudaram continuam sendo o mesmo objeto: seus cartões não precisam ser recriados.
        var previous = _all.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());
        var fresh = _repository.GetAll()
            .Select(a => previous.TryGetValue(a.Id, out var old) && old.Fingerprint == a.Fingerprint ? old : a);
        _all = CollectionOrdering.Sort(fresh, _sortKey, _groupKey);

        CoverImageLoader.Preload(_all.Select(a => a.CoverFile));

        VinylCount = _all.Count(a => a.Format == MediaFormat.Vinyl);
        CdCount = _all.Count(a => a.Format == MediaFormat.CD);
        CassetteCount = _all.Count(a => a.Format == MediaFormat.Cassette);

        _searchKeys.Clear();
        foreach (var album in _all)
            _searchKeys[album.Id] = Normalize($"{album.Title} {album.Artist}");

        BuildFilterOptions();   // zera filtros que não existem mais (ex.: gênero que ninguém usa)

        if (selectId is null)
            SelectedAlbum = null;

        RefreshView(whenDone: selectId is { } id
            ? () => SelectedAlbum = Albums.FirstOrDefault(a => a.Id == id)
            : null);

        DataChanged?.Invoke();
    }

    /// <summary>Monta as opções de gênero, década e lista a partir do que existe na coleção.</summary>
    private void BuildFilterOptions()
    {
        // Gêneros (sem diferenciar maiúsculas) do mais para o menos frequente.
        var genres = _all.Where(a => a.HasGenre)
            .GroupBy(a => a.Genre!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (Key: g.Key, Label: $"{g.Key}  ({g.Count()})")).ToList();
        if (_genreFilter is not null && !genres.Any(g => g.Key.Equals(_genreFilter, StringComparison.OrdinalIgnoreCase)))
            _genreFilter = null;
        FillOptions(GenreOptions, genres, _genreFilter, key => { _genreFilter = key.Length == 0 ? null : key; OnAdvancedFilterChanged(); });

        var decades = _all.Where(a => a.Decade.HasValue)
            .GroupBy(a => a.Decade!.Value).OrderBy(g => g.Key)
            .Select(g => (Key: g.Key.ToString(), Label: $"{Decades.Label(g.Key)}  ({g.Count()})")).ToList();
        if (_decadeFilter is not null && !decades.Any(d => d.Key == _decadeFilter.ToString()))
            _decadeFilter = null;
        FillOptions(DecadeOptions, decades, _decadeFilter?.ToString(),
            key => { _decadeFilter = key.Length == 0 ? null : int.Parse(key); OnAdvancedFilterChanged(); });

        var counts = new Dictionary<string, (string Name, int Count)>(StringComparer.OrdinalIgnoreCase);
        foreach (var album in _all)
            foreach (var tag in album.TagList)
                counts[tag] = counts.TryGetValue(tag, out var c) ? (c.Name, c.Count + 1) : (tag, 1);
        var tags = counts.Values
            .OrderBy(t => Array.FindIndex(TagUtil.Defaults, d => d.Equals(t.Name, StringComparison.OrdinalIgnoreCase)) is var i && i >= 0 ? i : 99)
            .ThenByDescending(t => t.Count).ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => (Key: t.Name, Label: $"{TagUtil.Display(t.Name)}  ({t.Count})")).ToList();
        if (_tagFilter is not null && !tags.Any(t => t.Key.Equals(_tagFilter, StringComparison.OrdinalIgnoreCase)))
            _tagFilter = null;
        FillOptions(TagOptions, tags, _tagFilter, key => { _tagFilter = key.Length == 0 ? null : key; OnAdvancedFilterChanged(); });

        LoanedCount = _all.Count(a => a.IsLoaned);
        HasLoaned = LoanedCount > 0;
        var loans = LoanedCount > 0
            ? new List<(string Key, string Label)> { ("out", Loc.F("Emprestados  ({0})", LoanedCount)) }
            : new List<(string Key, string Label)>();
        if (_loanFilter is not null && loans.Count == 0)
            _loanFilter = null;
        FillOptions(LoanOptions, loans, _loanFilter, key => { _loanFilter = key.Length == 0 ? null : key; OnAdvancedFilterChanged(); });

        HasGenreOptions = GenreOptions.Count > 0;
        HasDecadeOptions = DecadeOptions.Count > 0;
        HasTagOptions = TagOptions.Count > 0;
        HasLoanOptions = LoanOptions.Count > 0;
        HasNoFilterOptions = !(HasGenreOptions || HasDecadeOptions || HasTagOptions || HasLoanOptions);

        ActiveFilterCount = CountActiveFilters();
    }

    private static void FillOptions(ObservableCollection<ChoiceItem> target, List<(string Key, string Label)> items,
                                    string? selectedKey, Action<string> onChosen)
    {
        target.Clear();
        if (items.Count == 0)
            return;

        var all = new ChoiceItem("", Loc.T("Todos")) { IsSelected = selectedKey is null };
        all.Chosen += c => onChosen(c.Key);
        target.Add(all);

        foreach (var (key, label) in items)
        {
            var option = new ChoiceItem(key, label) { IsSelected = key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase) };
            option.Chosen += c => onChosen(c.Key);
            target.Add(option);
        }
    }

    private int CountActiveFilters() =>
        (_genreFilter is null ? 0 : 1) + (_decadeFilter is null ? 0 : 1) + (_tagFilter is null ? 0 : 1) + (_loanFilter is null ? 0 : 1);

    private void OnAdvancedFilterChanged()
    {
        ActiveFilterCount = CountActiveFilters();
        RefreshView();
    }

    // ---------- Ordenar e agrupar ----------

    private void BuildOrderingChoices()
    {
        foreach (var (key, label) in CollectionOrdering.SortOptions)
        {
            var choice = new ChoiceItem(key, label) { IsSelected = key == _sortKey };
            choice.Chosen += c => { _sortKey = c.Key; OrderingChanged(); };
            SortChoices.Add(choice);
        }

        foreach (var (key, label) in CollectionOrdering.GroupOptions)
        {
            var choice = new ChoiceItem(key, label) { IsSelected = key == _groupKey };
            choice.Chosen += c => { _groupKey = c.Key; OrderingChanged(); };
            GroupChoices.Add(choice);
        }
    }

    private void OrderingChanged()
    {
        var settings = SettingsStore.Current;
        settings.CollectionSort = _sortKey;
        settings.CollectionGroup = _groupKey;
        SettingsStore.Save();

        ApplyOrdering();
    }

    /// <summary>Reordena a lista completa e reconstrói o que está na tela (a ordem mudou, então não dá para atualizar só o que mudou).</summary>
    private void ApplyOrdering()
    {
        var selectedId = SelectedAlbum?.Id;
        _all = CollectionOrdering.Sort(_all, _sortKey, _groupKey);

        Albums.Clear();
        ApplyGroupingToView();
        UpdateOrderingState();

        RefreshView(whenDone: selectedId is { } id
            ? () => SelectedAlbum = Albums.FirstOrDefault(a => a.Id == id)
            : null);
    }

    /// <summary>Liga (ou desliga) as seções na lista mostrada. A galeria e a tabela usam a mesma visão.</summary>
    private void ApplyGroupingToView()
    {
        var view = CollectionViewSource.GetDefaultView(Albums);
        using (view.DeferRefresh())
        {
            view.SortDescriptions.Clear();   // tira a ordenação por clique no cabeçalho da tabela, se houver
            view.GroupDescriptions.Clear();
            if (_groupKey != CollectionOrdering.GroupNone)
                view.GroupDescriptions.Add(new AlbumGroupDescription(_groupKey));
        }
    }

    private void UpdateOrderingState()
    {
        IsGrouped = _groupKey != CollectionOrdering.GroupNone;

        var parts = new List<string>();
        if (_sortKey != CollectionOrdering.SortArtist)
            parts.Add(CollectionOrdering.SortOptions.First(o => o.Key == _sortKey).Label);
        if (IsGrouped)
            parts.Add(Loc.T("por ") + CollectionOrdering.GroupOptions.First(o => o.Key == _groupKey).Label.ToLowerInvariant());

        // O botão fica curto (a barra de filtros é estreita); o detalhe vai na dica.
        OrderButtonText = parts.Count == 0 ? Loc.T("⇅  Ordenar") : Loc.T("⇅  Ordenar  ●");
        OrderSummary = parts.Count == 0
            ? Loc.T("Ordenar a coleção e agrupar por gênero, década ou artista")
            : Loc.T("Ordem atual: ") + string.Join("  ·  ", parts);
    }

    /// <summary>Volta ao padrão: por artista, sem seções.</summary>
    [RelayCommand]
    private void ResetOrdering()
    {
        foreach (var choice in SortChoices)
            choice.IsSelected = choice.Key == CollectionOrdering.SortArtist;
        foreach (var choice in GroupChoices)
            choice.IsSelected = choice.Key == CollectionOrdering.GroupNone;
    }

    // ---------- Empréstimos ----------

    /// <summary>Empresta o álbum selecionado (pergunta para quem) ou registra a devolução.</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleLoan()
    {
        if (SelectedAlbum is not { } album)
            return;

        if (album.IsLoaned)
        {
            var since = album.LoanedOn.HasValue ? Loc.F(" (desde {0})", Fmt.Date(album.LoanedOn)) : "";
            if (!Dialogs.Confirm(Loc.T("Devolução"), Loc.F("{0} devolveu \"{1}\"{2}?", album.LoanedTo!.Trim(), album.Title, since)))
                return;

            album.LoanedTo = null;
            album.LoanedOn = null;
        }
        else
        {
            var settings = SettingsStore.Current;
            var suggestions = _all.Where(a => a.IsLoaned).Select(a => a.LoanedTo!.Trim())
                .Concat(settings.RecentBorrowers)
                .Distinct(StringComparer.CurrentCultureIgnoreCase).Take(8).ToList();

            if (Dialogs.PromptLoan(album.Title, suggestions) is not { } loan)
                return;

            album.LoanedTo = loan.Name;
            album.LoanedOn = loan.Date;

            settings.RecentBorrowers = new[] { loan.Name }.Concat(settings.RecentBorrowers)
                .Distinct(StringComparer.CurrentCultureIgnoreCase).Take(8).ToList();
            SettingsStore.Save();
        }

        _repository.Update(album);
        Reload(album.Id);
    }

    /// <summary>Liga/desliga a lista "Favoritos" do álbum selecionado (o coração).</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleFavorite()
    {
        if (SelectedAlbum is not { } album)
            return;

        var tags = album.TagList.ToList();
        var existing = tags.FindIndex(t => t.Equals(TagUtil.Favorites, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            tags.RemoveAt(existing);
        else
            tags.Add(TagUtil.Favorites);

        album.Tags = TagUtil.Join(tags);
        _repository.Update(album);
        Reload(album.Id);
    }

    [RelayCommand]
    private void SetFilter(string? key) => FilterKey = key ?? "all";

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        FilterKey = "all";
        ClearAdvancedFilters();
    }

    /// <summary>Volta gênero, década e lista para "Todos".</summary>
    [RelayCommand]
    private void ClearAdvancedFilters()
    {
        _genreFilter = null;
        _decadeFilter = null;
        _tagFilter = null;
        _loanFilter = null;
        foreach (var options in new[] { GenreOptions, DecadeOptions, TagOptions, LoanOptions })
            for (var i = options.Count - 1; i >= 0; i--)
                options[i].IsSelected = i == 0;   // só "Todos" fica marcado
        OnAdvancedFilterChanged();
    }

    [RelayCommand]
    private void Add()
    {
        var editor = new AlbumEditViewModel(_repository, null);
        if (Dialogs.ShowAlbumEditor(editor))
            Reload(editor.SavedId);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Edit()
    {
        if (SelectedAlbum is null)
            return;

        var editor = new AlbumEditViewModel(_repository, SelectedAlbum);
        if (Dialogs.ShowAlbumEditor(editor))
            Reload(editor.SavedId);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Delete()
    {
        if (SelectedAlbum is not { } album)
            return;

        if (!Dialogs.Confirm(Loc.T("Excluir álbum"), Loc.F("Excluir \"{0}\" ({1})?\nEssa ação não pode ser desfeita.", album.Title, album.Artist)))
            return;

        _repository.Delete(album.Id);
        CoverService.Delete(album.CoverFile);
        Reload();
    }

    /// <summary>Baixa da internet a capa de todos os álbuns que ainda não têm.</summary>
    [RelayCommand]
    private void DownloadCovers()
    {
        var missing = _all.Where(a => CoverService.Resolve(a.CoverFile) is null).ToList();
        if (missing.Count == 0)
        {
            Dialogs.Info(Loc.T("Baixar capas"), Loc.T("Todos os álbuns já têm capa."));
            return;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(missing.Count * 3 / 60.0));
        var question = Loc.F("Buscar na internet a capa de {0} álbuns que ainda não têm?\n\n", missing.Count) +
                       Loc.F("Leva em torno de {0} min (o serviço gratuito permite cerca de 1 consulta por segundo). ", minutes) +
                       Loc.T("Você pode cancelar quando quiser e o que já foi baixado fica salvo.");
        if (!Dialogs.Confirm(Loc.T("Baixar capas"), question))
            return;

        var viewModel = new CoverDownloadViewModel(_repository, missing);
        if (Dialogs.ShowCoverDownload(viewModel))
            Reload(SelectedAlbum?.Id);
    }

    /// <summary>Busca na internet a data de lançamento dos álbuns que estão sem ela.</summary>
    [RelayCommand]
    private void CompleteData()
    {
        var missing = _all.Where(a => a.ReleaseDate is null).ToList();
        if (missing.Count == 0)
        {
            Dialogs.Info(Loc.T("Completar dados"), Loc.T("Todos os álbuns já têm data de lançamento."));
            return;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(missing.Count * 2 / 60.0));
        var question = Loc.F("Buscar na internet a data de lançamento de {0} álbuns que estão sem ela?\n\n", missing.Count) +
                       Loc.F("Leva em torno de {0} min. Só preenche quando o resultado é confiável (mesmo artista); ", minutes) +
                       Loc.T("o que não achar fica em branco. Você pode cancelar quando quiser.");
        if (!Dialogs.Confirm(Loc.T("Completar dados"), question))
            return;

        var viewModel = new MetadataBatchViewModel(_repository, missing);
        if (Dialogs.ShowMetadataBatch(viewModel))
            Reload(SelectedAlbum?.Id);
    }

    /// <summary>Lista os álbuns que aparecem mais de uma vez (mesmo em mídias diferentes).</summary>
    [RelayCommand]
    private void FindDuplicates()
    {
        if (DuplicateService.Find(_all).Count == 0)
        {
            Dialogs.Info(Loc.T("Álbuns repetidos"), Loc.T("Não encontrei nenhum álbum repetido na coleção."));
            return;
        }

        var viewModel = new DuplicatesViewModel(_repository, _all);
        if (Dialogs.ShowDuplicates(viewModel))
            Reload(SelectedAlbum?.Id);
    }

    [RelayCommand]
    private void Export()
    {
        if (_all.Count == 0)
        {
            Dialogs.Info(Loc.T("Exportar coleção"), Loc.T("Não há álbuns para exportar."));
            return;
        }

        var path = Dialogs.PickExportFile();
        if (path is null)
            return;

        try
        {
            ExportService.Export(path, _all);
            Dialogs.Info(Loc.T("Exportar coleção"), Loc.F("{0} álbuns exportados para:\n{1}", _all.Count, path));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Não foi possível exportar"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var path = Dialogs.PickBackupSavePath();
        if (path is null)
            return;

        try
        {
            await Task.Run(() => BackupService.CreateBackup(Database.AppFolder, path));
            Dialogs.Info(Loc.T("Backup criado"), Loc.F("Backup salvo em:\n{0}\n\nGuarde esse arquivo num pendrive ou na nuvem.", path));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Não foi possível criar o backup"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var path = Dialogs.PickBackupFile();
        if (path is null)
            return;

        try
        {
            var (albums, covers) = await Task.Run(() => BackupService.Inspect(path));

            var question = Loc.F("Esse backup tem {0} álbuns e {1} capas.\n\n", albums, covers) +
                           Loc.T("Restaurar vai SUBSTITUIR a coleção atual por esse backup. ") +
                           Loc.T("Por segurança, o app guarda uma cópia da coleção de agora antes de trocar.\n\nContinuar?");
            if (!Dialogs.Confirm(Loc.T("Restaurar backup"), question))
                return;

            string safetyCopy = "";
            await Task.Run(() => BackupService.Restore(Database.AppFolder, path, out safetyCopy));
            Reload();

            Dialogs.Info(Loc.T("Backup restaurado"), Loc.F("Coleção restaurada: {0} álbuns.\n\nA coleção anterior ficou guardada em:\n{1}", albums, safetyCopy));
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Não foi possível restaurar"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var file = Dialogs.PickImportFile();
        if (file is null)
            return;

        try
        {
            var warnings = new List<string>();
            var isCsv = string.Equals(Path.GetExtension(file), ".csv", StringComparison.OrdinalIgnoreCase);
            var csv = new CsvImporter();
            var parsed = await Task.Run(() => isCsv ? csv.Read(file, warnings) : new ExcelImporter().Read(file, warnings));

            // Não duplica o que já está no banco (permite importar a mesma planilha duas vezes).
            var existing = _repository.GetAll()
                .Select(Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var toAdd = parsed.Where(a => !existing.Contains(Key(a))).ToList();
            _repository.AddRange(toAdd);
            Reload();

            var message = isCsv && csv.LastKind == CsvImporter.CsvKind.Discogs
                ? Loc.F("Arquivo do Discogs reconhecido.\n{0} itens importados.", toAdd.Count)
                : Loc.F("{0} itens importados.", toAdd.Count);
            if (parsed.Count - toAdd.Count > 0)
                message += Loc.F("\n{0} já existiam e foram ignorados.", parsed.Count - toAdd.Count);
            if (warnings.Count > 0)
                message += "\n\nAvisos:\n" + string.Join("\n", warnings.Take(10));

            Dialogs.Info(Loc.T("Importação concluída"), message);
        }
        catch (Exception ex)
        {
            Dialogs.Error(Loc.T("Não foi possível importar"), ex.Message);
        }
    }

    private static string Key(Album a) => $"{a.Title.Trim()}|{a.Artist.Trim()}|{(int)a.Format}";
}
