using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AlchemyStars.Avalonia;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new AnimationExportEngine()),
            };
            if (Program.StartupSmokeRequested)
            {
                mainWindow.ShowInTaskbar = false;
                mainWindow.Opacity = Program.RenderSmokePath is null ? 0 : 1;
                if (Program.RenderSmokePath is not null)
                {
                    if (Program.RenderSmokeSize is { } renderSize)
                    {
                        mainWindow.Width = renderSize.Width;
                        mainWindow.Height = renderSize.Height;
                    }
                    mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    mainWindow.Position = new PixelPoint(-32000, -32000);
                }
                mainWindow.Opened += (_, _) => DispatcherTimer.RunOnce(() =>
                {
                    if (Program.RenderSmokePath is not null)
                    {
                        var width = Math.Max(1, (int)Math.Ceiling(mainWindow.ClientSize.Width));
                        var height = Math.Max(1, (int)Math.Ceiling(mainWindow.ClientSize.Height));
                        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
                        bitmap.Render(mainWindow);
                        bitmap.Save(Program.RenderSmokePath, PngBitmapEncoderOptions.Default);
                    }
                    desktop.Shutdown(0);
                }, TimeSpan.FromMilliseconds(500));
            }
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
