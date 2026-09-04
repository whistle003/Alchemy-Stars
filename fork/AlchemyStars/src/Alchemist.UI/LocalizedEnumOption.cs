using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Alchemist.UI;

public sealed class LocalizedEnumOption<TEnum>(TEnum value, string resourceKey) : INotifyPropertyChanged
    where TEnum : struct, Enum
{
    public TEnum Value { get; } = value;
    public string DisplayName => LocalizationManager.Get(resourceKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
}
