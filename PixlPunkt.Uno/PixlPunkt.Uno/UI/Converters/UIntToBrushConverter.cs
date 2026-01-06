using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PixlPunkt.Uno.UI.Converters
{
    /// <summary>
    /// Converts packed ARGB uint values to <see cref="SolidColorBrush"/> for XAML binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UIntToBrushConverter translates between uint color representation (0xAARRGGBB) and WinUI brushes.
    /// This enables direct binding of color uint values to visual properties like Fill, Stroke, or Background.
    /// </para>
    /// <para><strong>Color Format:</strong></para>
    /// <para>
    /// Input uint is interpreted as ARGB with byte layout:
    /// <br/>- Bits 24-31: Alpha (0=transparent, 255=opaque)
    /// <br/>- Bits 16-23: Red
    /// <br/>- Bits 8-15: Green
    /// <br/>- Bits 0-7: Blue
    /// </para>
    /// <para><strong>Fallback Behavior:</strong></para>
    /// <para>
    /// If value is not a valid uint, returns transparent black (0x00000000).
    /// ConvertBack returns 0 if brush is invalid.
    /// </para>
    /// <para><strong>XAML Usage:</strong></para>
    /// <code>
    /// &lt;Rectangle Fill="{Binding CurrentColor, Converter={StaticResource UIntToBrush}}" /&gt;
    /// </code>
    /// <para><strong>Debug Logging:</strong></para>
    /// <para>
    /// Writes converted values to debug output in hex format (e.g., "FFAA5577") for troubleshooting
    /// color binding issues.
    /// </para>
    /// </remarks>
    /// <seealso cref="BgraToBrushConverter"/>
    /// <seealso cref="UIntToHexColorConverter"/>
    public sealed class UIntToBrushConverter : IValueConverter
    {
        /// <summary>
        /// Converts a packed ARGB uint to a <see cref="SolidColorBrush"/>.
        /// </summary>
        /// <param name="value">Uint color value in ARGB format (0xAARRGGBB).</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// <see cref="SolidColorBrush"/> with ARGB color, or transparent black if value is invalid.
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is uint u)
            {
                byte a = (byte)(u >> 24);
                byte r = (byte)(u >> 16);
                byte g = (byte)(u >> 8);
                byte b = (byte)u;
                return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
            }

            return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        /// <summary>
        /// Converts a <see cref="SolidColorBrush"/> back to packed ARGB uint.
        /// </summary>
        /// <param name="value">SolidColorBrush to convert.</param>
        /// <param name="targetType">Target type (not used).</param>
        /// <param name="parameter">Optional parameter (not used).</param>
        /// <param name="language">Language (not used).</param>
        /// <returns>
        /// Packed ARGB uint (0xAARRGGBB), or 0 if brush is invalid.
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is SolidColorBrush brush)
            {
                var c = brush.Color;
                uint u = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                return u;
            }

            return 0u;
        }
    }
}
