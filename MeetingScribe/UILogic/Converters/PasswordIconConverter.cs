using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace MeetingScribe.UILogic.Converters;

public class PasswordIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible = value is bool b && b;
        return isVisible ? MaterialIconKind.EyeOffOutline : MaterialIconKind.EyeOutline;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}