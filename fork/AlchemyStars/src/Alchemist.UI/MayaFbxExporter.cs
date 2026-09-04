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

        var workspace = CreateAsciiWorkspace();
        try
        {
            var mayaCastPath = Path.Combine(workspace, "input.cast");
            var mayaFbxPath = Path.Combine(workspace, "output.fbx");
            var mayaScriptPath = Path.Combine(workspace, "convert.py");
            var mayaPluginDirectory = Path.Combine(workspace, "plugin");
            Directory.CreateDirectory(mayaPluginDirectory);
            File.Copy(Path.GetFullPath(castPath), mayaCastPath, overwrite: true);
            File.Copy(script, mayaScriptPath, overwrite: true);
            foreach (var pluginFile in Directory.EnumerateFiles(pluginDirectory, "*.py"))
                File.Copy(pluginFile, Path.Combine(mayaPluginDirectory, Path.GetFileName(pluginFile)), overwrite: true);

            var startInfo = new ProcessStartInfo
            {
                FileName = mayapy,
                WorkingDirectory = workspace,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(mayaScriptPath);
            startInfo.ArgumentList.Add(mayaCastPath);
            startInfo.ArgumentList.Add(mayaFbxPath);
            startInfo.ArgumentList.Add(mayaPluginDirectory);
            startInfo.ArgumentList.Add(framerate.ToString(System.Globalization.CultureInfo.InvariantCulture));

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(LocalizationManager.Get("FbxMayaStartFailed"));
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ExportTimeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(30_000);
                }
                catch (Exception ex)
                {
                    Logging.Logger.Warn("Failed to terminate the timed-out Maya FBX process cleanly.", ex);
                }
                throw new TimeoutException(LocalizationManager.Get("FbxMayaTimedOut"));
            }
            Task.WaitAll(standardOutput, standardError);
            if (process.ExitCode != 0 || !File.Exists(mayaFbxPath) || new FileInfo(mayaFbxPath).Length == 0)
            {
                var details = string.Join(Environment.NewLine,
                    new[] { standardError.Result.Trim(), standardOutput.Result.Trim() }
                        .Where(value => !string.IsNullOrWhiteSpace(value)));
                var message = LocalizationManager.Format(
                    "FbxMayaFailed",
                    process.ExitCode,
                    details);
                if (!string.IsNullOrWhiteSpace(details)
                    && !message.Contains(details, StringComparison.Ordinal))
                {
                    message = $"{message}{Environment.NewLine}{details}";
                }
                throw new InvalidOperationException(message);
            }

            var destination = Path.GetFullPath(fbxPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(mayaFbxPath, destination, overwrite: true);
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                try
                {
                    Directory.Delete(workspace, recursive: true);
                }
                catch (Exception ex)
                {
                    Logging.Logger.Warn($"Failed to clean Maya FBX workspace: {workspace}", ex);
                }
            }
        }
    }

    private static string CreateAsciiWorkspace()
    {
        var candidates = new List<string?>
        {
            Path.GetTempPath(),
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        };
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrWhiteSpace(systemRoot))
            candidates.Add(Path.Combine(systemRoot, "Temp"));
        candidates.AddRange(DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName));

        foreach (var candidate in candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Path.GetFullPath(candidate!))
            .Where(candidate => candidate.All(character => character <= 0x7f))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var workspace = Path.Combine(candidate, "AlchemyStarsFbx", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(workspace);
                return workspace;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logging.Logger.Debug($"ASCII Maya workspace is not writable: {candidate}", ex);
            }
        }

        throw new IOException(LocalizationManager.Get("FbxAsciiWorkspaceUnavailable"));
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

        var years = new[] { 2025 }
            .Concat(Enumerable.Range(2020, 12).Reverse().Where(year => year != 2025));
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
