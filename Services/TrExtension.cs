using System.Windows.Markup;

namespace ColecaoMidias.Services;

/// <summary>Traduz um texto fixo do XAML: <c>Text="{l:Tr 'Salvar'}"</c> (o texto original é em português).</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string text) => Text = text;

    [ConstructorArgument("text")]
    public string Text { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Text);
}
