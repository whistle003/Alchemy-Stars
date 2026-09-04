using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;

namespace Alchemist.UI;

public partial class AppMessageWindow : Window
{
    public string DialogTitle { get; }
    public string Message { get; }
    public PackIconKind IconKind { get; }
    public Brush IconBrush { get; }

    public AppMessageWindow(string message, string title, MessageBoxImage image)
    {
        DialogTitle = title;
        Message = message;
        (IconKind, IconBrush) = image switch
        {
            MessageBoxImage.Error => (PackIconKind.AlertCircleOutline, Brushes.IndianRed),
            MessageBoxImage.Warning => (PackIconKind.AlertOutline, Brushes.DarkOrange),
            _ => (PackIconKind.InformationOutline, Brushes.DodgerBlue),
        };
        InitializeComponent();
        DataContext = this;
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
