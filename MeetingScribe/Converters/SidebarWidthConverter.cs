using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MeetingScribe.Converters;

public class SidebarWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? 240.0 : 64.0;
        }
        return 240.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}