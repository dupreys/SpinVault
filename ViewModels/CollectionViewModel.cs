using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
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
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleFavoriteCommand))]
    private Album? selectedAlbum;

    // ----- Filtros por gênero, década e lista (v1.1) -----
    private string? _genreFilter;
    private int? _decadeFilter;
    private string? _tagFilter;

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
    public string FilterButtonText => ActiveFilterCount > 0 ? $"⚟  Filtros ({ActiveFilterCount})" : "⚟  Filtros";
    public string FavoriteLabel => SelectedAlbum?.IsFavorite == true ? "♥  Favorito" : "♡  Favoritar";

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

        Reload();
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

    /// <summary>
    /// Atualiza a lista mostrada mexendo só no que mudou. Os dois lados estão na mesma ordem,
    /// então basta percorrer a lista completa uma vez.
    /// </summary>
    private void RefreshView()
    {
        var i = 0;
        foreach (var album in _all)
        {
            var wanted = Matches(album);
            var isShown = i < Albums.Count && ReferenceEquals(Albums[i], album);

            if (isShown)
            {
                if (wanted)
                    i++;
                else
                    Albums.RemoveAt(i);
            }
            else if (wanted)
            {
                Albums.Insert(i, album);
                i++;
            }
        }

        UpdateSummary();
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
        var total = 0m;
        foreach (var album in Albums)
            total += album.Price;

        SummaryText = $"{Albums.Count} de {_all.Count} itens  ·  {Fmt.Money(total)}";
        IsLibraryEmpty = _all.Count == 0;
        NoResults = _all.Count > 0 && Albums.Count == 0;
    }

    /// <summary>Recarrega do banco e, opcionalmente, seleciona um álbum.</summary>
    public void Reload(int? selectId = null)
    {
        _all = _repository.GetAll()
            .OrderBy(a => a.Artist, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.ReleaseDate)
            .ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        CoverImageLoader.Preload(_all.Select(a => a.CoverFile));

        VinylCount = _all.Count(a => a.Format == MediaFormat.Vinyl);
        CdCount = _all.Count(a => a.Format == MediaFormat.CD);
        CassetteCount = _all.Count(a => a.Format == MediaFormat.Cassette);

        _searchKeys.Clear();
        foreach (var album in _all)
            _searchKeys[album.Id] = Normalize($"{album.Title} {album.Artist}");

        BuildFilterOptions();   // zera filtros que não existem mais (ex.: gênero que ninguém usa)

        Albums.Clear();
        foreach (var album in _all)
            if (Matches(album))
                Albums.Add(album);

        UpdateSummary();
        SelectedAlbum = selectId is { } id ? Albums.FirstOrDefault(a => a.Id == id) : null;

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
            .Select(t => (Key: t.Name, Label: $"{t.Name}  ({t.Count})")).ToList();
        if (_tagFilter is not null && !tags.Any(t => t.Key.Equals(_tagFilter, StringComparison.OrdinalIgnoreCase)))
            _tagFilter = null;
        FillOptions(TagOptions, tags, _tagFilter, key => { _tagFilter = key.Length == 0 ? null : key; OnAdvancedFilterChanged(); });

        HasGenreOptions = GenreOptions.Count > 0;
        HasDecadeOptions = DecadeOptions.Count > 0;
        HasTagOptions = TagOptions.Count > 0;
        HasNoFilterOptions = !(HasGenreOptions || HasDecadeOptions || HasTagOptions);

        ActiveFilterCount = (_genreFilter is null ? 0 : 1) + (_decadeFilter is null ? 0 : 1) + (_tagFilter is null ? 0 : 1);
    }

    private static void FillOptions(ObservableCollection<ChoiceItem> target, List<(string Key, string Label)> items,
                                    string? selectedKey, Action<string> onChosen)
    {
        target.Clear();
        if (items.Count == 0)
            return;

        var all = new ChoiceItem("", "Todos") { IsSelected = selectedKey is null };
        all.Chosen += c => onChosen(c.Key);
        target.Add(all);

        foreach (var (key, label) in items)
        {
            var option = new ChoiceItem(key, label) { IsSelected = key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase) };
            option.Chosen += c => onChosen(c.Key);
            target.Add(option);
        }
    }

    private void OnAdvancedFilterChanged()
    {
        ActiveFilterCount = (_genreFilter is null ? 0 : 1) + (_decadeFilter is null ? 0 : 1) + (_tagFilter is null ? 0 : 1);
        RefreshView();
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
        foreach (var options in new[] { GenreOptions, DecadeOptions, TagOptions })
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

        if (!Dialogs.Confirm("Excluir álbum", $"Excluir \"{album.Title}\" ({album.Artist})?\nEssa ação não pode ser desfeita."))
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
            Dialogs.Info("Baixar capas", "Todos os álbuns já têm capa.");
            return;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(missing.Count * 3 / 60.0));
        var question = $"Buscar na internet a capa de {missing.Count} álbuns que ainda não têm?\n\n" +
                       $"Leva em torno de {minutes} min (o serviço gratuito permite cerca de 1 consulta por segundo). " +
                       "Você pode cancelar quando quiser e o que já foi baixado fica salvo.";
        if (!Dialogs.Confirm("Baixar capas", question))
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
            Dialogs.Info("Completar dados", "Todos os álbuns já têm data de lançamento.");
            return;
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(missing.Count * 2 / 60.0));
        var question = $"Buscar na internet a data de lançamento de {missing.Count} álbuns que estão sem ela?\n\n" +
                       $"Leva em torno de {minutes} min. Só preenche quando o resultado é confiável (mesmo artista); " +
                       "o que não achar fica em branco. Você pode cancelar quando quiser.";
        if (!Dialogs.Confirm("Completar dados", question))
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
            Dialogs.Info("Álbuns repetidos", "Não encontrei nenhum álbum repetido na coleção.");
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
            Dialogs.Info("Exportar coleção", "Não há álbuns para exportar.");
            return;
        }

        var path = Dialogs.PickExportFile();
        if (path is null)
            return;

        try
        {
            ExportService.Export(path, _all);
            Dialogs.Info("Exportar coleção", $"{_all.Count} álbuns exportados para:\n{path}");
        }
        catch (Exception ex)
        {
            Dialogs.Error("Não foi possível exportar", ex.Message);
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
            Dialogs.Info("Backup criado", $"Backup salvo em:\n{path}\n\nGuarde esse arquivo num pendrive ou na nuvem.");
        }
        catch (Exception ex)
        {
            Dialogs.Error("Não foi possível criar o backup", ex.Message);
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

            var question = $"Esse backup tem {albums} álbuns e {covers} capas.\n\n" +
                           "Restaurar vai SUBSTITUIR a coleção atual por esse backup. " +
                           "Por segurança, o app guarda uma cópia da coleção de agora antes de trocar.\n\nContinuar?";
            if (!Dialogs.Confirm("Restaurar backup", question))
                return;

            string safetyCopy = "";
            await Task.Run(() => BackupService.Restore(Database.AppFolder, path, out safetyCopy));
            Reload();

            Dialogs.Info("Backup restaurado", $"Coleção restaurada: {albums} álbuns.\n\nA coleção anterior ficou guardada em:\n{safetyCopy}");
        }
        catch (Exception ex)
        {
            Dialogs.Error("Não foi possível restaurar", ex.Message);
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
                ? $"Arquivo do Discogs reconhecido.\n{toAdd.Count} itens importados."
                : $"{toAdd.Count} itens importados.";
            if (parsed.Count - toAdd.Count > 0)
                message += $"\n{parsed.Count - toAdd.Count} já existiam e foram ignorados.";
            if (warnings.Count > 0)
                message += "\n\nAvisos:\n" + string.Join("\n", warnings.Take(10));

            Dialogs.Info("Importação concluída", message);
        }
        catch (Exception ex)
        {
            Dialogs.Error("Não foi possível importar", ex.Message);
        }
    }

    private static string Key(Album a) => $"{a.Title.Trim()}|{a.Artist.Trim()}|{(int)a.Format}";
}
