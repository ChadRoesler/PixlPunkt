using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace PixlPunkt.UI.Converters
{
    /// <summary>
    /// Converts packed ARGB uint values to hexadecimal color strings for XAML binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UIntToHexColorConverter enables two-way binding between uint color values (0xAARRGGBB format)
    /// and user-editable hex string representations. Supports both 6-digit RGB (assumes opaque) and
    /// 8-digit ARGB formats, with optional '#' prefix.
    /// </para>
    /// <para><strong>Supported Formats:</strong></para>
    /// <list type="bullet">
    /// <item><strong>AARRGGBB</strong>: Full 8-digit with alpha (e.g., "FF5533AA")</item>
    /// <item><strong>RRGGBB</strong>: 6-digit RGB, assumes alpha=FF (e.g., "5533AA" ? "FF5533AA")</item>
    /// <item><strong>#AARRGGBB</strong> or <strong>#RRGGBB</strong>: Optional '#' prefix is stripped</item>
    /// </list>
    /// <para><strong>Fallback Behavior:</strong></para>
    /// <para>
    /// Invalid input returns opaque black (0xFF000000). This preserves existing binding value rather
    /// than clearing it on parse errors.
    /// </para>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;TextBox Text="{Binding CurrentColor, Converter={StaticResource UIntToHexColor}, Mode=TwoWay}" /&gt;
    /// </code>
    /// </remarks>
    /// <seealso cref="UIntToBrushConverter"/>
    /// <seealso cref="UIntArrayToHexListConverter"/>
    public sealed class UIntToHexColorConverter : IValueConverter
    {
        /// <summary>
        /// Converts a packed ARGB uint to an 8-digit hexadecimal string.
        /// </summary>
        /// <param name="value">Uint color value in ARGB format (0xAARRGGBB).</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// 8-digit hex string (e.g., "FF5533AA"), or "FF000000" if value is invalid.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is uint u)
                return u.ToString("X8");
            return "FF000000";
        }

        /// <summary>
        /// Converts a hexadecimal color string back to packed ARGB uint.
        /// </summary>
        /// <param name="value">Hex string in RRGGBB or AARRGGBB format, optionally with '#' prefix.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// Packed ARGB uint (0xAARRGGBB). Returns 0xFF000000 (opaque black) if parsing fails.
        /// </returns>
        /// <remarks>
        /// Trims whitespace and strips leading '#'. 6-digit RGB is expanded to 8-digit with alpha=FF.
        /// Uses invariant culture parsing for consistent behavior regardless of locale.
        /// </remarks>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string s)
            {
                s = s.Trim();
                if (s.StartsWith("#")) s = s[1..];
                if (s.Length == 6) // treat RRGGBB as opaque
                    s = "FF" + s;
                if (uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u))
                    return u;
            }
            // Fallback: unchanged (Binding two-way will keep old value)
            return 0xFF000000u;
        }
    }
}