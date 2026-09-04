namespace Alchemist.UI;

internal static class OutputFormatCatalog
{
    public const string Default = ".cast";

    public static readonly string[] All = [".cast", ".fbx", ".smd", ".seanim"];

    public static string Normalize(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return Default;

        var normalized = format.StartsWith('.') ? format : $".{format}";
        return All.FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase)) ?? Default;
    }
}
