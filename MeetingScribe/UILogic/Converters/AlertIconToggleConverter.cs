using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace MeetingScribe.UILogic.Converters;

public class AlertIconToggleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool expanded && expanded)
            ? MaterialIconKind.ChevronUp
            : MaterialIconKind.ChevronDown;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}