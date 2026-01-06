using System;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace PixlPunkt.Uno.UI.Rendering
{
    /// <summary>
    /// Provides pattern background configuration and theme-aware colors.
    /// Used by SkiaSharp-based canvas controls for checkerboard/stripe backgrounds.
    /// </summary>
    /// <remarks>
    /// This service manages color schemes for transparency patterns (checkerboard/stripes).
    /// Individual controls create their own SKShader/SKBitmap for rendering using these colors.
    /// </remarks>
    public sealed class PatternBackgroundService : IDisposable
    {
        private Color _cachedLight;
        private Color _cachedDark;

        public float StripeBandDip { get; set; } = 8f;               // logical DIP width of one light OR dark band
        public int RepeatCycles { get; set; } = 8;                    // number of light+dark periods per tile side
        public BackgroundPatternKind PatternKind { get; set; } = BackgroundPatternKind.Stripes;
        public Color LightColor { get; set; } = Color.FromArgb(255, 255, 255, 255);
        public Color DarkColor { get; set; } = Color.FromArgb(255, 232, 232, 232);

        public enum BackgroundPatternKind
        {
            Stripes,
            Checkerboard,
            Solid
        }

        public bool AutoTheme { get; set; } = true;

        // Preset schemes (tweak to taste)
        private static readonly (Color Light, Color Dark) LightScheme = (
            Color.FromArgb(255, 255, 255, 255),
            Color.FromArgb(255, 232, 232, 232));

        private static readonly (Color Light, Color Dark) DarkScheme = (
            Color.FromArgb(255, 48, 48, 48),
            Color.FromArgb(255, 36, 36, 36));

        // Expose current scheme (read‑only)
        public (Color Light, Color Dark) CurrentScheme => (LightColor, DarkColor);

        /// <summary>
        /// Event raised when colors or pattern settings change.
        /// </summary>
        public event Action? Changed;

        // Apply explicit theme (overrides AutoTheme if called directly)
        public void ApplyTheme(ElementTheme theme)
        {
            AutoTheme = false;
            SetStripeColorsFor(theme);
        }

        // Re-evaluate colors based on ActualTheme when AutoTheme = true
        public void SyncWith(ElementTheme theme)
        {
            if (!AutoTheme) return;
            SetStripeColorsFor(theme);
        }

        private void SetStripeColorsFor(ElementTheme theme)
        {
            var scheme = theme == ElementTheme.Dark ? DarkScheme : LightScheme;
            if (LightColor == scheme.Light && DarkColor == scheme.Dark) return;
            LightColor = scheme.Light;
            DarkColor = scheme.Dark;
            Invalidate(); // notify consumers
        }

        /// <summary>
        /// Calculates the band size in pixels for the given DPI scale.
        /// </summary>
        /// <param name="rasterizationScale">DPI rasterization scale.</param>
        /// <returns>Band size in pixels.</returns>
        public int GetBandPixelSize(double rasterizationScale)
        {
            return Math.Max(1, (int)Math.Round(StripeBandDip * rasterizationScale));
        }

        /// <summary>
        /// Calculates the tile size in pixels for the given DPI scale.
        /// </summary>
        /// <param name="rasterizationScale">DPI rasterization scale.</param>
        /// <returns>Tile size in pixels.</returns>
        public int GetTilePixelSize(double rasterizationScale)
        {
            int bandPx = GetBandPixelSize(rasterizationScale);
            int period = bandPx * 2;
            return period * Math.Max(1, RepeatCycles);
        }

        /// <summary>
        /// Checks if cached colors need update.
        /// </summary>
        /// <returns>True if colors changed since last check.</returns>
        public bool ColorsChanged()
        {
            bool changed = _cachedLight != LightColor || _cachedDark != DarkColor;
            _cachedLight = LightColor;
            _cachedDark = DarkColor;
            return changed;
        }

        /// <summary>Force consumers to rebuild their pattern resources.</summary>
        public void Invalidate()
        {
            Changed?.Invoke();
        }

        public void Dispose()
        {
            // No resources to dispose - SkiaSharp resources are owned by consumers
        }
    }
}