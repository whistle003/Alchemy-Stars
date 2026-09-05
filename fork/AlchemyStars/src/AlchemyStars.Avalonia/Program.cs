using Avalonia;
using System.Globalization;

namespace AlchemyStars.Avalonia;

internal static class Program
{
    internal static bool StartupSmokeRequested { get; private set; }
    internal static string? RenderSmokePath { get; private set; }
    internal static PixelSize? RenderSmokeSize { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        var cultureName = GetOption(args, "--culture");
        if (cultureName is not null)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            return SelfTest.Run();

        var hawkArgumentIndex = Array.FindIndex(args, argument =>
            argument.Equals("--hawk-smoke", StringComparison.OrdinalIgnoreCase));
        if (hawkArgumentIndex >= 0)
            return SelfTest.RunHawk(args.Skip(hawkArgumentIndex + 1).ToArray());

        var renderPath = GetOption(args, "--render-smoke");
        RenderSmokePath = renderPath is not null
            ? Path.GetFullPath(renderPath)
            : null;
        var renderSize = GetOption(args, "--window-size");
        RenderSmokeSize = renderSize is null ? null : ParseSize(renderSize);
        StartupSmokeRequested = RenderSmokePath is not null
            || args.Contains("--startup-smoke", StringComparer.OrdinalIgnoreCase);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();

    private static string? GetOption(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index].Equals(name, StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }
        return null;
    }

    private static PixelSize ParseSize(string value)
    {
        var separator = value.IndexOfAny(['x', 'X']);
        if (separator <= 0
            || !int.TryParse(value.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            || !int.TryParse(value.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var height)
            || width < 900
            || height < 600)
        {
            throw new ArgumentException("Window size must be WIDTHxHEIGHT and at least 900x600.", nameof(value));
        }
        return new PixelSize(width, height);
    }
}
