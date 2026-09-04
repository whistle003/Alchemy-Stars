using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Alchemist.UI;

public static class LocalizationManager
{
    private const string ChineseCulture = "zh-CN";
    private const string EnglishCulture = "en-US";
    private const string DictionaryMarker = "LocalizationDictionaryMarker";
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Alchemy Stars",
        "settings.json");

    public static event EventHandler? LanguageChanged;

    public static string CurrentCulture { get; private set; } = ChineseCulture;

    public static void Initialize()
    {
        var culture = ReadSavedCulture();
        if (culture is null)
            culture = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? ChineseCulture
                : EnglishCulture;

        SetCulture(culture, persist: false);
    }

    public static void Toggle() =>
        SetCulture(CurrentCulture == ChineseCulture ? EnglishCulture : ChineseCulture);

    public static void SetCulture(string culture, bool persist = true)
    {
        culture = string.Equals(culture, EnglishCulture, StringComparison.OrdinalIgnoreCase)
            ? EnglishCulture
            : ChineseCulture;

        var app = Application.Current;
        if (app is not null)
        {
            var dictionaries = app.Resources.MergedDictionaries;
            var existing = dictionaries.FirstOrDefault(dictionary => dictionary.Contains(DictionaryMarker));
            var replacement = new ResourceDictionary
            {
                Source = new Uri($"Resources/Strings.{culture}.xaml", UriKind.Relative),
            };

            if (existing is null)
                dictionaries.Add(replacement);
            else
                dictionaries[dictionaries.IndexOf(existing)] = replacement;
        }

        CurrentCulture = culture;
        var cultureInfo = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

        if (persist)
            SaveCulture(culture);

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key)
    {
        if (Application.Current?.TryFindResource(key) is string value)
            return value;

        return key;
    }

    public static string Format(string key, params object?[] values) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), values);

    private static string? ReadSavedCulture()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return null;

            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsPath));
            return settings?.Language;
        }
        catch (Exception ex)
        {
            Logging.Logger.Error("Failed to read language preference.", ex);
            return null;
        }
    }

    private static void SaveCulture(string culture)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new UserSettings(culture)));
        }
        catch (Exception ex)
        {
            Logging.Logger.Error("Failed to save language preference.", ex);
        }
    }

    private sealed record UserSettings(string Language);
}
