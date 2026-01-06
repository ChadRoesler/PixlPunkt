using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PixlPunkt.Uno.UI.Converters
{
    /// <summary>
    /// Converts boolean values to <see cref="Visibility"/> with inverted logic for XAML binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// InverseBoolToVisibilityConverter implements the opposite mapping from standard bool-to-visibility:
    /// <br/>- <c>false</c> → <see cref="Visibility.Visible"/>
    /// <br/>- <c>true</c> → <see cref="Visibility.Collapsed"/>
    /// </para>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Hide UI when a feature is enabled (e.g., "IsProcessing" hides action buttons)
    /// <br/>- Show placeholders when content is empty (e.g., "HasItems" == false shows "No items")
    /// <br/>- Display error messages when validation fails (e.g., "IsValid" == false shows error)
    /// </para>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;TextBlock Text="No layers"
    ///            Visibility="{Binding HasLayers, Converter={StaticResource InverseBoolToVisibility}}" /&gt;
    /// </code>
    /// </remarks>
    /// <seealso cref="BoolToVisibilityConverter"/>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean to <see cref="Visibility"/> with inverted logic.
        /// </summary>
        /// <param name="value">Boolean value to convert.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns><see cref="Visibility.Visible"/> if false, <see cref="Visibility.Collapsed"/> if true.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
           => (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Converts <see cref="Visibility"/> back to boolean with inverted logic.
        /// </summary>
        /// <param name="value">Visibility value.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>False if Visible, true otherwise.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => (value is Visibility v && v != Visibility.Visible);
    }
}
