using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace AlchemyStars.Avalonia;

/// <summary>Runtime appearance tokens shared by windows, popups and previews.</summary>
internal static class AppearanceTheme
{
    private static Application? owner;
    private static string currentStyle = "apple";

    public static void Apply(string style, string mode)
    {
        var app = Application.Current;
        if (app is null) return; // Headless engine self-tests have no UI application.
        if (!ReferenceEquals(owner, app))
        {
            if (owner is not null) owner.ActualThemeVariantChanged -= ThemeChanged;
            owner = app;
            app.ActualThemeVariantChanged += ThemeChanged;
        }
        currentStyle = style;
        app.RequestedThemeVariant = mode switch
        {
            "dark" => ThemeVariant.Dark,
            "system" => ThemeVariant.Default,
            _ => ThemeVariant.Light,
        };
        UpdateTokens(app);
    }

    private static void ThemeChanged(object? sender, EventArgs e)
    {
        if (owner is not null) UpdateTokens(owner);
    }

    private static void UpdateTokens(Application app)
    {
        var dark = app.ActualThemeVariant == ThemeVariant.Dark;
        var relief = currentStyle == "neumorphic";
        var r = app.Resources;
        void Brush(string name, string color)
        {
            var value = Color.Parse(color);
            r["Alchemy" + name] = value;
            r["Alchemy" + name + "Brush"] = new SolidColorBrush(value);
        }

        var background = relief ? (dark ? "#252b35" : "#e0e5ec") : (dark ? "#1d1d1f" : "#f5f5f7");
        var surface = relief ? background : (dark ? "#272729" : "#ffffff");
        var raised = relief ? background : (dark ? "#2a2a2c" : "#fafafc");
        var ink = dark ? "#f5f5f7" : (relief ? "#3a4a5a" : "#1d1d1f");
        var muted = dark ? "#b8bbc2" : (relief ? "#5a6575" : "#6e6e73");
        var accent = dark ? "#2997ff" : "#0066cc";
        Brush("Background", background);
        Brush("Sidebar", background);
        Brush("Surface", surface);
        Brush("SubtleSurface", raised);
        Brush("SurfaceRaised", raised);
        Brush("Canvas", relief ? (dark ? "#222832" : "#d6dee8") : (dark ? "#252527" : "#f5f5f7"));
        Brush("CommandBar", relief ? background : "#000000");
        Brush("CommandText", relief ? ink : "#ffffff");
        Brush("OnDarkMuted", relief ? muted : "#cccccc");
        Brush("DarkTile", dark ? "#353840" : (relief ? "#e9edf3" : "#272729"));
        Brush("Text", ink);
        Brush("MutedText", muted);
        Brush("Border", dark ? "#383c43" : (relief ? "#d0d7e1" : "#f0f0f0"));
        Brush("BorderStrong", dark ? "#555b66" : (relief ? "#bcc7d4" : "#e0e0e0"));
        Brush("Accent", accent);
        Brush("Action", "#0066cc"); // White primary labels retain contrast in both modes.
        Brush("Focus", dark ? "#64b3ff" : "#0071e3");
        Brush("AccentHover", "#0071e3");
        Brush("AccentBorder", accent);
        Brush("Hover", dark ? "#353d49" : "#e9edf3");
        Brush("Pressed", dark ? "#1b2028" : "#d3dde9");
        Brush("Selected", dark ? "#253e59" : "#e4effb");
        Brush("Hero", raised);
        Brush("Success", muted);
        Brush("Error", dark ? "#ff8896" : "#b83d50");
        Brush("Chip", dark ? "#414853" : "#d2d2d7");
        Brush("BaseClip", dark ? "#253e59" : "#e4effb");
        Brush("BaseEdge", accent);
        Brush("LayerClip", raised);
        Brush("LayerEdge", muted);
        r["SystemAccentColor"] = Color.Parse("#0066cc");
        r["SystemAccentColorDark1"] = Color.Parse("#0066cc");
        r["SystemAccentColorLight1"] = Color.Parse("#0071e3");

        var shade = dark ? "#171c23" : "#c2c8d2";
        var light = dark ? "#353e4b" : "#fefeff";
        r["AppearanceRaisedShadow"] = BoxShadows.Parse(relief ? $"3 3 6 0 {shade}, -3 -3 6 0 {light}" : "none");
        r["AppearanceInsetShadow"] = BoxShadows.Parse(relief ? $"inset 2 2 5 0 {shade}, inset -2 -2 5 0 {light}" : "none");
        r["AppearancePanelShadow"] = BoxShadows.Parse(relief ? $"5 5 10 0 {shade}, -5 -5 10 0 {light}" : "none");
        r["AppearancePanelRadius"] = new CornerRadius(relief ? 20 : 0);
        r["AppearanceHeaderRadius"] = relief ? new CornerRadius(20, 20, 0, 0) : new CornerRadius(0);
        r["AppearanceButtonRadius"] = new CornerRadius(relief ? 12 : 8);
        r["AppearanceActionRadius"] = new CornerRadius(relief ? 12 : 9999);
        r["AppearanceCardRadius"] = new CornerRadius(relief ? 20 : 18);
        r["AppearanceCardBorder"] = new Thickness(relief ? 0 : 1);
    }
}
