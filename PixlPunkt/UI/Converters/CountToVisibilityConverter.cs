using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PixlPunkt.UI.Converters
{
    /// <summary>
    /// Converts a count to Visibility (Visible if > 0, Collapsed otherwise).
    /// </summary>
    public sealed class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int count)
            {
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
