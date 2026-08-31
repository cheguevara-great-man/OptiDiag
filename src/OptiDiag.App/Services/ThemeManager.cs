using System.Windows.Media;
using OptiDiag.Infrastructure;

namespace OptiDiag.App.Services;

public static class ThemeManager
{
    private sealed record Palette(
        string Window,
        string Header,
        string Panel,
        string PanelAlt,
        string Input,
        string Border,
        string Text,
        string Muted,
        string Accent,
        string AccentDark,
        string Warning,
        string Hover,
        string Pressed,
        string Row,
        string AlternateRow,
        string Grid,
        string HeaderCell);

    private static readonly IReadOnlyDictionary<string, Palette> Palettes =
        new Dictionary<string, Palette>(StringComparer.Ordinal)
        {
            ["Dark"] = new(
                "#0B1220", "#101A2B", "#121B2C", "#18243A", "#0E1727", "#293A56",
                "#E8EEF8", "#91A1B8", "#32B8C6", "#187F8A", "#FFB85C", "#223553",
                "#17263D", "#111A2B", "#151F32", "#22314A", "#1C2A42"),
            ["Light"] = new(
                "#F5F7FB", "#EAF0F7", "#FFFFFF", "#E8EEF7", "#FFFFFF", "#C6D1E0",
                "#172033", "#53657A", "#007F91", "#086777", "#A04B00", "#DCE8F3",
                "#CDDCEA", "#FFFFFF", "#F3F6FA", "#D6DFEA", "#E3EAF3"),
            ["HighContrast"] = new(
                "#000000", "#090909", "#000000", "#151515", "#050505", "#8A8A8A",
                "#FFFFFF", "#D0D0D0", "#00E5FF", "#006B78", "#FFD54F", "#252525",
                "#101010", "#000000", "#101010", "#6C6C6C", "#202020")
        };

    public static void Apply(UserPreferences preferences)
    {
        var normalized = preferences.Normalize();
        var palette = Palettes[normalized.Theme];
        Set("WindowBrush", palette.Window);
        Set("HeaderBrush", palette.Header);
        Set("PanelBrush", palette.Panel);
        Set("PanelAltBrush", palette.PanelAlt);
        Set("InputBrush", palette.Input);
        Set("BorderBrush", palette.Border);
        Set("TextBrush", palette.Text);
        Set("MutedTextBrush", palette.Muted);
        Set("AccentBrush", palette.Accent);
        Set("AccentDarkBrush", palette.AccentDark);
        Set("WarningBrush", palette.Warning);
        Set("OnAccentTextBrush", "#FFFFFF");
        Set("HoverBrush", palette.Hover);
        Set("PressedBrush", palette.Pressed);
        Set("RowBrush", palette.Row);
        Set("AlternateRowBrush", palette.AlternateRow);
        Set("GridBrush", palette.Grid);
        Set("HeaderCellBrush", palette.HeaderCell);
        System.Windows.Application.Current.Resources["BaseFontSize"] = (double)normalized.FontSize;
    }

    private static void Set(string key, string color)
    {
        var converted = (Color)ColorConverter.ConvertFromString(color);
        System.Windows.Application.Current.Resources[key] = new SolidColorBrush(converted);
    }
}
