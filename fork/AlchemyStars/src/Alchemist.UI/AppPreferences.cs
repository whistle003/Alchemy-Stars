using System.Text.Json;
using System.IO;

namespace Alchemist.UI;

internal static class AppPreferences
{
    private static readonly object Sync = new();
    private static string SettingsPath
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("ALCHEMY_STARS_SETTINGS_PATH");
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Alchemy Stars",
                    "settings.json")
                : Path.GetFullPath(overridePath);
        }
    }
    private static PreferenceData? data;

    public static string Language
    {
        get => Read().Language;
        set => Update(settings => settings.Language = value);
    }

    public static string DefaultOutputFormat
    {
        get => Read().DefaultOutputFormat;
        set => Update(settings => settings.DefaultOutputFormat = value);
    }

    public static bool DefaultCastAnimationOnly
    {
        get => Read().DefaultCastAnimationOnly;
        set => Update(settings => settings.DefaultCastAnimationOnly = value);
    }

    public static bool DefaultBakeRelevantBonesOnly
    {
        get => Read().DefaultBakeRelevantBonesOnly;
        set => Update(settings => settings.DefaultBakeRelevantBonesOnly = value);
    }

    public static string? GetLastDirectory(string scope)
    {
        var settings = Read();
        return settings.LastDirectories.TryGetValue(scope, out var directory) && Directory.Exists(directory)
            ? directory
            : null;
    }

    public static void SetLastDirectory(string scope, string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
            return;
        var directory = Directory.Exists(selectedPath)
            ? selectedPath
            : Path.GetDirectoryName(selectedPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        Update(settings => settings.LastDirectories[scope] = Path.GetFullPath(directory));
    }

    private static PreferenceData Read()
    {
        lock (Sync)
        {
            if (data is not null)
                return data;

            try
            {
                data = File.Exists(SettingsPath)
                    ? JsonSerializer.Deserialize<PreferenceData>(File.ReadAllText(SettingsPath))
                    : null;
            }
            catch (Exception ex)
            {
                Logging.Logger.Error("Failed to read application preferences.", ex);
            }

            data ??= new PreferenceData();
            data.LastDirectories ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return data;
        }
    }

    private static void Update(Action<PreferenceData> update)
    {
        lock (Sync)
        {
            var settings = Read();
            update(settings);
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(directory);
                var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.settings.tmp");
                try
                {
                    File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }));
                    File.Move(temporaryPath, SettingsPath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }
            catch (Exception ex)
            {
                Logging.Logger.Error("Failed to save application preferences.", ex);
            }
        }
    }

    private sealed class PreferenceData
    {
        public string Language { get; set; } = LocalizationManager.SystemLanguage;
        public string DefaultOutputFormat { get; set; } = OutputFormatCatalog.Default;
        public bool DefaultCastAnimationOnly { get; set; }
        public bool DefaultBakeRelevantBonesOnly { get; set; }
        public Dictionary<string, string> LastDirectories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
