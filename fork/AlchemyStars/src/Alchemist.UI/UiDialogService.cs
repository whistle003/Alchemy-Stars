using System.Windows;

namespace Alchemist.UI;

internal static class UiDialogService
{
    internal static void ConstrainToOwner(Window dialog)
    {
        if (dialog.Owner is not { IsLoaded: true } owner) return;
        // SizeToContent may settle after Loaded/UpdateLayout. Recenter using the
        // final measured size too, not only the initial window dimensions.
        dialog.SizeChanged += (_, _) => CenterOnOwner(dialog);
        dialog.MaxWidth = Math.Max(320, owner.ActualWidth - 32);
        dialog.MaxHeight = Math.Max(240, owner.ActualHeight - 48);
        dialog.UpdateLayout();
        CenterOnOwner(dialog);
    }

    private static void CenterOnOwner(Window dialog)
    {
        if (dialog.Owner is not { IsLoaded: true } owner) return;
        dialog.Left = owner.Left + (owner.ActualWidth - dialog.ActualWidth) / 2;
        dialog.Top = owner.Top + (owner.ActualHeight - dialog.ActualHeight) / 2;
    }

    public static void Show(
        string message,
        string title,
        MessageBoxImage image,
        Window? owner = null)
    {
        Logging.Logger.Info($"Showing centered application dialog: {title}");
        var application = Application.Current;
        if (application is not null && !application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => Show(message, title, image, owner));
            return;
        }

        var effectiveOwner = owner ?? application?.MainWindow;
        var dialog = new AppMessageWindow(message, title, image);
        if (effectiveOwner is not null && effectiveOwner.IsLoaded)
            dialog.Owner = effectiveOwner;
        else
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
        Logging.Logger.Info($"Closed centered application dialog: {title}");
    }
}
