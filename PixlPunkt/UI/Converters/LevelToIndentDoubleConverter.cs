using System;
using Microsoft.UI.Xaml.Data;

namespace PixlPunkt.UI.Converters
{
    /// <summary>
    /// Converts tree depth levels to double for width/margin binding in hierarchical layouts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LevelToIndentDoubleConverter is similar to <see cref="LevelToIndentConverter"/> but returns
    /// a double value instead of <see cref="Microsoft.UI.Xaml.Thickness"/>. This enables binding to
    /// Width, Height, or numeric properties that require scalar values.
    /// </para>
    /// <para><strong>Indent Calculation:</strong></para>
    /// <para>
    /// <c>Result = level × Indent</c>
    /// <br/>Example with Indent=14 (default):
    /// <br/>- Level 0 → 0.0
    /// <br/>- Level 1 → 14.0
    /// <br/>- Level 2 → 28.0
    /// </para>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;local:LevelToIndentDoubleConverter x:Key="LevelToIndentDouble" Indent="16" /&gt;
    /// 
    /// &lt;Border Width="{Binding Depth, Converter={StaticResource LevelToIndentDouble}}" /&gt;
    /// </code>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Indent spacer widths for tree views
    /// <br/>- Progressive disclosure animations (width transitions)
    /// <br/>- Hierarchical spacing calculations
    /// <br/>- Level-based sizing (e.g., progressively smaller icons at deeper levels)
    /// </para>
    /// </remarks>
    /// <seealso cref="LevelToIndentConverter"/>
    public sealed class LevelToIndentDoubleConverter : IValueConverter
    {
        /// <summary>
        /// Gets or sets the indentation width per level in pixels.
        /// </summary>
        /// <value>
        /// Number of pixels to indent per depth level. Default is 14.
        /// </value>
        public double Indent { get; set; } = 14;

        /// <summary>
        /// Converts a depth level integer to indentation width.
        /// </summary>
        /// <param name="value">Integer depth level (0 = root, 1 = first child, etc.).</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// Double value = level × <see cref="Indent"/>. Returns 0.0 if value is not an integer.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int level = 0;
            if (value is int i) level = i;
            return level * Indent; // <-- Double for Width
        }

        /// <summary>
        /// Converts back (not supported - throws <see cref="NotSupportedException"/>).
        /// </summary>
        /// <exception cref="NotSupportedException">Always thrown; conversion back is not meaningful.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();
    }
}
