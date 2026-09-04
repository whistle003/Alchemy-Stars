using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace Alchemist.UI;

internal static class FileBrowserService
{
    public static string? ChooseCastFile(Window? owner, string titleKey, string? currentPath = null)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Get(titleKey),
            Filter = LocalizationManager.Get("CastFilter"),
            Multiselect = false,
            CheckFileExists = true,
            InitialDirectory = GetExistingDirectory(currentPath),
        };

        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public static IReadOnlyList<string> ChooseCastFiles(Window? owner, string titleKey)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Get(titleKey),
            Filter = LocalizationManager.Get("CastFilter"),
            Multiselect = true,
            CheckFileExists = true,
        };

        return dialog.ShowDialog(owner) == true ? dialog.FileNames : [];
    }

    public static string? ChooseProject(Window? owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationManager.Get("DialogOpenProject"),
            Filter = LocalizationManager.Get("ProjectFilter"),
            Multiselect = false,
            CheckFileExists = true,
        };

        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
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
            InitialDirectory = GetExistingDirectory(currentPath),
        };

        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public static string? ChooseFolder(Window? owner, string? currentPath = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationManager.Get("DialogOutputFolder"),
            FolderName = Directory.Exists(currentPath) ? currentPath : string.Empty,
        };

        return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
    }

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
