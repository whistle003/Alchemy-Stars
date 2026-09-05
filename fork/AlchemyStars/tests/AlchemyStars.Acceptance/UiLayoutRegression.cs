using Alchemist.UI;
using MaterialDesignThemes.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class UiLayoutRegression
{
    public static int Run(string output)
    {
        var failures = new List<string>();
        var thread = new Thread(() =>
        {
            try
            {
                Directory.CreateDirectory(output);
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var appXaml = System.Xml.Linq.XDocument.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                    "../../../../../src/Alchemist.UI/App.xaml")));
                var resources = appXaml.Descendants().First(e => e.Name.LocalName == "ResourceDictionary");
                foreach (var attribute in appXaml.Root!.Attributes().Where(a => a.IsNamespaceDeclaration))
                    resources.SetAttributeValue(attribute.Name, attribute.Value);
                var context = new System.Windows.Markup.ParserContext
                { BaseUri = new Uri("pack://application:,,,/Alchemy Stars;component/App.xaml") };
                app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(resources.ToString(), context);
                Check(app.TryFindResource("MaterialDesign.Brush.Primary") is Brush, "Missing primary/focus brush", failures);
                foreach (var culture in new[] { "zh-CN", "en-US" })
                {
                    // Select the same compiled language dictionary without touching
                    // the user's persisted language preference in a test host.
                    var dictionaries = app.Resources.MergedDictionaries;
                    dictionaries.Remove(dictionaries.Last());
                    dictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/Alchemy Stars;component/Resources/Strings.{culture}.xaml") });
                    var vm = new MainViewModel(_ => { }, string.Empty);
                    var animation = new Animation(@"D:\Example\animations\weapon_sprint_loop_with_a_long_filename.cast");
                    animation.Layers.Add(new AnimationLayer(@"D:\Example\animations\weapon_sprint_offset_additive.cast", animation));
                    animation.Layers[0].Type = AnimationLayerType.GesturePose;
                    vm.Animations.Add(animation);
                    vm.Parts.Add(new Part(vm, @"D:\Example\models\viewhands_base.cast"));
                    MainWindow.ViewModel = vm;
                    var window = new MainWindow { Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false };
                    window.Show();
                    foreach (var width in new[] { 900, 1100, 1366, 1920 })
                    {
                        window.Width = width;
                        window.Height = width == 900 ? 520 : 768;
                        Pump(window);
                        var content = (FrameworkElement)window.Content;
                        Capture(content, Path.Combine(output, $"{culture}-{width}-animations.png"));
                        var path = Descendants(content).OfType<TextBox>().Single(t => AutomationProperties.GetAutomationId(t) == "LayerPathTextBox");
                        Check(path.ActualWidth >= 160, $"{culture}/{width}: layer path only {path.ActualWidth:F1} DIPs", failures);
                        foreach (var button in Descendants(content).OfType<Button>().Where(b => b.IsVisible))
                        {
                            var name = AutomationProperties.GetName(button);
                            if (button.Content is PackIcon)
                                Check(!string.IsNullOrWhiteSpace(name), $"{culture}/{width}: unnamed icon button ({button.ToolTip})", failures);
                            if (button.Content is PackIcon icon)
                                Check(icon.Width <= button.ActualWidth - button.Padding.Left - button.Padding.Right
                                      && icon.Height <= button.ActualHeight - button.Padding.Top - button.Padding.Bottom,
                                    $"{culture}/{width}: icon clipped by button padding ({name})", failures);
                        }
                        var aboutButton = (Button)window.FindName("AboutButton");
                        var toolbar = Descendants(content).OfType<ToolBar>().Single();
                        Check(toolbar.TransformToAncestor(content).TransformBounds(new Rect(toolbar.RenderSize)).Right
                              <= ((FrameworkElement)window.FindName("LanguageButton")).TransformToAncestor(content).Transform(new Point()).X,
                            $"{culture}/{width}: toolbar overlaps language/About", failures);
                        Check(aboutButton.ActualWidth >= 44, $"{culture}/{width}: About target too small", failures);
                    }
                    window.Width = 1366; window.Height = 768; Pump(window);
                    var layerMode = Descendants(window).OfType<ComboBox>().Single(c => AutomationProperties.GetAutomationId(c) == "LayerTypeComboBox");
                    layerMode.IsDropDownOpen = true; Pump(window);
                    var popup = (Popup)layerMode.Template.FindName("PART_Popup", layerMode);
                    Capture((FrameworkElement)popup.Child, Path.Combine(output, $"{culture}-layer-modes.png"));
                    foreach (var item in layerMode.Items)
                    {
                        var container = (ComboBoxItem)layerMode.ItemContainerGenerator.ContainerFromItem(item);
                        Check(container.ActualHeight >= 36, $"{culture}: dropdown item too short", failures);
                        foreach (var text in Descendants(container).OfType<TextBlock>())
                            Check(text.TransformToAncestor(container).TransformBounds(new Rect(text.RenderSize)).Right <= container.ActualWidth,
                                $"{culture}: dropdown text clipped ({text.Text})", failures);
                    }
                    layerMode.IsDropDownOpen = false;
                    animation.Layers.Clear(); Pump(window);
                    var emptyLayerHint = Descendants(window).OfType<TextBlock>().Single(t => AutomationProperties.GetAutomationId(t) == "EmptyLayersHint");
                    Check(emptyLayerHint.IsVisible && !emptyLayerHint.IsHitTestVisible, $"{culture}: empty layer hint blocks import", failures);
                    Capture((FrameworkElement)window.Content, Path.Combine(output, $"{culture}-empty-layers.png"));
                    // Model columns resize too; all three operations remain usable.
                    var partsTab = Descendants(window).OfType<TabItem>().Single(t => AutomationProperties.GetAutomationId(t) == "PartsTab");
                    partsTab.IsSelected = true; window.Width = 900; window.Height = 520; Pump(window);
                    Capture((FrameworkElement)window.Content, Path.Combine(output, $"{culture}-900-parts.png"));
                    var modelPath = Descendants(window).OfType<TextBox>().Single(t => AutomationProperties.GetAutomationId(t) == "ModelPathTextBox");
                    Check(modelPath.ActualWidth >= 240, $"{culture}: model path is cramped", failures);
                    Check(MainWindow.IsTextEditingSource(modelPath), "Editor shortcuts are not isolated", failures);
                    // Centering needs an on-screen owner: Windows may reposition
                    // SizeToContent windows whose owners are far off-screen.
                    // Keep the test transparent so it does not cover user work.
                    window.Opacity = 0; window.Left = 40; window.Top = 40; Pump(window);
                    var about = new AboutWindow { Owner = window, Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
                    about.Show(); Pump(about);
                    Check(Descendants(about).OfType<Image>().Single().Source is BitmapSource { PixelWidth: > 0 }, $"{culture}: About logo did not load", failures);
                    Capture((FrameworkElement)about.Content, Path.Combine(output, $"{culture}-about.png"));
                    Check(about.ActualHeight < window.ActualHeight && about.ActualWidth < window.ActualWidth, $"{culture}: About exceeds owner", failures);
                    Check(IsCentered(about, window), $"{culture}: About is not centered after constraining height", failures);
                    about.Close();
                    var message = new AppMessageWindow(new string('W', 10000), "Export result — long messages remain scrollable", MessageBoxImage.Information)
                    { Owner = window, Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
                    message.Show(); Pump(message);
                    var messageContent = (FrameworkElement)message.Content;
                    var closeButton = Descendants(message).OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "AppMessageCloseButton");
                    var closeBounds = closeButton.TransformToAncestor(messageContent).TransformBounds(new Rect(closeButton.RenderSize));
                    Check(new Rect(messageContent.RenderSize).Contains(closeBounds), $"{culture}: message close button clipped ({closeBounds} in {messageContent.RenderSize})", failures);
                    var messageText = Descendants(message).OfType<TextBox>().Single(t => AutomationProperties.GetAutomationId(t) == "AppMessageText");
                    Check(messageText.TransformToAncestor(messageContent).TransformBounds(new Rect(messageText.RenderSize)).Bottom <= closeBounds.Top,
                        $"{culture}: message text overlaps close button", failures);
                    Check(messageText.ExtentHeight > messageText.ViewportHeight, $"{culture}: long message cannot scroll", failures);
                    Capture((FrameworkElement)message.Content, Path.Combine(output, $"{culture}-message.png"));
                    Check(message.ActualHeight < window.ActualHeight, $"{culture}: message exceeds owner", failures);
                    Check(IsCentered(message, window), $"{culture}: message is not centered after constraining height: dialog {message.Left},{message.Top},{message.ActualWidth},{message.ActualHeight}; owner {window.Left},{window.Top},{window.ActualWidth},{window.ActualHeight}", failures);
                    message.Close();
                    window.WindowState = WindowState.Maximized; Pump(window);
                    var maximizedMessage = new AppMessageWindow("Export completed.", "Export result", MessageBoxImage.Information)
                    { Owner = window, Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
                    maximizedMessage.Show(); Pump(maximizedMessage);
                    Check(IsCentered(maximizedMessage, window), $"{culture}: message is not centered on a maximized owner", failures);
                    maximizedMessage.Close();
                    window.WindowState = WindowState.Normal; Pump(window);
                    var settings = (FrameworkElement)Descendants(window).OfType<ToolBar>().Single().Items.OfType<Button>().Single(b => AutomationProperties.GetAutomationId(b) == "SettingsButton").CommandParameter;
                    window.Close();
                    // Render the real dialog content in a test-owned host, without
                    // changing preferences or invoking file/import/export commands.
                    var host = new Window { Content = settings, DataContext = vm, Width = 900, Height = 520, FontSize = 14,
                        Left = -15000, Top = -15000, ShowActivated = false, ShowInTaskbar = false,
                        Foreground = (Brush)app.FindResource("MaterialDesignBody"), Background = (Brush)app.FindResource("MaterialDesignPaper") };
                    host.Show(); Pump(host);
                    Capture(settings, Path.Combine(output, $"{culture}-settings.png"));
                    foreach (var radio in Descendants(settings).OfType<RadioButton>())
                    {
                        var label = Descendants(radio).OfType<ContentPresenter>().FirstOrDefault(p => p.Content is string);
                        if (label is not null)
                            Check(label.TransformToAncestor(radio).TransformBounds(new Rect(label.RenderSize)).Right <= radio.ActualWidth,
                                $"{culture}: output format {radio.Content} overflows", failures);
                        Check(((SolidColorBrush)radio.Foreground).Color.R > 160, $"{culture}: output format text has insufficient dark-theme contrast", failures);
                    }
                    var ikTab = Descendants(settings).OfType<TabItem>().Single(t => AutomationProperties.GetAutomationId(t) == "IkSettingsTab");
                    ikTab.IsSelected = true; Pump(host);
                    Capture(settings, Path.Combine(output, $"{culture}-ik-settings.png"));
                    var lastIkInput = Descendants(settings).OfType<TextBox>().Last();
                    lastIkInput.BringIntoView(); Pump(host);
                    Capture(settings, Path.Combine(output, $"{culture}-ik-settings-scrolled.png"));
                    host.Close();
                }
                app.Shutdown();
            }
            catch (Exception ex) { failures.Add(ex.ToString()); }
            finally
            {
                if (Application.Current is { } application)
                {
                    foreach (Window openWindow in application.Windows.Cast<Window>().ToArray()) openWindow.Close();
                    application.Shutdown();
                }
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        foreach (var failure in failures.Distinct()) Console.WriteLine("FAIL " + failure);
        Console.WriteLine($"UI layout checks: {failures.Count} failures. Renders: {output}");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string failure, List<string> failures)
    {
        if (!condition) failures.Add(failure);
    }

    private static bool IsCentered(Window dialog, Window owner)
    {
        var dialogBounds = new WindowAutomationPeer(dialog).GetBoundingRectangle();
        var ownerBounds = new WindowAutomationPeer(owner).GetBoundingRectangle();
        return Math.Abs(dialogBounds.Left + dialogBounds.Width / 2 - ownerBounds.Left - ownerBounds.Width / 2) < 2
            && Math.Abs(dialogBounds.Top + dialogBounds.Height / 2 - ownerBounds.Top - ownerBounds.Height / 2) < 2;
    }

    private static void Pump(Window window)
    {
        window.UpdateLayout();
        var frame = new DispatcherFrame();
        window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        window.UpdateLayout();
    }

    private static void Capture(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap((int)element.ActualWidth, (int)element.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
        {
            dc.DrawRectangle((Brush)Application.Current.FindResource("MaterialDesignPaper"), null, new Rect(element.RenderSize));
            // Render local bounds, not the element's margin/offset in its parent.
            dc.DrawRectangle(new VisualBrush(element), null, new Rect(element.RenderSize));
        }
        bitmap.Render(background);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); png.Save(stream);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
