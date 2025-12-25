using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Coloring.Helpers;

namespace PixlPunkt.UI.Converters
{
    /// <summary>
    /// Converts packed BGRA uint values to <see cref="SolidColorBrush"/> for XAML binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BgraToBrushConverter handles PixlPunkt's internal BGRA color format (0xAARRGGBB in memory order,
    /// but B/G/R/A byte layout). Unlike <see cref="UIntToBrushConverter"/> which expects ARGB, this
    /// converter uses <see cref="ColorUtil.ToColor"/> to correctly interpret BGRA-packed values.
    /// </para>
    /// <para><strong>Color Format Difference:</strong></para>
    /// <list type="table">
    /// <listheader>
    /// <term>Converter</term>
    /// <description>Format</description>
    /// </listheader>
    /// <item>
    /// <term>UIntToBrush</term>
    /// <description>ARGB (0xAARRGGBB) - Standard WinUI format</description>
    /// </item>
    /// <item>
    /// <term>BgraToBrush</term>
    /// <description>BGRA (B/G/R/A bytes) - PixlPunkt internal format</description>
    /// </item>
    /// </list>
    /// <para><strong>Type Handling:</strong></para>
    /// <para>
    /// Accepts multiple numeric types (uint, int, long, ulong) and hex strings. All are converted to
    /// uint before interpretation. Fallback is opaque black (0xFF000000) if value is invalid.
    /// </para>
    /// <para><strong>One-Way Conversion:</strong></para>
    /// <para>
    /// ConvertBack always returns opaque black. This converter is intended for display-only scenarios
    /// where BGRA values are shown but not edited through the brush.
    /// </para>
    /// </remarks>
    /// <seealso cref="ColorUtil"/>
    /// <seealso cref="UIntToBrushConverter"/>
    public sealed partial class BgraToBrushConverter : IValueConverter
    {
        // ════════════════════════════════════════════════════════════════════
        // CONVERT METHOD
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a packed BGRA uint (or compatible numeric type) to a <see cref="SolidColorBrush"/>.
        /// </summary>
        /// <param name="value">
        /// BGRA color value as uint, int, long, ulong, or hex string. Value is interpreted using
        /// <see cref="ColorUtil.ToColor"/> which handles BGRA byte order.
        /// </param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// <see cref="SolidColorBrush"/> with BGRA color converted to ARGB, or opaque black if value is invalid.
        /// </returns>
        /// <remarks>
        /// Handles numeric boxing variations common in WinUI binding. Hex strings are parsed with invariant culture.
        /// </remarks>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            uint u;

            // Handle multiple numeric boxings
            if (value is uint u32) u = u32;
            else if (value is int i32) u = unchecked((uint)i32);
            else if (value is long i64) u = unchecked((uint)i64);
            else if (value is ulong u64) u = unchecked((uint)u64);
            else if (value is string s &&
                     uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var parsed))
                u = parsed;
            else
                u = 0xFF000000u; // fallback: opaque black

            // If alpha happens to be 0 but color exists, you can force alpha:
            // if ((u & 0xFF000000u) == 0 && (u & 0x00FFFFFFu) != 0) u |= 0xFF000000u;

            return new SolidColorBrush(ColorUtil.ToColor(u));
        }

        // ════════════════════════════════════════════════════════════════════
        // CONVERT BACK METHOD
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Converts a brush back to packed BGRA uint (not implemented - returns opaque black).
        /// </summary>
        /// <param name="value">Brush value (not used).</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>Always returns 0xFF000000 (opaque black).</returns>
        /// <remarks>
        /// This converter is intended for one-way binding (display only). Use dedicated color pickers
        /// for editing BGRA values.
        /// </remarks>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => 0xFF000000u;
    }
}
