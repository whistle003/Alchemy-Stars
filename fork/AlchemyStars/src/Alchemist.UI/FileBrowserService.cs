using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace Alchemist.UI;

internal static class FileBrowserService
{
    public static string? ChooseCastFile(Window? owner, string titleKey, string? currentPath = null)
    {
        var scope = $"cast:{titleKey}";
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Get(titleKey),
            Filter = LocalizationManager.Get("CastFilter"),
            Multiselect = false,
            CheckFileExists = true,
            InitialDirectory = GetInitialDirectory(currentPath, scope),
        };

        if (dialog.ShowDialog(owner) != true)
            return null;
        AppPreferences.SetLastDirectory(scope, dialog.FileName);
        return dialog.FileName;
    }

    public static IReadOnlyList<string> ChooseCastFiles(Window? owner, string titleKey)
    {
        var scope = $"cast:{titleKey}";
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Get(titleKey),
            Filter = LocalizationManager.Get("CastFilter"),
            Multiselect = true,
            CheckFileExists = true,
            InitialDirectory = GetInitialDirectory(null, scope),
        };

        if (dialog.ShowDialog(owner) != true)
            return [];
        AppPreferences.SetLastDirectory(scope, dialog.FileNames.FirstOrDefault());
        return dialog.FileNames;
    }

    public static string? ChooseProject(Window? owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Get("DialogOpenProject"),
            Filter = LocalizationManager.Get("ProjectFilter"),
            Multiselect = false,
            CheckFileExists = true,
            InitialDirectory = GetInitialDirectory(null, "project"),
        };

        if (dialog.ShowDialog(owner) != true)
            return null;
        AppPreferences.SetLastDirectory("project", dialog.FileName);
        return dialog.FileName;
    }

    public static string? ChooseProjectDestination(Window? owner, string? currentPath = null)
    {
        var dialog = new SaveFileDialog
        {
            Title = LocalizationManager.Get("DialogSaveProject"),
            Filter = LocalizationManager.Get("ProjectFilter"),
            AddExtension = true,
            DefaultExt = ".aprj",
            FileName = string.IsNullOrWhiteSpace(currentPath) ? string.Empty : Path.GetFileName(currentPath),
            InitialDirectory = GetInitialDirectory(currentPath, "project"),
        };

        if (dialog.ShowDialog(owner) != true)
            return null;
        AppPreferences.SetLastDirectory("project", dialog.FileName);
        return dialog.FileName;
    }

    public static string? ChooseFolder(Window? owner, string? currentPath = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationManager.Get("DialogOutputFolder"),
            FolderName = GetInitialDirectory(currentPath, "output") ?? string.Empty,
        };

        if (dialog.ShowDialog(owner) != true)
            return null;
        AppPreferences.SetLastDirectory("output", dialog.FolderName);
        return dialog.FolderName;
    }

    private static string? GetInitialDirectory(string? path, string scope) =>
        GetExistingDirectory(path) ?? AppPreferences.GetLastDirectory(scope);

    private static string? GetExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (Directory.Exists(path))
            return path;

        var directory = Path.GetDirectoryName(path);
        return Directory.Exists(directory) ? directory : null;
    }
}
