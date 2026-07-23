using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace MeetingScribe.UILogic.Converters;

public class PathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string fileName && !string.IsNullOrWhiteSpace(fileName))
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Data", "Photos", fileName);

            if (File.Exists(fullPath))
            {
                try { return new Bitmap(fullPath); }
                catch { return null; }
            }
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}