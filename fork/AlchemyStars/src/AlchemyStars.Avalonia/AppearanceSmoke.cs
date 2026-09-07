using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace AlchemyStars.Avalonia;

internal static class AppearanceSmoke
{
    internal static async Task RunAsync(MainWindow window, MainWindowViewModel vm)
    {
        var app = Application.Current!;
        var workspace = vm.Workspace;
        var stylePicker = window.FindControl<ComboBox>("ThemeStylePicker")!;
        var modePicker = window.FindControl<ComboBox>("ThemeModePicker")!;
        vm.SelectPage(WorkspacePage.Settings);
        var originalStyle = vm.ThemeStyleIndex;
        var originalMode = vm.ThemeModeIndex;
        var directory = Path.GetDirectoryName(Program.RenderSmokePath!)!;
        Directory.CreateDirectory(directory);
        try
        {
            // Change the actual frontend controls, not only the view-model values.
            foreach (var style in new[] { 1, 0, 1, 0 })
            foreach (var mode in new[] { 1, 0 })
            {
                stylePicker.SelectedIndex = style;
                modePicker.SelectedIndex = mode;
                await Task.Delay(160);
                Require(vm.ThemeStyleIndex == style && vm.ThemeModeIndex == mode, "Appearance picker binding failed.");
                Require(ReferenceEquals(workspace, vm.Workspace), "Theme switch recreated the workspace.");
                Require(app.ActualThemeVariant == (mode == 1 ? ThemeVariant.Dark : ThemeVariant.Light), "Fluent theme mode did not switch.");
                Require(stylePicker.Bounds.Height == 44 && modePicker.Bounds.Height == 44, "Appearance control heights drifted.");
                var shadow = (BoxShadows)app.Resources["AppearancePanelShadow"]!;
                Require(shadow.Equals(BoxShadows.Parse("none")) == (style == 0), "Style switch left stale shadows.");
                var snapshot = new ApplicationPreferencesStore().Snapshot();
                Require(snapshot.ThemeStyle == (style == 1 ? "neumorphic" : "apple")
                    && snapshot.ThemeMode == (mode == 1 ? "dark" : "light"), "Appearance selection did not survive a disk reload.");
                window.VerifyToolbarLayout();
                await VerifyTooltipsAsync(window, directory, style, mode);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                bitmap.Render(window);
                bitmap.Save(Path.Combine(directory, $"{(style == 1 ? "neumorphic" : "apple")}-{(mode == 1 ? "dark" : "light")}.png"), PngBitmapEncoderOptions.Default);
            }
            modePicker.SelectedIndex = 2;
            await Task.Delay(160);
            Require(app.RequestedThemeVariant == ThemeVariant.Default && vm.ThemeModeIndex == 2, "System mode did not delegate to Avalonia.");
            // Verify palette updates when the actual variant changes (same event
            // path used by OS light/dark notifications), without changing Windows.
            app.RequestedThemeVariant = ThemeVariant.Dark;
            await Task.Delay(80);
            Require(((SolidColorBrush)app.Resources["AlchemySurfaceBrush"]!).Color == Color.Parse("#272729"), "Actual dark change did not update palette.");
            app.RequestedThemeVariant = ThemeVariant.Light;
            await Task.Delay(80);
            Require(((SolidColorBrush)app.Resources["AlchemySurfaceBrush"]!).Color == Colors.White, "Actual light change did not update palette.");
            vm.ToggleLanguage();
            await Task.Delay(80);
            Require(vm.ThemeModeIndex == 2 && vm.ThemeStyleIndex == 0, "Language refresh reset appearance selection.");
            vm.ToggleLanguage();
            Console.WriteLine("Appearance smoke passed: live selectors, repeated transitions, 44px heights, persistence, system delegation and language refresh.");
        }
        finally
        {
            vm.ThemeStyleIndex = originalStyle;
            vm.ThemeModeIndex = originalMode;
            AppearanceTheme.Apply(originalStyle == 1 ? "neumorphic" : "apple", originalMode switch { 1 => "dark", 2 => "system", _ => "light" });
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task VerifyTooltipsAsync(MainWindow window, string directory, int style, int mode)
    {
        var index = 0;
        foreach (var button in window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("toolbar")))
        {
            ToolTip.SetIsOpen(button, true);
            try
            {
                await Task.Delay(100);
                var tip = button.GetValue(global::Avalonia.Controls.Diagnostics.ToolTipDiagnostics.ToolTipProperty) as ToolTip
                    ?? throw new InvalidOperationException("Toolbar tooltip failed to open.");
                var text = tip.GetVisualDescendants().OfType<TextBlock>().Single(t => !string.IsNullOrEmpty(t.Text));
                var expected = ((SolidColorBrush)Application.Current!.Resources["AlchemyTextBrush"]!).Color;
                Require(text.Foreground is ISolidColorBrush foreground && foreground.Color == expected,
                    "Toolbar text color leaked into its tooltip.");
                Require(tip.Background is ISolidColorBrush background && background.Color != expected,
                    "Tooltip surface hides its text.");
                Require(text.Text == ToolTip.GetTip(button)?.ToString(), "Tooltip label changed.");
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(tip.Bounds.Width), (int)Math.Ceiling(tip.Bounds.Height)));
                bitmap.Render(tip);
                bitmap.Save(Path.Combine(directory, $"tooltip-{style}-{mode}-{index++}.png"), PngBitmapEncoderOptions.Default);
            }
            finally { ToolTip.SetIsOpen(button, false); }
        }
    }
}
