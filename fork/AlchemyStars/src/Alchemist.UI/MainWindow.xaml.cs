using Alchemist.Scripting;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using RedFox.UI;
using RedFox.Zenith;
using RedFox.Zenith.LicenseStorages;
using RedFox.Zenith.LicenseVerifiers;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Alchemist.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly HashSet<string> ImportSurfaceIds =
        [
            "AnimationList",
            "LayerList",
            "ModelPartsList",
        ];

        public static MainViewModel ViewModel { get; set; } = new((message) => { }, string.Empty);
        private AboutWindow? aboutWindow;

        public MainWindow()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

            InitializeComponent();
            DataContext = ViewModel;
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            Closed += (_, _) => LocalizationManager.LanguageChanged -= OnLanguageChanged;
            LoadStartupProject();
        }

        private static void LoadStartupProject()
        {
            var requestedProject = Environment.GetCommandLineArgs()
                .Skip(1)
                .FirstOrDefault(path =>
                    string.Equals(Path.GetExtension(path), ".aprj", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(path));
            if (requestedProject is null)
                return;

            try
            {
                ViewModel.LoadProjectFile(requestedProject);
            }
            catch (Exception ex)
            {
                Logging.Logger.Error($"Failed to load startup project: {requestedProject}", ex);
                MessageBox.Show(
                    LocalizationManager.Format("StartupProjectFailedMessage", requestedProject, ex.Message),
                    LocalizationManager.Get("StartupProjectFailedTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                LocalizationManager.Format("FatalErrorMessage", e.ExceptionObject),
                LocalizationManager.Get("FatalErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            try
            {
                var backupDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Alchemy Stars");
                Directory.CreateDirectory(backupDirectory);
                MainViewModel.SaveProject(ViewModel, Path.Combine(backupDirectory, "Backup.aprj"));
            }
            catch { }
        }

        private static void OnLanguageChanged(object? sender, EventArgs e) => ViewModel.RefreshLocalization();

        private void ToggleLanguageClick(object sender, RoutedEventArgs e) => LocalizationManager.Toggle();

        private void OpenAboutClick(object sender, RoutedEventArgs e)
        {
            Logging.Logger.Info("Opening About window.");
            if (aboutWindow is not null)
            {
                aboutWindow.Activate();
                return;
            }

            aboutWindow = new AboutWindow { Owner = this };
            aboutWindow.Closed += (_, _) => aboutWindow = null;
            aboutWindow.Show();
            Logging.Logger.Info($"About window shown: {aboutWindow.IsVisible}.");
        }

        private void BrowseAnimationClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Animation animation)
                return;

            var selected = FileBrowserService.ChooseCastFile(this, "DialogAddAnimation", animation.Name);
            if (selected is null)
                return;

            var previousOutputName = Path.GetFileNameWithoutExtension(animation.Name);
            var previousOutputFolder = Path.GetDirectoryName(animation.Name) ?? string.Empty;
            animation.Name = selected;
            if (string.IsNullOrWhiteSpace(animation.OutputName) || animation.OutputName == previousOutputName)
                animation.OutputName = Path.GetFileNameWithoutExtension(selected);
            if (string.IsNullOrWhiteSpace(animation.OutputFolder) || animation.OutputFolder == previousOutputFolder)
                animation.OutputFolder = Path.GetDirectoryName(selected) ?? string.Empty;
        }

        private void BrowsePoseClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Animation animation || sender is not Button button)
                return;

            var isLeft = Equals(button.Tag, "Left");
            var current = isLeft ? animation.LeftHandPoseFile : animation.RightHandPoseFile;
            var selected = FileBrowserService.ChooseCastFile(this, isLeft ? "DialogLeftPose" : "DialogRightPose", current);
            if (selected is null)
                return;

            if (isLeft)
                animation.LeftHandPoseFile = selected;
            else
                animation.RightHandPoseFile = selected;
        }

        private void ClearPoseClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Animation animation || sender is not Button button)
                return;

            if (Equals(button.Tag, "Left"))
                animation.LeftHandPoseFile = string.Empty;
            else
                animation.RightHandPoseFile = string.Empty;
        }

        private void BrowseLayerClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not AnimationLayer layer)
                return;

            var selected = FileBrowserService.ChooseCastFile(this, "DialogAddLayer", layer.Name);
            if (selected is not null)
                layer.Name = selected;
        }

        private void BrowseOutputFolderClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Animation animation)
                return;

            var selected = FileBrowserService.ChooseFolder(this, animation.OutputFolder);
            if (selected is not null)
                animation.OutputFolder = selected;
        }

        private void BrowsePartClick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not Part part)
                return;

            var selected = FileBrowserService.ChooseCastFile(this, "DialogAddPart", part.FilePath);
            if (selected is not null)
                part.FilePath = selected;
        }

        private void ImportSurfacePreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var surface = FindImportSurface(e.OriginalSource as DependencyObject)
                ?? sender as FrameworkElement;
            if (OpenImportContextMenu(surface, PlacementMode.MousePoint))
                e.Handled = true;
        }

        private void ImportSurfacePreviewKeyDown(object sender, KeyEventArgs e)
        {
            var isContextMenuKey = e.Key == Key.Apps
                || (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            if (!isContextMenuKey)
                return;

            var surface = FindImportSurface(Keyboard.FocusedElement as DependencyObject)
                ?? sender as FrameworkElement;
            if (OpenImportContextMenu(surface, PlacementMode.Center))
                e.Handled = true;
        }

        private static bool OpenImportContextMenu(FrameworkElement? surface, PlacementMode placement)
        {
            if (surface?.ContextMenu is not ContextMenu menu)
                return false;

            menu.PlacementTarget = surface;
            menu.Placement = placement;
            menu.IsOpen = true;
            return true;
        }

        private static FrameworkElement? FindImportSurface(DependencyObject? current)
        {
            while (current is not null)
            {
                if (current is FrameworkElement element
                    && ImportSurfaceIds.Contains(AutomationProperties.GetAutomationId(element)))
                    return element;

                current = GetParent(current);
            }

            return null;
        }

        private static DependencyObject? GetParent(DependencyObject current)
        {
            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                return VisualTreeHelper.GetParent(current);
            if (current is ContentElement content)
                return ContentOperations.GetParent(content)
                    ?? (content as FrameworkContentElement)?.Parent;
            return LogicalTreeHelper.GetParent(current);
        }

        private void TextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectionStart = 0;
            ((TextBox)sender).SelectionLength = ((TextBox)sender).Text.Length;
        }

        public static IEnumerable<T> GetChildOfType<T>(DependencyObject depObj) where T : DependencyObject
        {
            Stack<DependencyObject> objs = [];

            objs.Push(depObj);

            while(objs.Count > 0)
            {
                var obj = objs.Pop();
                var count = VisualTreeHelper.GetChildrenCount(obj);

                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(obj, i);

                    Trace.WriteLine(child);

                    if (child is T rVal)
                        yield return rVal;

                    objs.Push(child);
                }
            }
        }

        private void ListViewDrop(object sender, DragEventArgs e)
        {
            //Animation? poseAnim = null;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // First check, let's see if we have aprj
                if (files.Length > 0 && Path.GetExtension(files[0]).Equals(".aprj", StringComparison.CurrentCultureIgnoreCase))
                {
                    ViewModel.LoadProjectFile(files[0]);

                    foreach (var l in GetChildOfType<ListViewItem>(MainAnimationList))
                    {
                        Trace.WriteLine(l);
                    }
                }
                else
                {
                    if (sender is ListView listView && listView.SelectedItems.Count > 0)
                    {
                        foreach (var animation in listView.SelectedItems.Cast<Animation>())
                        {
                            foreach (var file in files)
                            {
                                animation.Layers.Add(new(file, animation));
                                //ViewModel.AnimationLayers.Add(layer);
                            }
                        }
                    }
                    else
                    {
                        using var modifier = new MVVMItemListModifier<Animation>(ViewModel.Animations);

                        foreach (var file in files)
                        {
                            var anim = new Animation(file);
                            modifier.Add(anim);
                        }
                    }
                }
            }
        }

        private void ModelListDrop(object sender, DragEventArgs e)
        {
            //Animation? poseAnim = null;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                using var modifier = new MVVMItemListModifier<Part>(ViewModel.Parts);

                foreach (var file in files)
                {
                    var model = new Part(ViewModel, file);
                    modifier.Add(model);
                }
            }
        }

        /// <inheritdoc/>
        private void ListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox)
                return;

            foreach (var anim in e.RemovedItems.OfType<AnimationLayer>())
                ViewModel.SelectedLayers.Remove(anim);
            foreach (var anim in e.AddedItems.OfType<AnimationLayer>())
                ViewModel.SelectedLayers.Add(anim);

            e.Handled = true;
        }

        /// <inheritdoc/>
        private void ListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListView)
                return;

            foreach (var anim in e.RemovedItems.OfType<Animation>())
                ViewModel.SelectedAnimations.Remove(anim);
            foreach (var anim in e.AddedItems.OfType<Animation>())
                ViewModel.SelectedAnimations.Add(anim);
            foreach (var model in e.RemovedItems.OfType<Part>())
                ViewModel.SelectedParts.Remove(model);
            foreach (var model in e.AddedItems.OfType<Part>())
                ViewModel.SelectedParts.Add(model);

            e.Handled = true;
        }

        private void DialogHostDialogClosing(object sender, DialogClosingEventArgs eventArgs)
        {
            if (eventArgs.Parameter is string str)
            {
                switch (str)
                {
                    case "AcceptPrefix":
                        if (!string.IsNullOrWhiteSpace(PrefixBox.Text))
                            foreach (var anim in ViewModel.SelectedAnimations)
                                anim.OutputName = PrefixBox.Text.Trim() + anim.OutputName;
                        break;
                    case "AcceptSuffix":
                        if (!string.IsNullOrWhiteSpace(SuffixBox.Text))
                            foreach (var anim in ViewModel.SelectedAnimations)
                                anim.OutputName += SuffixBox.Text.Trim();
                        break;
                    case "Run":
                        foreach (var script in ScriptsListBox.SelectedItems.Cast<Script>())
                            script.Run(ViewModel);
                        break;
                    default:
                        break;
                }
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            // https://stackoverflow.com/a/978352
            if (depObj == null) yield return (T)Enumerable.Empty<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject ithChild = VisualTreeHelper.GetChild(depObj, i);
                if (ithChild == null) continue;
                if (ithChild is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(ithChild)) yield return childOfChild;
            }
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                switch(e.Key)
                {
                    case Key.S:
                        ViewModel.SaveProjectCommand.Execute((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);
                        break;
                    case Key.C:
                        ViewModel.Clipboard.Clear();
                        ViewModel.Clipboard.AddRange(MainAnimationList.SelectedItems.OfType<Animation>());
                        break;
                    case Key.V:
                        ViewModel.Animations.AddRange(ViewModel.Clipboard.Select(x => x.Clone()));
                        break;
                    case Key.Z:
                        ViewModel.Undo();
                        break;
                    case Key.Y:
                        ViewModel.Redo();
                        break;
                    case Key.Q:
                        ViewModel.AutoAdjustColumns(ActualWidth);
                        break;
                    case Key.W:
                        ViewModel.AutoAdjustColumns(ActualWidth);
                        break;
                    case Key.X:
                        foreach (var b in FindVisualChildren<ListView>(this))
                            b.SelectedItems.Clear();
                        foreach (var b in FindVisualChildren<ListBox>(this))
                            b.SelectedItems.Clear();
                        break;
                }
            }
            else if (e.Key == Key.Delete)
            {
                ViewModel.RemoveAnimationsCommand.Execute(null);
            }
        }
    }
}
