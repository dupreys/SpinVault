using ColecaoMidias.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ColecaoMidias.ViewModels;

/// <summary>Uma opção de escolha única (chip) — usada em filtros e na conservação do editor.</summary>
public sealed partial class ChoiceItem : ObservableObject
{
    public ChoiceItem(string key, string label, string? toolTip = null)
    {
        Key = key;
        Label = label;
        ToolTip = toolTip;
    }

    public string Key { get; }
    public string Label { get; }
    public string? ToolTip { get; }

    [ObservableProperty] private bool isSelected;

    /// <summary>Disparado quando o usuário marca esta opção.</summary>
    public event Action<ChoiceItem>? Chosen;

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
            Chosen?.Invoke(this);
    }
}

/// <summary>Uma lista/etiqueta que pode ser marcada ou não num álbum.</summary>
public sealed partial class TagChoice : ObservableObject
{
    public TagChoice(string name, bool selected)
    {
        Name = name;
        isSelected = selected;
    }

    public string Name { get; }

    /// <summary>Nome mostrado na tela (as listas padrão aparecem no idioma atual).</summary>
    public string Display => TagUtil.Display(Name);

    [ObservableProperty] private bool isSelected;
}

/// <summary>Uma das cinco estrelas da avaliação.</summary>
public sealed partial class StarItem : ObservableObject
{
    public StarItem(int number) => Number = number;

    public int Number { get; }

    [ObservableProperty] private bool filled;

    public string Glyph => Filled ? "★" : "☆";

    partial void OnFilledChanged(bool value) => OnPropertyChanged(nameof(Glyph));
}
