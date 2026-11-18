using ColecaoMidias.Services;

namespace SpinVaultMobile;

/// <summary>Traduz um texto fixo do XAML: <c>Text="{l:Tr 'Salvar'}"</c> (o texto original é em português).</summary>
[ContentProperty(nameof(Text))]
public sealed class TrExtension : IMarkupExtension<string>
{
    public string Text { get; set; } = "";

    public string ProvideValue(IServiceProvider serviceProvider) => Loc.T(Text);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
