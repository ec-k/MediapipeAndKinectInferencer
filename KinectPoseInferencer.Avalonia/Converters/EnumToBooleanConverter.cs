using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace KinectPoseInferencer.Avalonia.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        return value.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter is not null)
            return parameter;

        return global::Avalonia.Data.BindingOperations.DoNothing;
    }
}
