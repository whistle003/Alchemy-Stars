using System.Windows;

namespace Alchemist.UI;

internal static class UiDialogService
{
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
