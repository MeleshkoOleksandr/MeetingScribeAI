using Avalonia.Data.Converters;
using Avalonia.Media;
using MeetingScribe.Enums;
using System;
using System.Globalization;

namespace MeetingScribe.UILogic.Converters;

public class LogLevelToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Critical => Brush.Parse("#ffb4ab"), // Red
                LogLevel.Warning => Brush.Parse("#81CFFF"), // Blue
                _ => Brush.Parse("#8d9382") // Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}