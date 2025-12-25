using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using PixlPunkt.Core.Document.Layer;


namespace PixlPunkt.UI.Converters
{
    /// <summary>
    /// Converts tree depth levels to <see cref="Thickness"/> for hierarchical indentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LevelToIndentConverter creates visual hierarchy in tree views by converting depth integers to
    /// left margin thickness. Each level adds configurable indent pixels, creating nested visual structure.
    /// </para>
    /// <para><strong>Indent Calculation:</strong></para>
    /// <para>
    /// <c>Margin.Left = level × Indent</c>
    /// <br/>Example with Indent=12:
    /// <br/>- Level 0 → Thickness(0, 0, 0, 0)
    /// <br/>- Level 1 → Thickness(12, 0, 0, 0)
    /// <br/>- Level 2 → Thickness(24, 0, 0, 0)
    /// </para>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;local:LevelToIndentConverter x:Key="LevelToIndent" Indent="16" /&gt;
    /// 
    /// &lt;TextBlock Margin="{x:Bind Depth, Converter={StaticResource LevelToIndent}}" /&gt;
    /// </code>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// - Layer panel tree indentation (see <see cref="LayerListItem"/>)
    /// <br/>- File/folder explorer hierarchies
    /// <br/>- Nested menu structures
    /// <br/>- Outline/document tree views
    /// </para>
    /// </remarks>
    /// <seealso cref="LevelToIndentDoubleConverter"/>
    /// <seealso cref="LayerListItem"/>
    public sealed partial class LevelToIndentConverter : IValueConverter
    {
        /// <summary>
        /// Gets or sets the indentation width per level in pixels.
        /// </summary>
        /// <value>
        /// Number of pixels to indent per depth level. Default is 12.
        /// </value>
        public double Indent { get; set; } = 12;

        /// <summary>
        /// Converts a depth level integer to left margin thickness.
        /// </summary>
        /// <param name="value">Integer depth level (0 = root, 1 = first child, etc.).</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// <see cref="Thickness"/> with left margin = level × <see cref="Indent"/>, other margins zero.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, string language)
            => new Thickness(System.Convert.ToInt32(value) * Indent, 0, 0, 0);

        /// <summary>
        /// Converts back (not supported - returns 0).
        /// </summary>
        /// <returns>Always returns 0.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => 0;
    }
}
