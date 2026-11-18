using System.Windows;
using System.Windows.Controls;
using ColecaoMidias.Models;
using ColecaoMidias.Services;

namespace ColecaoMidias.Views;

public partial class LoanWindow : Window
{
    public string? BorrowerName { get; private set; }
    public DateTime LoanDate { get; private set; } = DateTime.Today;

    public LoanWindow(string albumTitle, IReadOnlyList<string> suggestions)
    {
        InitializeComponent();
        AlbumText.Text = albumTitle;
        DateBox.Text = Fmt.Date(DateTime.Today);
        Suggestions.ItemsSource = suggestions;
        SuggestionsPanel.Visibility = suggestions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SourceInitialized += (_, _) => WindowTheme.Apply(this, ThemeService.Current == AppTheme.Dark);
        Loaded += (_, _) => NameBox.Focus();
    }

    private void OnSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string name })
        {
            NameBox.Text = name;
            NameBox.CaretIndex = name.Length;
            NameBox.Focus();
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            Error.Text = Loc.T("Informe para quem você emprestou.");
            NameBox.Focus();
            return;
        }

        var date = DateTime.Today;
        if (DateBox.Text.Trim().Length > 0)
        {
            if (!Fmt.TryParseDate(DateBox.Text, out date) || date.Date > DateTime.Today)
            {
                Error.Text = Loc.T("Data inválida. Use o formato dia/mês/ano (por exemplo, 15/11/2026); não pode ser no futuro.");
                DateBox.Focus();
                return;
            }
        }

        BorrowerName = name;
        LoanDate = date.Date;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
