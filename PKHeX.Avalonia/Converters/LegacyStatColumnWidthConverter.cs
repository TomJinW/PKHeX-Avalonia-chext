using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace PKHeX.Avalonia.Converters;

public sealed class LegacyStatColumnWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new GridLength(value is true ? 64 : 42);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
