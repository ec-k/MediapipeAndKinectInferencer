using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KinectPoseInferencer.WPF.UI.Converters;

class BooleanToVisibilityConverter:IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // If parameter is 'True', invert the boolean value before conversion. 
            // This allows a single bool property (e.g., IsPlaying) to control two states
            // (e.g., showing a Play button when IsPlaying is false).
            bool invert = parameter is not null 
                && parameter is string s && s.Equals("True", StringComparison.OrdinalIgnoreCase);

            if (boolValue ^ invert)
                return Visibility.Visible;
            else
                return Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
