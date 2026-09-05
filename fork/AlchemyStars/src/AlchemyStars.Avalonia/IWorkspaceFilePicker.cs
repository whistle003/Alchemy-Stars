namespace AlchemyStars.Avalonia;

public enum FilePickerPurpose
{
    Project,
    Animation,
    ModelPart,
    AnimationLayer,
    LeftPose,
    RightPose,
}

public interface IWorkspaceFilePicker
{
    Task<IReadOnlyList<string>> PickFilesAsync(FilePickerPurpose purpose, bool allowMultiple);
    Task<string?> PickProjectDestinationAsync(string? currentPath);
    Task<string?> PickFolderAsync(string? currentPath);
    Task OpenUriAsync(Uri uri);
}
