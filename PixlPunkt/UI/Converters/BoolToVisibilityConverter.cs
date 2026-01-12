using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PixlPunkt.UI.Converters
{
    /// <summary>
    /// Converts boolean values to <see cref="Visibility"/> for XAML binding (true=Visible, false=Collapsed).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard WinUI boolean-to-visibility converter implementing the common pattern:
    /// <br/>- <c>true</c> → <see cref="Visibility.Visible"/>
    /// <br/>- <c>false</c> → <see cref="Visibility.Collapsed"/>
    /// </para>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Show UI when a feature is enabled (e.g., "HasSelection" shows transform handles)
    /// <br/>- Display content when data is present (e.g., "HasLayers" shows layer list)
    /// <br/>- Conditional UI visibility (e.g., "IsAdmin" shows admin panel)
    /// </para>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;Button Content="Save"
    ///         Visibility="{Binding HasUnsavedChanges, Converter={StaticResource BoolToVisibility}}" /&gt;
    /// </code>
    /// </remarks>
    /// <seealso cref="InverseBoolToVisibilityConverter"/>
    public sealed partial class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a <see cref="Visibility"/> value.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <param name="targetType">The target type (not used).</param>
        /// <param name="parameter">Optional conversion parameter (not used).</param>
        /// <param name="language">The language (not used).</param>
        /// <returns><see cref="Visibility.Visible"/> if true, <see cref="Visibility.Collapsed"/> if false.</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Converts <see cref="Visibility"/> back to boolean.
        /// </summary>
        /// <param name="value">Visibility value.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>True if Visible, false otherwise.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => (value is Visibility v && v == Visibility.Visible);
    }
}
