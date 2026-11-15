using System.Windows;
using ColecaoMidias.Data;

namespace ColecaoMidias.Services;

public enum AppTheme
{
    Dark,
    Light
}

/// <summary>Troca entre Themes/DarkTheme.xaml e Themes/LightTheme.xaml e lembra a escolha.</summary>
public static class ThemeService
{
    private static string SettingsFile => Path.Combine(Database.AppFolder, "theme.txt");

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static void Load()
    {
        var theme = AppTheme.Dark;
        try
        {
            if (File.Exists(SettingsFile) &&
                Enum.TryParse<AppTheme>(File.ReadAllText(SettingsFile).Trim(), out var saved))
                theme = saved;
        }
        catch (IOException) { /* usa o padrão */ }

        Apply(theme);
    }

    public static void Toggle() =>
        Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);

    public static void Apply(AppTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;

        for (var i = dictionaries.Count - 1; i >= 0; i--)
        {
            var source = dictionaries[i].Source?.OriginalString;
            if (source != null && source.EndsWith("Theme.xaml", StringComparison.OrdinalIgnoreCase))
                dictionaries.RemoveAt(i);
        }

        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{theme}Theme.xaml", UriKind.Absolute)
        });

        Current = theme;
        WindowTheme.ApplyToAll(theme == AppTheme.Dark);

        try
        {
            Directory.CreateDirectory(Database.AppFolder);
            File.WriteAllText(SettingsFile, theme.ToString());
        }
        catch (IOException) { /* não é crítico */ }
    }
}
