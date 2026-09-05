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
        var (iconKind, brushKey) = image switch
        {
            MessageBoxImage.Error => (PackIconKind.AlertCircleOutline, "AlchemyDestructiveBrush"),
            MessageBoxImage.Warning => (PackIconKind.AlertOutline, "AlchemyAccentBrush"),
            _ => (PackIconKind.InformationOutline, "AlchemyInfoBrush"),
        };
        IconKind = iconKind;
        IconBrush = Application.Current.TryFindResource(brushKey) as Brush ?? SystemColors.ControlTextBrush;
        InitializeComponent();
        DataContext = this;
        Loaded += (_, _) => UiDialogService.ConstrainToOwner(this);
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}
