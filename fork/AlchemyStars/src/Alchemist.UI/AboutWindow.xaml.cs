using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

namespace Alchemist.UI;

public partial class AboutWindow : Window
{
    public string AppVersion { get; } = typeof(AboutWindow).Assembly.GetName().Version?.ToString(3) ?? "1.1.6";
    public string RuntimeVersion { get; } = RuntimeInformation.FrameworkDescription;
    public string OperatingSystem { get; } = RuntimeInformation.OSDescription;

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OpenUpstreamClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/Scobalula/Alchemist",
            UseShellExecute = true,
        });
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
