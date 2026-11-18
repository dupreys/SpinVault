using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using ColecaoMidias.Models;
using ColecaoMidias.Services;

namespace ColecaoMidias.Converters;

/// <summary>Agrupa os álbuns em seções (gênero, década ou artista), sem diferenciar maiúsculas de minúsculas.</summary>
public sealed class AlbumGroupDescription : GroupDescription
{
    private readonly string _groupKey;

    public AlbumGroupDescription(string groupKey) => _groupKey = groupKey;

    public override object GroupNameFromItem(object item, int level, CultureInfo culture) =>
        item is Album album ? CollectionOrdering.GroupLabel(album, _groupKey) ?? "" : "";

    public override bool NamesMatch(object groupName, object itemName) =>
        string.Equals(groupName as string, itemName as string, StringComparison.CurrentCultureIgnoreCase);
}
