using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CmdHub.ViewModels;
using MediaColor = System.Windows.Media.Color;

namespace CmdHub.Converters;

[ValueConversion(typeof(ProcessStatus), typeof(SolidColorBrush))]
public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ProcessStatus status)
        {
            return status switch
            {
                ProcessStatus.Running => new SolidColorBrush(MediaColor.FromRgb(0x4C, 0xAF, 0x50)),
                ProcessStatus.Crashed => new SolidColorBrush(MediaColor.FromRgb(0xF4, 0x43, 0x36)),
                _ => new SolidColorBrush(MediaColor.FromRgb(0x9E, 0x9E, 0x9E))
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
