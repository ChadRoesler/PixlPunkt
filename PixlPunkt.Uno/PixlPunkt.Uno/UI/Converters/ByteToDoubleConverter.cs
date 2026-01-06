using System;
using Microsoft.UI.Xaml.Data;

namespace PixlPunkt.Uno.UI.Converters
{
    /// <summary>
    /// Converts byte values (0-255) to/from double for numeric controls like sliders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ByteToDoubleConverter enables binding byte properties (e.g., opacity, color channels) to
    /// WinUI numeric controls that require double values. Handles bidirectional conversion with
    /// automatic clamping to byte range.
    /// </para>
    /// <para><strong>Conversion Rules:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Forward</strong>: byte → double (simple cast, preserves value)</item>
    /// <item><strong>Backward</strong>: double → byte (rounded to nearest integer, clamped to 0-255)</item>
    /// </list>
    /// <para><strong>Input Flexibility:</strong></para>
    /// <para>
    /// Convert method accepts byte or int (clamped to 0-255). ConvertBack accepts double, float, or int
    /// and rounds to nearest integer before clamping. This handles various numeric boxings from WinUI controls.
    /// </para>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;Slider Minimum="0" Maximum="255"
    ///         Value="{x:Bind LayerOpacity, Converter={StaticResource ByteToDouble}, Mode=TwoWay}" /&gt;
    /// </code>
    /// </remarks>
    /// <seealso cref="LevelToIndentConverter"/>
    public sealed class ByteToDoubleConverter : IValueConverter
    {
        /// <summary>
        /// Converts a byte (or int) to double.
        /// </summary>
        /// <param name="value">Byte or int value to convert. Int values are clamped to 0-255.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>Double representation of the byte value (0.0-255.0).</returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is byte b) return (double)b;
            if (value is int i) return (double)Math.Clamp(i, 0, 255);
            return 0.0;
        }

        /// <summary>
        /// Converts a double (or float/int) back to byte with rounding and clamping.
        /// </summary>
        /// <param name="value">Double, float, or int value to convert.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>Byte value rounded to nearest integer and clamped to 0-255 range.</returns>
        /// <remarks>
        /// Uses Math.Round for nearest-integer rounding (banker's rounding). Clamps result to valid byte
        /// range to prevent overflow.
        /// </remarks>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            double d = value is double dv ? dv
                      : value is float fv ? fv
                      : value is int iv ? iv
                      : 0.0;
            int clamped = Math.Clamp((int)Math.Round(d), 0, 255);
            return (byte)clamped;
        }
    }
}
