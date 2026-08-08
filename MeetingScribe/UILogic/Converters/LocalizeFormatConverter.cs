using Avalonia.Data.Converters;
using MeetingScribe.Logic.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MeetingScribe.UILogic.Converters;

public class LocalizeFormatConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not string key) return "";

        string format = LocalizationManager.Instance[key];

        var value = values[1];

        try
        {
            var currentCulture = new CultureInfo(LocalizationManager.Instance.CurrentLanguage);
            return string.Format(currentCulture, format, value);
        }
        catch { return format; }
    }
}