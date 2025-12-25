using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using PixlPunkt.Core.Document.Layer;

namespace PixlPunkt.UI.Converters
{
    /// <summary>
    /// Converts layer types to <see cref="Visibility"/> for expand/collapse chevron display.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LayerTypeToChevronVisibilityConverter determines whether a layer should display an expand/collapse
    /// chevron in tree views. Only <see cref="LayerFolder"/> instances (which can contain children) show
    /// chevrons; <see cref="RasterLayer"/> instances (leaf nodes) have chevrons collapsed.
    /// </para>
    /// <para><strong>Conversion Logic:</strong></para>
    /// <list type="bullet">
    /// <item><see cref="RasterLayer"/> → <see cref="Visibility.Collapsed"/> (no chevron)</item>
    /// <item><see cref="LayerFolder"/> → <see cref="Visibility.Visible"/> (show chevron)</item>
    /// <item>Any other type → <see cref="Visibility.Visible"/> (defensive default)</item>
    /// </list>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;TextBlock Text="▼" 
    ///            Visibility="{x:Bind Layer, Converter={StaticResource LayerTypeToChevron}}" /&gt;
    /// </code>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Layer panel tree view chevron visibility
    /// <br/>- Hierarchical navigation indicators
    /// <br/>- Conditional UI based on layer type capabilities
    /// </para>
    /// </remarks>
    /// <seealso cref="LayerBase"/>
    /// <seealso cref="LayerFolder"/>
    /// <seealso cref="RasterLayer"/>
    /// <seealso cref="LayerListItem"/>
    public class LayerTypeToChevronVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a layer object to chevron visibility.
        /// </summary>
        /// <param name="value">Layer object (<see cref="LayerBase"/> derived type).</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// <see cref="Visibility.Collapsed"/> if value is <see cref="RasterLayer"/>;
        /// <see cref="Visibility.Visible"/> otherwise.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (((value is RasterLayer)))
            {
                return Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        /// <summary>
        /// Converts back (not supported - throws <see cref="NotImplementedException"/>).
        /// </summary>
        /// <exception cref="NotImplementedException">Always thrown; reverse conversion is not meaningful.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
