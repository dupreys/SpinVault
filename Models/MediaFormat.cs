namespace ColecaoMidias.Models;

public enum MediaFormat
{
    Vinyl = 0,
    CD = 1,
    Cassette = 2
}

public static class MediaFormatInfo
{
    public static string Label(MediaFormat format) => format switch
    {
        MediaFormat.Vinyl => "Vinil",
        MediaFormat.CD => "CD",
        MediaFormat.Cassette => "Cassete",
        _ => format.ToString()
    };

    public static string Glyph(MediaFormat format) => format switch
    {
        MediaFormat.Vinyl => "♫",
        MediaFormat.CD => "◉",
        MediaFormat.Cassette => "▤",
        _ => "♪"
    };

    /// <summary>Chave do pincel (definido nos temas) usado para colorir cada formato.</summary>
    public static string BrushKey(MediaFormat format) => format switch
    {
        MediaFormat.Vinyl => "VinylBrush",
        MediaFormat.CD => "CdBrush",
        MediaFormat.Cassette => "CassetteBrush",
        _ => "TextPrimaryBrush"
    };

    /// <summary>
    /// Reconhece textos como "🎵 Vinil", "💿 CD", "📼 Cassete" (como estão na planilha).
    /// </summary>
    public static bool TryParse(string? text, out MediaFormat format)
    {
        var t = (text ?? "").Trim().ToLowerInvariant();

        if (t.Contains("vinil") || t.Contains("vinyl") || t == "lp")
        {
            format = MediaFormat.Vinyl;
            return true;
        }

        if (t.Contains("cassete") || t.Contains("cassette") || t.Contains("fita") || t.Contains("k7"))
        {
            format = MediaFormat.Cassette;
            return true;
        }

        if (t.Contains("cd"))
        {
            format = MediaFormat.CD;
            return true;
        }

        format = default;
        return false;
    }
}
