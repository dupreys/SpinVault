using System.Collections.ObjectModel;
using ColecaoMidias.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ColecaoMidias.ViewModels;

public partial class DuplicatesViewModel : ObservableObject
{
    private readonly AlbumRepository _repository;
    private readonly List<Album> _albums;

    public ObservableCollection<DuplicateGroup> Groups { get; } = new();

    [ObservableProperty] private string summaryText = "";
    [ObservableProperty] private bool isEmpty;

    /// <summary>Algum álbum foi excluído (a coleção precisa ser recarregada).</summary>
    public bool Changed { get; private set; }

    public DuplicatesViewModel(AlbumRepository repository, IEnumerable<Album> albums)
    {
        _repository = repository;
        _albums = albums.ToList();
        Refresh();
    }

    private void Refresh()
    {
        Groups.Clear();
        foreach (var group in DuplicateService.Find(_albums))
            Groups.Add(group);

        IsEmpty = Groups.Count == 0;
        var same = Groups.Count(g => g.SameFormat);
        SummaryText = Groups.Count == 0
            ? "Nenhum álbum repetido."
            : $"{Groups.Count} álbuns repetidos ({same} na mesma mídia, {Groups.Count - same} em mídias diferentes).";
    }

    [RelayCommand]
    private void DeleteItem(Album? album)
    {
        if (album is null)
            return;

        if (!Dialogs.Confirm("Excluir álbum",
                $"Excluir \"{album.Title}\" ({album.Artist}) em {MediaFormatInfo.Label(album.Format)}?\nEssa ação não pode ser desfeita."))
            return;

        _repository.Delete(album.Id);
        CoverService.Delete(album.CoverFile);
        _albums.Remove(album);
        Changed = true;
        Refresh();
    }
}
