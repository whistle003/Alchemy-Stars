using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Alchemist.UI;

internal static class MayaFbxExporter
{
    private const int ExportTimeoutMilliseconds = 10 * 60 * 1000;

    public static void Export(string castPath, string fbxPath, float framerate)
    {
        var mayapy = FindMayapy()
            ?? throw new FileNotFoundException(LocalizationManager.Get("FbxMayaNotFound"));
        var script = FindConverterScript()
            ?? throw new FileNotFoundException(LocalizationManager.Get("FbxConverterMissing"));
        var pluginDirectory = Path.Combine(AppContext.BaseDirectory, "MayaPlugin");
        if (!File.Exists(Path.Combine(pluginDirectory, "castplugin.py")))
            pluginDirectory = FindDevelopmentPluginDirectory()
                ?? throw new FileNotFoundException(LocalizationManager.Get("FbxCastPluginMissing"));

        var startInfo = new ProcessStartInfo
        {
            FileName = mayapy,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add(Path.GetFullPath(castPath));
        startInfo.ArgumentList.Add(Path.GetFullPath(fbxPath));
        startInfo.ArgumentList.Add(pluginDirectory);
        startInfo.ArgumentList.Add(framerate.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(LocalizationManager.Get("FbxMayaStartFailed"));
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(ExportTimeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(LocalizationManager.Get("FbxMayaTimedOut"));
        }
        Task.WaitAll(standardOutput, standardError);
        if (process.ExitCode != 0 || !File.Exists(fbxPath) || new FileInfo(fbxPath).Length == 0)
        {
            var details = string.Join(Environment.NewLine,
                new[] { standardError.Result.Trim(), standardOutput.Result.Trim() }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            throw new InvalidOperationException(LocalizationManager.Format(
                "FbxMayaFailed",
                process.ExitCode,
                details));
        }
    }

    internal static string? FindMayapy()
    {
        foreach (var candidate in EnumerateMayapyCandidates())
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }
        return null;
    }

    private static IEnumerable<string> EnumerateMayapyCandidates()
    {
        var overridePath = Environment.GetEnvironmentVariable("ALCHEMY_STARS_MAYAPY");
        if (!string.IsNullOrWhiteSpace(overridePath))
            yield return overridePath;

        var mayaLocation = Environment.GetEnvironmentVariable("MAYA_LOCATION");
        if (!string.IsNullOrWhiteSpace(mayaLocation))
            yield return Path.Combine(mayaLocation, "bin", "mayapy.exe");

        var years = Enumerable.Range(2020, 12).Reverse();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (var year in years)
        {
            if (!string.IsNullOrWhiteSpace(programFiles))
                yield return Path.Combine(programFiles, "Autodesk", $"Maya{year}", "bin", "mayapy.exe");
            foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady))
                yield return Path.Combine(drive.RootDirectory.FullName, $"Maya{year}", "bin", "mayapy.exe");
        }
    }

    private static string? FindConverterScript() => FindUpwardFile(
        Path.Combine("Converters", "export_cast_to_fbx.py"),
        Path.Combine("maya", "export_cast_to_fbx.py"));

    private static string? FindDevelopmentPluginDirectory()
    {
        var plugin = FindUpwardFile(Path.Combine("third_party", "cast", "maya", "castplugin.py"));
        return plugin is null ? null : Path.GetDirectoryName(plugin);
    }

    private static string? FindUpwardFile(params string[] relativePaths)
    {
        foreach (var seed in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(seed); directory is not null; directory = directory.Parent)
            {
                foreach (var relativePath in relativePaths)
                {
                    var candidate = Path.Combine(directory.FullName, relativePath);
                    if (File.Exists(candidate))
                        return Path.GetFullPath(candidate);
                }
            }
        }
        return null;
    }
}
