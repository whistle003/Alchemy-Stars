using System.IO;

namespace AlchemyStars.App;

internal static class MayaPluginInstaller
{
    public static string DescribeMaya2025()
    {
        var maya = FindMaya2025();
        var installed = File.Exists(InstalledPluginPath());
        return maya is null
            ? $"未自动找到 Maya 2025；仍可安装用户级插件。插件状态：{(installed ? "已安装" : "未安装")}。"
            : $"已找到 {maya}。用户级 CAST 插件：{(installed ? "已安装" : "未安装")}。";
    }

    public static string InstallFromBundle()
    {
        var bundle = Path.Combine(AppContext.BaseDirectory, "maya-plugin");
        var sourceLibrary = Path.Combine(bundle, "cast.py");
        var sourcePlugin = Path.Combine(bundle, "castplugin.py");
        if (!File.Exists(sourceLibrary) || !File.Exists(sourcePlugin))
        {
            throw new FileNotFoundException("程序发布包缺少 maya-plugin/cast.py 或 castplugin.py，请重新构建发布包。");
        }

        var moduleRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "maya", "modules", "AlchemyStarsCast");
        var scripts = Path.Combine(moduleRoot, "scripts");
        var plugins = Path.Combine(moduleRoot, "plug-ins");
        Directory.CreateDirectory(scripts);
        Directory.CreateDirectory(plugins);

        CopyWithBackup(sourceLibrary, Path.Combine(scripts, "cast.py"));
        CopyWithBackup(sourcePlugin, Path.Combine(plugins, "castplugin.py"));
        var license = Path.Combine(bundle, "LICENSE");
        if (File.Exists(license))
        {
            CopyWithBackup(license, Path.Combine(moduleRoot, "LICENSE.cast.txt"));
        }

        var modulesFolder = Directory.GetParent(moduleRoot)?.FullName
            ?? throw new InvalidOperationException("无法解析 Maya modules 目录。");
        var moduleFile = Path.Combine(modulesFolder, "AlchemyStarsCast.mod");
        var content = $"+ AlchemyStarsCast 1.0 {moduleRoot}\n";
        if (!File.Exists(moduleFile) || !string.Equals(File.ReadAllText(moduleFile), content, StringComparison.Ordinal))
        {
            BackupIfPresent(moduleFile);
            File.WriteAllText(moduleFile, content);
        }

        return $"CAST 插件已安装到：{moduleRoot}\n重启 Maya 2025 后，在 Plug-in Manager 中加载 castplugin.py，然后直接导入 Alchemy Stars 产出的 .cast。";
    }

    private static string? FindMaya2025()
    {
        var candidates = new[]
        {
            @"D:\Maya2025\bin\maya.exe",
            @"C:\Program Files\Autodesk\Maya2025\bin\maya.exe",
            @"D:\Program Files\Autodesk\Maya2025\bin\maya.exe",
            @"E:\Program Files\Autodesk\Maya2025\bin\maya.exe",
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string InstalledPluginPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "maya", "modules", "AlchemyStarsCast", "plug-ins", "castplugin.py");

    private static void CopyWithBackup(string source, string destination)
    {
        if (File.Exists(destination) && FilesEqual(source, destination))
        {
            return;
        }

        BackupIfPresent(destination);
        File.Copy(source, destination, overwrite: true);
    }

    private static void BackupIfPresent(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var backup = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}.bak";
        File.Copy(path, backup, overwrite: false);
    }

    private static bool FilesEqual(string first, string second)
    {
        var firstInfo = new FileInfo(first);
        var secondInfo = new FileInfo(second);
        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        return File.ReadAllBytes(first).AsSpan().SequenceEqual(File.ReadAllBytes(second));
    }
}
