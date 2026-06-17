using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace MeetingScribe.UILogic.Converters;

public class PauseIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // If paused (true) -> display the Play icon (to resume)
        // If recording (false) -> display the Pause icon
        bool isPaused = value is bool b && b;
        return isPaused ? MaterialIconKind.Play : MaterialIconKind.Pause;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}