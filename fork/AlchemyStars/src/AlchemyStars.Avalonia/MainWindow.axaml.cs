using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AlchemyStars.Avalonia;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public MainWindowViewModel InitializeWorkspace(IAnimationExportEngine engine, WorkspaceProjectStore projectStore, ApplicationPreferencesStore preferences)
    {
        var viewModel = new MainWindowViewModel(engine, projectStore, preferences, new AvaloniaFilePickerAdapter(this, preferences));
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MainWindowViewModel.IsDialogOpen) && viewModel.IsDialogOpen)
                Dispatcher.UIThread.Post(() => DialogCloseButton.Focus());
        };
        return viewModel;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void NewProjectClick(object? sender, RoutedEventArgs e) => ViewModel.NewProject();
    private async void OpenProjectClick(object? sender, RoutedEventArgs e) => await ViewModel.OpenProjectAsync();
    private async void SaveProjectClick(object? sender, RoutedEventArgs e) => await ViewModel.SaveProjectAsync(false);
    private async void SaveProjectAsClick(object? sender, RoutedEventArgs e) => await ViewModel.SaveProjectAsync(true);
    private async void ExportClick(object? sender, RoutedEventArgs e) => await ViewModel.ExportAsync();
    private async void AddAnimationClick(object? sender, RoutedEventArgs e) => await ViewModel.AddAnimationsAsync();
    private async void AddPartClick(object? sender, RoutedEventArgs e) => await ViewModel.AddPartsAsync();
    private async void AddLayerClick(object? sender, RoutedEventArgs e) => await ViewModel.AddLayersAsync();
    private void RemoveAnimationClick(object? sender, RoutedEventArgs e) => ViewModel.RemoveSelectedAnimation();
    private void GenerateSprintBatchClick(object? sender, RoutedEventArgs e) => ViewModel.GenerateSprintBatch();
    private void RemovePartClick(object? sender, RoutedEventArgs e) => ViewModel.RemoveSelectedPart();
    private void RemoveLayerClick(object? sender, RoutedEventArgs e) => ViewModel.RemoveSelectedLayer();
    private void MovePartUpClick(object? sender, RoutedEventArgs e) => ViewModel.MoveSelectedPart(-1);
    private void MovePartDownClick(object? sender, RoutedEventArgs e) => ViewModel.MoveSelectedPart(1);
    private void MoveLayerUpClick(object? sender, RoutedEventArgs e) => ViewModel.MoveSelectedLayer(-1);
    private void MoveLayerDownClick(object? sender, RoutedEventArgs e) => ViewModel.MoveSelectedLayer(1);
    private async void BrowseAnimationClick(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedAnimation is { } item) await ViewModel.ReplaceAnimationSourceAsync(item); }
    private async void BrowsePartClick(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedPart is { } item) await ViewModel.ReplacePartSourceAsync(item); }
    private async void BrowseLayerClick(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedLayer is { } item) await ViewModel.ReplaceLayerSourceAsync(item); }
    private async void BrowseLeftPoseClick(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedAnimation is { } item) await ViewModel.SetPoseAsync(item, true); }
    private async void BrowseRightPoseClick(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedAnimation is { } item) await ViewModel.SetPoseAsync(item, false); }
    private async void BrowseOutputFolderClick(object? sender, RoutedEventArgs e) { if (ViewModel.SelectedAnimation is { } item) await ViewModel.SetOutputFolderAsync(item); }
    private void AnimationsPageClick(object? sender, RoutedEventArgs e) => ViewModel.SelectPage(WorkspacePage.Animations);
    private void PartsPageClick(object? sender, RoutedEventArgs e) => ViewModel.SelectPage(WorkspacePage.ModelParts);
    private void SettingsPageClick(object? sender, RoutedEventArgs e) => ViewModel.SelectPage(WorkspacePage.Settings);
    private void AboutPageClick(object? sender, RoutedEventArgs e) => ViewModel.SelectPage(WorkspacePage.About);
    private void LanguageClick(object? sender, RoutedEventArgs e) => ViewModel.ToggleLanguage();
    private void SystemLanguageClick(object? sender, RoutedEventArgs e) => ViewModel.UseSystemLanguage();
    private void SaveDefaultsClick(object? sender, RoutedEventArgs e) => ViewModel.SaveDefaults();
    private void CloseDialogClick(object? sender, RoutedEventArgs e) => ViewModel.CloseDialog();
    private async void OpenUpstreamClick(object? sender, RoutedEventArgs e) => await ViewModel.OpenUpstreamAsync();

    private void DropZoneDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;

    private void PathDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = HasFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;

    private void AnimationDrop(object? sender, DragEventArgs e)
    {
        var paths = GetDroppedPaths(e);
        if (paths.Count == 1 && string.Equals(Path.GetExtension(paths[0]), ".aprj", StringComparison.OrdinalIgnoreCase))
            ViewModel.LoadProject(paths[0]);
        else
            ViewModel.AddAnimationPaths(paths);
        e.DragEffects = DragDropEffects.Copy;
    }

    private void PartDrop(object? sender, DragEventArgs e)
    {
        ViewModel.AddPartPaths(GetDroppedPaths(e));
        e.DragEffects = DragDropEffects.Copy;
    }

    private void LayerDrop(object? sender, DragEventArgs e)
    {
        // This dedicated target intentionally wins over the surrounding animation page.
        ViewModel.AddLayerPaths(GetDroppedPaths(e));
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void AnimationPathDrop(object? sender, DragEventArgs e) => SetDroppedPath(ViewModel.SelectedAnimation, "animation", e);
    private void LeftPosePathDrop(object? sender, DragEventArgs e) => SetDroppedPath(ViewModel.SelectedAnimation, "leftPose", e);
    private void RightPosePathDrop(object? sender, DragEventArgs e) => SetDroppedPath(ViewModel.SelectedAnimation, "rightPose", e);
    private void OutputPathDrop(object? sender, DragEventArgs e) => SetDroppedPath(ViewModel.SelectedAnimation, "output", e);
    private void PartPathDrop(object? sender, DragEventArgs e) => SetDroppedPath(ViewModel.SelectedPart, "part", e);
    private void LayerPathDrop(object? sender, DragEventArgs e) => SetDroppedPath(ViewModel.SelectedLayer, "layer", e);

    private void SetDroppedPath(object? target, string role, DragEventArgs e)
    {
        var path = GetDroppedPaths(e).FirstOrDefault();
        if (target is not null && path is not null)
        {
            ViewModel.SetPathFromDrop(target, path, role);
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ViewModel.IsDialogOpen)
        {
            ViewModel.CloseDialog();
            e.Handled = true;
            return;
        }
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;
        switch (e.Key)
        {
            case Key.O: await ViewModel.OpenProjectAsync(); e.Handled = true; break;
            case Key.S when e.KeyModifiers.HasFlag(KeyModifiers.Shift): await ViewModel.SaveProjectAsync(true); e.Handled = true; break;
            case Key.S: await ViewModel.SaveProjectAsync(false); e.Handled = true; break;
            case Key.E: await ViewModel.ExportAsync(); e.Handled = true; break;
        }
    }

    private void DialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.CloseDialog();
            e.Handled = true;
        }
    }

    private static bool HasFiles(DragEventArgs e) => e.DataTransfer.Formats.Contains(DataFormat.File);

    private static IReadOnlyList<string> GetDroppedPaths(DragEventArgs e) => e.DataTransfer.TryGetFiles()?
        .Select(item => item.Path.LocalPath)
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .ToArray() ?? [];
}
