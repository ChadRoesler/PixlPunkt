using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PixlPunkt.UI.Icons
{
    /// <summary>
    /// Custom codicons from the PixlPunkt icon font.
    /// Add new codepoints here as needed.
    /// </summary>
    public enum PixlPunktCodicon
    {
        CubeSubtract = 0xE900,
        CubeMove = 0xE901
    }

    /// <summary>
    /// Back-compat alias. Prefer <see cref="PixlPunktCodicon"/>.
    /// </summary>
    [Obsolete("Use PixlPunktCodicon instead.")]
    public enum PixlPunktGlyph
    {
        CubeSubtract = (int)PixlPunktCodicon.CubeSubtract,
        CubeMove = (int)PixlPunktCodicon.CubeMove
    }

    /// <summary>
    /// Helper for rendering custom icon-font glyphs with fallback detection.
    /// </summary>
    public static class PixlPunktIconFont
    {
        public const string FontAssetPath = "Assets/Fonts/PixlPunktIcons.ttf";
        public const string FontFamilyName = "PixlPunktIcons";
        private static readonly FontFamily IconFontFamily = new($"ms-appx:///{FontAssetPath}#{FontFamilyName}");

        private static bool? _isAvailable;

        public static bool IsAvailable => _isAvailable ??= DetectFontAsset();

        public static bool TryCreateGlyph(PixlPunktCodicon codicon, double glyphSize, out UIElement element, double opticalScale = 1d)
        {
            element = null!;
            if (!IsAvailable)
            {
                return false;
            }

            var icon = new TextBlock
            {
                Text = char.ConvertFromUtf32((int)codicon),
                FontFamily = IconFontFamily,
                FontSize = glyphSize,
                Foreground = ResolveForegroundBrush(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            if (Math.Abs(opticalScale - 1d) < 0.0001d)
            {
                element = icon;
                return true;
            }

            element = new Viewbox
            {
                Width = glyphSize * opticalScale,
                Height = glyphSize * opticalScale,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = icon
            };

            return true;
        }

        [Obsolete("Use PixlPunktCodicon overload.")]
        public static bool TryCreateGlyph(PixlPunktGlyph glyph, double glyphSize, out UIElement element, double opticalScale = 1d)
        {
            return TryCreateGlyph((PixlPunktCodicon)glyph, glyphSize, out element, opticalScale);
        }

        public static bool TryCreateGlyph(string codiconName, double glyphSize, out UIElement element, double opticalScale = 1d)
        {
            if (Enum.TryParse<PixlPunktCodicon>(codiconName, true, out var codicon))
            {
                return TryCreateGlyph(codicon, glyphSize, out element, opticalScale);
            }

            element = null!;
            return false;
        }

        private static Brush ResolveForegroundBrush()
        {
            if (Application.Current?.Resources.TryGetValue("TextFillColorPrimaryBrush", out var brushObj) == true &&
                brushObj is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(Microsoft.UI.Colors.White);
        }

        private static bool DetectFontAsset()
        {
            try
            {
                var fullPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "PixlPunktIcons.ttf");
                return File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }
    }
}
