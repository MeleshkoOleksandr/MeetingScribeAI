using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace MeetingScribe.UILogic.Converters;

public class SidebarIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // If the panel is expanded, the icon is MenuOpen; if it is collapsed, the icon is Menu (burger)
        bool isExpanded = value is bool b && b;
        return isExpanded ? MaterialIconKind.MenuOpen : MaterialIconKind.Menu;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}