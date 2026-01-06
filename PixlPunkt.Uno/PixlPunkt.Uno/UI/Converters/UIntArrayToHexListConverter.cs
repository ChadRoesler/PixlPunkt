using System;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml.Data;
using PixlPunkt.Uno.Core.Palette.Helpers.Defaults;

namespace PixlPunkt.Uno.UI.Converters
{
    /// <summary>
    /// Converts arrays of packed ARGB uint values to/from comma-separated hex color strings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UIntArrayToHexListConverter enables editing of color palettes and lists through text input.
    /// Useful for palette import/export, color list configuration, and batch color editing.
    /// </para>
    /// <para><strong>Format:</strong></para>
    /// <para>
    /// Colors are separated by commas, spaces, newlines, or semicolons. Each color can be:
    /// <br/>- <strong>AARRGGBB</strong>: Full 8-digit with alpha (e.g., "FF5533AA")
    /// <br/>- <strong>RRGGBB</strong>: 6-digit RGB, assumes alpha=FF (e.g., "5533AA")
    /// <br/>- <strong>#AARRGGBB</strong> or <strong>#RRGGBB</strong>: Optional '#' prefix
    /// </para>
    /// <para><strong>Example:</strong></para>
    /// <code>
    /// Input:  uint[] { 0xFF5533AA, 0xFF22BB44, 0xFFCC8800 }
    /// Output: "FF5533AA,FF22BB44,FFCC8800"
    /// 
    /// Input:  "#5533AA, #22BB44, CC8800"
    /// Output: uint[] { 0xFF5533AA, 0xFF22BB44, 0xFFCC8800 }
    /// </code>
    /// <para><strong>Parsing Behavior:</strong></para>
    /// <para>
    /// Invalid tokens are skipped (filtered out). Empty input returns empty array.
    /// Flexible delimiter support (comma, space, newline, semicolon) enables paste from various sources.
    /// </para>
    /// </remarks>
    /// <seealso cref="UIntToHexColorConverter"/>
    /// <seealso cref="DefaultPalettes"/>
    public sealed class UIntArrayToHexListConverter : IValueConverter
    {
        /// <summary>
        /// Converts a uint array to a comma-separated hex string.
        /// </summary>
        /// <param name="value">Array of ARGB uint colors.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// Comma-separated hex string (e.g., "FF5533AA,FF22BB44"), or empty string if array is null/empty.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is uint[] arr && arr.Length > 0)
                return string.Join(",", arr.Select(c => c.ToString("X8")));
            return string.Empty;
        }

        /// <summary>
        /// Converts a delimited hex string back to a uint array.
        /// </summary>
        /// <param name="value">String containing hex colors separated by comma, space, newline, or semicolon.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// Array of parsed ARGB uint colors. Invalid tokens are skipped. Empty input returns empty array.
        /// </returns>
        /// <remarks>
        /// Supports multiple delimiters for flexible paste operations. Each token is trimmed, '#' prefix
        /// is stripped, and 6-digit RGB is expanded to 8-digit with alpha=FF. Uses invariant culture parsing.
        /// </remarks>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string s)
            {
                var parts = s.Split(new[] { ',', ' ', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var list = parts
                    .Select(p =>
                    {
                        var token = p.Trim();
                        if (token.StartsWith("#")) token = token[1..];
                        if (token.Length == 6) token = "FF" + token;
                        return uint.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u) ? (uint?)u : null;
                    })
                    .Where(u => u.HasValue)
                    .Select(u => u.Value)
                    .ToArray();
                return list;
            }
            return Array.Empty<uint>();
        }
    }
}