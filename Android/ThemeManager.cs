namespace SpinVaultMobile;

/// <summary>Tema escuro ou claro do app (mesmas paletas do SpinVault no Windows). O escolhido fica salvo.</summary>
public static class ThemeManager
{
    private const string Key = "app_theme";

    public static bool IsDark => Preferences.Get(Key, "dark") != "light";

    public static void Set(bool dark) => Preferences.Set(Key, dark ? "dark" : "light");

    /// <summary>Aplica as cores do tema escolhido. Telas já abertas precisam ser recriadas (App.Reload).</summary>
    public static void Apply()
    {
        var app = Application.Current;
        if (app is null)
            return;

        var dark = IsDark;
        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;

        var palette = dark
            ? new Dictionary<string, string>
            {
                ["Bg"] = "#0E1016", ["Surface"] = "#161922", ["SurfaceAlt"] = "#1E2230", ["Line"] = "#2A3042",
                ["TextPrimary"] = "#F3F5F9", ["TextSecondary"] = "#9EA6BB", ["TextMuted"] = "#6C7490",
                ["Accent"] = "#F59E0B", ["AccentFg"] = "#1B1203", ["Danger"] = "#F87171",
            }
            : new Dictionary<string, string>
            {
                ["Bg"] = "#F5F2EA", ["Surface"] = "#FFFFFF", ["SurfaceAlt"] = "#F1ECE0", ["Line"] = "#E2DACA",
                ["TextPrimary"] = "#1B1F2A", ["TextSecondary"] = "#5F677B", ["TextMuted"] = "#98A0B2",
                ["Accent"] = "#D97706", ["AccentFg"] = "#FFFFFF", ["Danger"] = "#DC2626",
            };

        foreach (var (name, hex) in palette)
            app.Resources[name] = Color.FromArgb(hex);
        app.Resources["LineBrush"] = new SolidColorBrush(Color.FromArgb(palette["Line"]));
    }
}
