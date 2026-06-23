using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MeetingScribe.UILogic.Converters;

public class BoolToStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasData = value is bool b && b;

        // if (hasData) than return primary color, else return gray
        if (hasData)
        {
            if (Application.Current?.Resources.TryGetResource("BrushPrimary", null, out var res) == true)
                return (IBrush)res!;
            return Brushes.LimeGreen;
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}