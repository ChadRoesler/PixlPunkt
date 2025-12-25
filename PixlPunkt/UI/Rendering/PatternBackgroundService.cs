using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace PixlPunkt.UI.Rendering
{
    /// <summary>
    /// Generates and caches a Win2D pattern brush (e.g. stripes / checkerboard) in a DPI‑aware way.
    /// Reusable by canvas hosts, layer previews, thumbnails, etc.
    /// </summary>
    public sealed class PatternBackgroundService : IDisposable
    {
        private CanvasImageBrush? _brush;
        private CanvasDevice? _device;
        private int _cachedBandPx;
        private int _cachedTileSize;
        private BackgroundPatternKind _cachedKind;
        private Color _cachedLight;
        private Color _cachedDark;
        private double _cachedRasterScale;
        private int _cachedRepeatCycles;

        // Sized image caching
        private CanvasRenderTarget? _sizedRt;
        private int _cachedWidthPx;
        private int _cachedHeightPx;

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
            Invalidate(); // force rebuild
        }

        /// <summary>
        /// Returns a cached or newly built pattern brush for the given device + raster scale.
        /// Tiled brush mode: small texture, wraps seamlessly; band width constant in screen DIPs.
        /// </summary>
        public CanvasImageBrush GetBrush(CanvasDevice device, double rasterizationScale)
        {
            int bandPx = Math.Max(1, (int)Math.Round(StripeBandDip * rasterizationScale));
            int period = bandPx * 2;
            int tileSize = period * Math.Max(1, RepeatCycles);

            bool needsRebuild =
                _brush == null ||
                _device != device ||
                bandPx != _cachedBandPx ||
                tileSize != _cachedTileSize ||
                PatternKind != _cachedKind ||
                LightColor != _cachedLight ||
                DarkColor != _cachedDark ||
                Math.Abs(rasterizationScale - _cachedRasterScale) > 0.0001 ||
                RepeatCycles != _cachedRepeatCycles;

            if (!needsRebuild)
                return _brush!;

            DisposeBrushOnly();

            _device = device;
            _cachedBandPx = bandPx;
            _cachedTileSize = tileSize;
            _cachedKind = PatternKind;
            _cachedLight = LightColor;
            _cachedDark = DarkColor;
            _cachedRasterScale = rasterizationScale;
            _cachedRepeatCycles = RepeatCycles;

            var rt = new CanvasRenderTarget(device, tileSize, tileSize, 96);
            var pixels = new Color[tileSize * tileSize];

            switch (PatternKind)
            {
                case BackgroundPatternKind.Stripes:
                    {
                        int periodLocal = period;
                        for (int y = 0; y < tileSize; y++)
                        {
                            int rowIndex = y * tileSize;
                            for (int x = 0; x < tileSize; x++)
                            {
                                bool light = ((x + y) % periodLocal) < bandPx;
                                pixels[rowIndex + x] = light ? LightColor : DarkColor;
                            }
                        }
                        break;
                    }
                case BackgroundPatternKind.Checkerboard:
                    {
                        int square = bandPx;
                        for (int y = 0; y < tileSize; y++)
                        {
                            int rowIndex = y * tileSize;
                            int cy = y / square;
                            for (int x = 0; x < tileSize; x++)
                            {
                                int cx = x / square;
                                bool light = ((cx + cy) & 1) == 0;
                                pixels[rowIndex + x] = light ? LightColor : DarkColor;
                            }
                        }
                        break;
                    }
                case BackgroundPatternKind.Solid:
                    {
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = LightColor;
                        break;
                    }
            }

            rt.SetPixelColors(pixels);

            _brush = new CanvasImageBrush(device, rt)
            {
                ExtendX = CanvasEdgeBehavior.Wrap,
                ExtendY = CanvasEdgeBehavior.Wrap,
                Interpolation = CanvasImageInterpolation.NearestNeighbor,
                Transform = Matrix3x2.Identity
            };

            rt.Dispose();
            return _brush;
        }

        /// <summary>
        /// Returns a DPI-aware image exactly matching the given DIP area.
        /// This avoids any tiling seam and keeps band width constant, regardless of zoom.
        /// Rebuilt only when device, DPI, pattern settings, or area changes.
        /// </summary>
        public CanvasRenderTarget GetSizedImage(CanvasDevice device, double rasterizationScale, float areaWidthDip, float areaHeightDip)
        {
            int bandPx = Math.Max(1, (int)Math.Round(StripeBandDip * rasterizationScale));
            int widthPx = Math.Max(1, (int)Math.Ceiling(areaWidthDip * rasterizationScale));
            int heightPx = Math.Max(1, (int)Math.Ceiling(areaHeightDip * rasterizationScale));

            bool needsRebuild =
                _sizedRt == null ||
                _device != device ||
                bandPx != _cachedBandPx ||
                widthPx != _cachedWidthPx ||
                heightPx != _cachedHeightPx ||
                PatternKind != _cachedKind ||
                LightColor != _cachedLight ||
                DarkColor != _cachedDark ||
                Math.Abs(rasterizationScale - _cachedRasterScale) > 0.0001;

            if (!needsRebuild)
                return _sizedRt!;

            DisposeSizedOnly();

            _device = device;
            _cachedBandPx = bandPx;
            _cachedWidthPx = widthPx;
            _cachedHeightPx = heightPx;
            _cachedKind = PatternKind;
            _cachedLight = LightColor;
            _cachedDark = DarkColor;
            _cachedRasterScale = rasterizationScale;

            var rt = new CanvasRenderTarget(device, widthPx, heightPx, 96);
            var pixels = new Color[widthPx * heightPx];

            switch (PatternKind)
            {
                case BackgroundPatternKind.Stripes:
                    {
                        int period = bandPx * 2;
                        for (int y = 0; y < heightPx; y++)
                        {
                            int rowIndex = y * widthPx;
                            for (int x = 0; x < widthPx; x++)
                            {
                                bool light = ((x + y) % period) < bandPx;
                                pixels[rowIndex + x] = light ? LightColor : DarkColor;
                            }
                        }
                        break;
                    }
                case BackgroundPatternKind.Checkerboard:
                    {
                        int square = bandPx;
                        for (int y = 0; y < heightPx; y++)
                        {
                            int rowIndex = y * widthPx;
                            int cy = y / square;
                            for (int x = 0; x < widthPx; x++)
                            {
                                int cx = x / square;
                                bool light = ((cx + cy) & 1) == 0;
                                pixels[rowIndex + x] = light ? LightColor : DarkColor;
                            }
                        }
                        break;
                    }
                case BackgroundPatternKind.Solid:
                    {
                        for (int i = 0; i < pixels.Length; i++) pixels[i] = LightColor;
                        break;
                    }
            }

            rt.SetPixelColors(pixels);
            _sizedRt = rt;
            return _sizedRt;
        }

        /// <summary>Force rebuild on next GetBrush/GetSizedImage.</summary>
        public void Invalidate()
        {
            DisposeBrushOnly();
            DisposeSizedOnly();
        }

        private void DisposeBrushOnly()
        {
            _brush?.Dispose();
            _brush = null;
        }
        private void DisposeSizedOnly()
        {
            _sizedRt?.Dispose();
            _sizedRt = null;
        }

        public void Dispose()
        {
            DisposeBrushOnly();
            DisposeSizedOnly();
        }
    }
}