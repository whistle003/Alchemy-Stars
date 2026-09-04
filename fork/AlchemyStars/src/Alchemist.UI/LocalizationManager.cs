using System.Globalization;
using System.Windows;

namespace Alchemist.UI;

public static class LocalizationManager
{
    public const string SystemLanguage = "system";
    public const string ChineseCulture = "zh-CN";
    public const string EnglishCulture = "en-US";
    private const string DictionaryMarker = "LocalizationDictionaryMarker";
    private static string detectedSystemCulture = ResolveSystemCulture(CultureInfo.CurrentUICulture);

    public static event EventHandler? LanguageChanged;

    public static string CurrentCulture { get; private set; } = ChineseCulture;
    public static string LanguagePreference { get; private set; } = SystemLanguage;
    public static string DefaultOutputFormat => OutputFormatCatalog.Normalize(AppPreferences.DefaultOutputFormat);

    public static void Initialize()
    {
        detectedSystemCulture = ResolveSystemCulture(CultureInfo.CurrentUICulture);
        SetLanguagePreference(AppPreferences.Language, persist: false);
    }

    public static void Toggle() => SetLanguagePreference(
        CurrentCulture == ChineseCulture ? EnglishCulture : ChineseCulture);

    public static void SetLanguagePreference(string? preference, bool persist = true)
    {
        LanguagePreference = NormalizeLanguagePreference(preference);
        ApplyCulture(LanguagePreference == SystemLanguage ? detectedSystemCulture : LanguagePreference);
        if (persist)
            AppPreferences.Language = LanguagePreference;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void SetCulture(string culture, bool persist = true) =>
        SetLanguagePreference(culture, persist);

    public static void SetDefaultOutputFormat(string? format) =>
        AppPreferences.DefaultOutputFormat = OutputFormatCatalog.Normalize(format);

    public static string ResolveCulture(string? preference, CultureInfo systemCulture)
    {
        var normalized = NormalizeLanguagePreference(preference);
        return normalized == SystemLanguage ? ResolveSystemCulture(systemCulture) : normalized;
    }

    public static string Get(string key)
    {
        if (Application.Current?.TryFindResource(key) is string value)
            return value;
        return key;
    }

    public static string Format(string key, params object?[] values) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), values);

    private static string NormalizeLanguagePreference(string? preference)
    {
        if (string.Equals(preference, ChineseCulture, StringComparison.OrdinalIgnoreCase))
            return ChineseCulture;
        if (string.Equals(preference, EnglishCulture, StringComparison.OrdinalIgnoreCase))
            return EnglishCulture;
        return SystemLanguage;
    }

    private static string ResolveSystemCulture(CultureInfo culture) =>
        culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? ChineseCulture
            : EnglishCulture;

    private static void ApplyCulture(string culture)
    {
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
    }
}
