using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Core.Rendering;

/// <summary>
/// SkiaSharp implementation of <see cref="ICanvasRenderer"/>.
/// Used for cross-platform rendering on Desktop (Windows/macOS/Linux) and WebAssembly.
/// </summary>
/// <remarks>
/// This renderer caches SKBitmap and SKPaint objects to minimize GC pressure during
/// frequent rendering operations. Without caching, each frame would allocate new
/// bitmaps and paints, causing significant GC pauses especially in maximized windows.
/// </remarks>
public sealed class SkiaCanvasRenderer : ICanvasRenderer
{
    private readonly SKCanvas _canvas;
    private readonly SKSurface? _surface;
    private readonly float _width;
    private readonly float _height;
    private bool _antialiasing = true;
    private readonly Stack<int> _saveStack = new();

    // ════════════════════════════════════════════════════════════════════
    // CACHED OBJECTS - Reused across frames to minimize GC pressure
    // ════════════════════════════════════════════════════════════════════
    
    /// <summary>Primary cached bitmap for DrawPixels - reused when dimensions match (document surface).</summary>
    private SKBitmap? _cachedBitmap;
    private int _cachedBitmapWidth;
    private int _cachedBitmapHeight;

    /// <summary>Secondary cached bitmap for DrawPixels - for mask overlay (typically same dimensions as primary).</summary>
    private SKBitmap? _cachedBitmap2;
    private int _cachedBitmap2Width;
    private int _cachedBitmap2Height;

    /// <summary>Tertiary cached bitmap for DrawPixels - for reference layers (may have different dimensions).</summary>
    private SKBitmap? _cachedBitmap3;

    /// <summary>
    /// Images that do not change between frames, read once and kept at the size they are drawn.
    /// </summary>
    /// <remarks>
    /// Shared across renderers rather than held by one, because a renderer lives for a single
    /// paint: an instance cache would rebuild the image every frame and be worse than no cache.
    /// The table holds its keys weakly, so an entry goes when the pixel array it belongs to does
    /// and a closed document leaves nothing behind.
    /// </remarks>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, StaticImage> _staticImages = new();

    /// <summary>
    /// The most a resized copy may cost before drawing straight from the source is the better
    /// trade. Sixteen megapixels is sixty four megabytes, comfortably past a maximised 4K window.
    /// </summary>
    private const long MaxScaledPixels = 16_000_000;

    /// <summary>Sampling for a picture already at its drawn size: only the sub pixel offset is left to cover.</summary>
    private static readonly SKSamplingOptions LinearSampling = new(SKFilterMode.Linear, SKMipmapMode.None);
    private int _cachedBitmap3Width;
    private int _cachedBitmap3Height;

    /// <summary>Cached paint for image drawing operations.</summary>
    private readonly SKPaint _imagePaint = new()
    {
        IsAntialias = false
    };

    /// <summary>Cached paint for stroke operations.</summary>
    private readonly SKPaint _strokePaint = new()
    {
        Style = SKPaintStyle.Stroke,
        IsAntialias = true
    };

    /// <summary>Cached paint for fill operations.</summary>
    private readonly SKPaint _fillPaint = new()
    {
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };

    /// <summary>
    /// Creates a renderer wrapping an existing SKCanvas (from PaintSurface event).
    /// </summary>
    public SkiaCanvasRenderer(SKCanvas canvas, float width, float height)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _width = width;
        _height = height;
    }

    /// <summary>
    /// Creates a renderer with its own SKSurface (for offscreen rendering).
    /// </summary>
    public SkiaCanvasRenderer(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info);
        _canvas = _surface.Canvas;
        _width = width;
        _height = height;
    }

    public object Device => _canvas;
    public float Width => _width;
    public float Height => _height;

    public bool Antialiasing
    {
        get => _antialiasing;
        set => _antialiasing = value;
    }

    public Matrix3x2 Transform
    {
        get
        {
            var m = _canvas.TotalMatrix;
            return new Matrix3x2(m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);
        }
        set
        {
            _canvas.SetMatrix(new SKMatrix(
                value.M11, value.M21, value.M31,
                value.M12, value.M22, value.M32,
                0, 0, 1));
        }
    }

    public void Dispose()
    {
        _surface?.Dispose();
        _cachedBitmap?.Dispose();
        _cachedBitmap2?.Dispose();
        _cachedBitmap3?.Dispose();
        _imagePaint.Dispose();
        _strokePaint.Dispose();
        _fillPaint.Dispose();
    }

    // ====================================================================
    // CLEAR
    // ====================================================================

    public void Clear(Color color)
    {
        _canvas.Clear(color.ToSKColor());
    }

    // ====================================================================
    // LINE DRAWING
    // ====================================================================

    public void DrawLine(float x1, float y1, float x2, float y2, Color color, float strokeWidth)
    {
        _strokePaint.Color = color.ToSKColor();
        _strokePaint.StrokeWidth = strokeWidth;
        _strokePaint.IsAntialias = _antialiasing;
        _canvas.DrawLine(x1, y1, x2, y2, _strokePaint);
    }

    public void DrawLine(Vector2 p1, Vector2 p2, Color color, float strokeWidth)
    {
        DrawLine(p1.X, p1.Y, p2.X, p2.Y, color, strokeWidth);
    }

    // ====================================================================
    // RECTANGLE DRAWING
    // ====================================================================

    public void DrawRectangle(float x, float y, float width, float height, Color color, float strokeWidth)
    {
        _strokePaint.Color = color.ToSKColor();
        _strokePaint.StrokeWidth = strokeWidth;
        _strokePaint.IsAntialias = _antialiasing;
        _canvas.DrawRect(x, y, width, height, _strokePaint);
    }

    public void DrawRectangle(Rect rect, Color color, float strokeWidth)
    {
        DrawRectangle((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height, color, strokeWidth);
    }

    public void FillRectangle(float x, float y, float width, float height, Color color)
    {
        _fillPaint.Color = color.ToSKColor();
        _fillPaint.IsAntialias = _antialiasing;
        _canvas.DrawRect(x, y, width, height, _fillPaint);
    }

    public void FillRectangle(Rect rect, Color color)
    {
        FillRectangle((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height, color);
    }

    public void DrawRoundedRectangle(Rect rect, float radiusX, float radiusY, Color color, float strokeWidth)
    {
        _strokePaint.Color = color.ToSKColor();
        _strokePaint.StrokeWidth = strokeWidth;
        _strokePaint.IsAntialias = _antialiasing;
        _canvas.DrawRoundRect(rect.ToSKRect(), radiusX, radiusY, _strokePaint);
    }

    public void FillRoundedRectangle(Rect rect, float radiusX, float radiusY, Color color)
    {
        _fillPaint.Color = color.ToSKColor();
        _fillPaint.IsAntialias = _antialiasing;
        _canvas.DrawRoundRect(rect.ToSKRect(), radiusX, radiusY, _fillPaint);
    }

    // ====================================================================
    // ELLIPSE DRAWING
    // ====================================================================

    public void DrawEllipse(float centerX, float centerY, float radiusX, float radiusY, Color color, float strokeWidth)
    {
        _strokePaint.Color = color.ToSKColor();
        _strokePaint.StrokeWidth = strokeWidth;
        _strokePaint.IsAntialias = _antialiasing;
        _canvas.DrawOval(centerX, centerY, radiusX, radiusY, _strokePaint);
    }

    public void FillEllipse(float centerX, float centerY, float radiusX, float radiusY, Color color)
    {
        _fillPaint.Color = color.ToSKColor();
        _fillPaint.IsAntialias = _antialiasing;
        _canvas.DrawOval(centerX, centerY, radiusX, radiusY, _fillPaint);
    }

    // ====================================================================
    // IMAGE DRAWING
    // ====================================================================

    public void DrawImage(ICanvasBitmap bitmap, Rect destRect, Rect srcRect, float opacity, ImageInterpolation interpolation)
    {
        if (bitmap.NativeBitmap is not SKBitmap skBitmap)
            throw new ArgumentException("Expected SKBitmap", nameof(bitmap));

        _imagePaint.Color = new SKColor(255, 255, 255, (byte)(opacity * 255));
        var sampling = MapInterpolation(interpolation);
        _imagePaint.IsAntialias = _antialiasing && interpolation != ImageInterpolation.NearestNeighbor;

        DrawBitmapSampled(skBitmap, srcRect.ToSKRect(), destRect.ToSKRect(), interpolation, sampling);
    }

    public void DrawImage(ICanvasBitmap bitmap, Rect destRect, Rect srcRect, float opacity)
    {
        DrawImage(bitmap, destRect, srcRect, opacity, ImageInterpolation.NearestNeighbor);
    }

    /// <summary>
    /// Draws pixels from a byte array. Uses cached bitmap pool to avoid per-frame allocations.
    /// </summary>
    /// <remarks>
    /// This method is called multiple times per frame (document surface, mask overlay, 
    /// reference layers, etc.). Caching multiple SKBitmaps by dimension eliminates massive 
    /// GC pressure that was causing stuttering, especially in maximized windows.
    /// </remarks>
    public void DrawPixels(byte[] pixels, int width, int height, Rect destRect, Rect srcRect, float opacity, ImageInterpolation interpolation)
    {
        // Get or create a cached bitmap for these dimensions
        var bitmap = GetOrCreateCachedBitmap(width, height);

        // Copy pixel data to the bitmap (this is unavoidable but much faster than allocation)
        var handle = bitmap.GetPixels();
        Marshal.Copy(pixels, 0, handle, pixels.Length);

        // Configure paint and draw
        _imagePaint.Color = new SKColor(255, 255, 255, (byte)(opacity * 255));
        var sampling = MapInterpolation(interpolation);
        _imagePaint.IsAntialias = _antialiasing && interpolation != ImageInterpolation.NearestNeighbor;

        DrawBitmapSampled(bitmap, srcRect.ToSKRect(), destRect.ToSKRect(), interpolation, sampling);
    }

    public void DrawStaticPixels(object key, byte[] pixels, int width, int height, Rect destRect, Rect srcRect, float opacity, ImageInterpolation interpolation)
    {
        if (key is null || pixels is null || width <= 0 || height <= 0) return;
        if (_canvas is null) return;

        var entry = Entry(key, pixels, width, height);
        if (entry is null) return;

        _imagePaint.Color = new SKColor(255, 255, 255, (byte)(opacity * 255));
        _imagePaint.IsAntialias = _antialiasing && interpolation != ImageInterpolation.NearestNeighbor;

        var scaled = entry.ScaledFor(destRect, srcRect, width, height, interpolation);

        if (scaled is not null)
        {
            // Already the size it is going on screen, so the costly filter does not run again.
            // Linear covers the fraction of a pixel the destination sits off by, and whatever
            // rotation is waiting in the canvas matrix.
            _canvas.DrawImage(scaled, new SKRect(0, 0, scaled.Width, scaled.Height),
                destRect.ToSKRect(), LinearSampling, _imagePaint);
            return;
        }

        _canvas.DrawImage(entry.Source, srcRect.ToSKRect(), destRect.ToSKRect(), MapInterpolation(interpolation), _imagePaint);
    }

    /// <summary>Finds the kept copy of a picture, reading it from the caller's pixels the first time.</summary>
    private static StaticImage? Entry(object key, byte[] pixels, int width, int height)
    {
        if (_staticImages.TryGetValue(key, out var kept) && kept is not null)
            return kept;

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        SKImage? source;
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), info.RowBytes);
            source = SKImage.FromPixelCopy(pixmap);
        }
        finally
        {
            handle.Free();
        }

        if (source is null) return null;

        kept = new StaticImage(source);
        _staticImages.AddOrUpdate(key, kept);
        return kept;
    }

    public void ForgetStaticPixels(object key)
    {
        if (key is null) return;

        if (_staticImages.TryGetValue(key, out var kept) && kept is not null)
            kept.Dispose();

        _staticImages.Remove(key);
    }

    /// <summary>
    /// Gets a cached bitmap with the specified dimensions, or creates one if not available.
    /// Uses a small pool (3 bitmaps) to handle common scenarios without constant reallocation.
    /// </summary>
    private SKBitmap GetOrCreateCachedBitmap(int width, int height)
    {
        // Check primary cache (usually document surface)
        if (_cachedBitmap != null && _cachedBitmapWidth == width && _cachedBitmapHeight == height)
            return _cachedBitmap;

        // Check secondary cache (usually mask overlay - often same as primary)
        if (_cachedBitmap2 != null && _cachedBitmap2Width == width && _cachedBitmap2Height == height)
            return _cachedBitmap2;

        // Check tertiary cache (reference layers, tiles, etc.)
        if (_cachedBitmap3 != null && _cachedBitmap3Width == width && _cachedBitmap3Height == height)
            return _cachedBitmap3;

        // Need to allocate or reuse a slot
        // Priority: use empty slot, then reuse tertiary (least likely to be reused)
        if (_cachedBitmap == null)
        {
            _cachedBitmap = CreateBitmap(width, height);
            _cachedBitmapWidth = width;
            _cachedBitmapHeight = height;
            return _cachedBitmap;
        }

        if (_cachedBitmap2 == null)
        {
            _cachedBitmap2 = CreateBitmap(width, height);
            _cachedBitmap2Width = width;
            _cachedBitmap2Height = height;
            return _cachedBitmap2;
        }

        if (_cachedBitmap3 == null)
        {
            _cachedBitmap3 = CreateBitmap(width, height);
            _cachedBitmap3Width = width;
            _cachedBitmap3Height = height;
            return _cachedBitmap3;
        }

        // All slots are full and none match - reuse slot 3 (least likely to match primary use cases)
        _cachedBitmap3?.Dispose();
        _cachedBitmap3 = CreateBitmap(width, height);
        _cachedBitmap3Width = width;
        _cachedBitmap3Height = height;
        return _cachedBitmap3;
    }

    /// <summary>
    /// Creates a new SKBitmap with the specified dimensions.
    /// </summary>
    private static SKBitmap CreateBitmap(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        return new SKBitmap(info);
    }

    // ====================================================================
    // TEXT DRAWING
    // ====================================================================

    public void DrawText(string text, float x, float y, Color color, ITextFormat format)
    {
        if (format.NativeFormat is not SKFont skFont)
            throw new ArgumentException("Expected SKFont", nameof(format));

        _fillPaint.Color = color.ToSKColor();
        _fillPaint.IsAntialias = true; // Text always antialiased
        _canvas.DrawText(text, x, y + skFont.Size, skFont, _fillPaint);
    }

    public ITextFormat CreateTextFormat(string fontFamily, float fontSize, FontWeight fontWeight = FontWeight.Normal)
    {
        return new SkiaTextFormat(fontFamily, fontSize, fontWeight);
    }

    public ITextLayout CreateTextLayout(string text, ITextFormat format, float maxWidth, float maxHeight)
    {
        if (format.NativeFormat is not SKFont skFont)
            throw new ArgumentException("Expected SKFont", nameof(format));

        return new SkiaTextLayout(text, skFont, maxWidth, maxHeight);
    }

    // ====================================================================
    // TRANSFORMS & CLIPPING
    // ====================================================================

    public void PushClip(Rect clipRect)
    {
        _saveStack.Push(_canvas.Save());
        _canvas.ClipRect(clipRect.ToSKRect());
    }

    public void PopClip()
    {
        if (_saveStack.Count > 0)
        {
            _canvas.RestoreToCount(_saveStack.Pop());
        }
    }

    public IDisposable CreateLayer(float opacity, Rect? clipRect = null)
    {
        var saveCount = _canvas.Save();

        if (clipRect.HasValue)
        {
            _canvas.ClipRect(clipRect.Value.ToSKRect());
        }

        if (opacity < 1.0f)
        {
            using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(opacity * 255)) };
            _canvas.SaveLayer(layerPaint);
        }

        return new LayerScope(_canvas, saveCount);
    }

    // ====================================================================
    // PATTERN/BRUSH FILLS
    // ====================================================================

    public void FillRectangleWithBrush(Rect rect, ICanvasBrush brush)
    {
        if (brush.NativeBrush is not SKShader shader)
            throw new ArgumentException("Expected SKShader", nameof(brush));

        using var paint = new SKPaint
        {
            Shader = shader,
            IsAntialias = _antialiasing
        };

        // Apply brush transform
        if (brush.Transform != Matrix3x2.Identity)
        {
            var m = brush.Transform;
            var localMatrix = new SKMatrix(m.M11, m.M21, m.M31, m.M12, m.M22, m.M32, 0, 0, 1);
            paint.Shader = shader.WithLocalMatrix(localMatrix);
        }

        _canvas.DrawRect(rect.ToSKRect(), paint);
    }

    public ICanvasBrush CreateTiledImageBrush(ICanvasBitmap bitmap)
    {
        if (bitmap.NativeBitmap is not SKBitmap skBitmap)
            throw new ArgumentException("Expected SKBitmap", nameof(bitmap));

        using var image = SKImage.FromBitmap(skBitmap);
        var shader = image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        return new SkiaCanvasBrush(shader);
    }

    // ====================================================================
    // HELPER METHODS
    // ====================================================================

    private SKPaint CreateStrokePaint(Color color, float strokeWidth)
    {
        return new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = color.ToSKColor(),
            StrokeWidth = strokeWidth,
            IsAntialias = _antialiasing
        };
    }

    private SKPaint CreateFillPaint(Color color)
    {
        return new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = color.ToSKColor(),
            IsAntialias = _antialiasing
        };
    }

    /// <summary>
    /// Draws a bitmap with the requested sampling. This SkiaSharp exposes sampling options only
    /// on <see cref="SKCanvas.DrawImage(SKImage, SKRect, SKRect, SKSamplingOptions, SKPaint)"/>,
    /// so non-nearest draws (reference layers) go through a transient <see cref="SKImage"/>;
    /// nearest-neighbour draws - every cached pixel-art bitmap - stay on the direct path, where
    /// nearest is already the default and no conversion is paid.
    /// </summary>
    private void DrawBitmapSampled(SKBitmap bitmap, SKRect src, SKRect dst, ImageInterpolation interpolation, SKSamplingOptions sampling)
    {
        if (interpolation == ImageInterpolation.NearestNeighbor)
        {
            _canvas.DrawBitmap(bitmap, src, dst, _imagePaint);
            return;
        }

        using var image = SKImage.FromBitmap(bitmap);
        _canvas.DrawImage(image, src, dst, sampling, _imagePaint);
    }

    private static SKSamplingOptions MapInterpolation(ImageInterpolation interpolation)
    {
        return interpolation switch
        {
            ImageInterpolation.NearestNeighbor => new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
            ImageInterpolation.Linear => new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
            ImageInterpolation.HighQualityCubic => new SKSamplingOptions(SKCubicResampler.Mitchell),
            _ => new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
        };
    }

    // ====================================================================
    // NESTED TYPES
    // ====================================================================

    /// <summary>
    /// A picture that does not change between frames, together with the resized copy that is what
    /// actually goes on screen.
    /// </summary>
    /// <remarks>
    /// Keeping only the source was not enough. A reference layer asks for a Mitchell cubic
    /// resample, sixteen taps per output pixel, and Skia redoes it from the source on every paint:
    /// profiling a session with one reference photo open put twenty eight percent of all the
    /// process CPU inside that one DrawImage call, which is why the brush felt stuck. The resized
    /// copy is built once per size, so painting, panning and changing opacity all reuse it and
    /// only a zoom rebuilds it.
    /// </remarks>
    private sealed class StaticImage
    {
        /// <summary>The picture at its own resolution, kept so a new size can be resized from it.</summary>
        public SKImage Source { get; }

        private SKImage? _scaled;
        private int _scaledWidth;
        private int _scaledHeight;

        public StaticImage(SKImage source) => Source = source;

        /// <summary>
        /// The picture at the size it is about to be drawn, or null when drawing straight from the
        /// source is the better answer.
        /// </summary>
        public SKImage? ScaledFor(Rect destRect, Rect srcRect, int width, int height, ImageInterpolation interpolation)
        {
            // Nearest neighbour is a lookup rather than a filter, so there is no filter to save and
            // a resized copy would only soften what is meant to stay blocky.
            if (interpolation == ImageInterpolation.NearestNeighbor) return null;

            // Only the whole picture is worth keeping: a partial source rect would want an entry
            // of its own, and nothing asks for one.
            if (srcRect.X != 0 || srcRect.Y != 0) return null;
            if ((int)srcRect.Width != width || (int)srcRect.Height != height) return null;

            int w = (int)Math.Round(destRect.Width);
            int h = (int)Math.Round(destRect.Height);

            if (w <= 0 || h <= 0) return null;
            if (w == width && h == height) return null;
            if ((long)w * h > MaxScaledPixels) return null;

            if (_scaled is not null && _scaledWidth == w && _scaledHeight == h)
                return _scaled;

            var resized = Resize(w, h, interpolation);
            if (resized is null) return null;

            _scaled?.Dispose();
            _scaled = resized;
            _scaledWidth = w;
            _scaledHeight = h;

            return _scaled;
        }

        /// <summary>
        /// Runs the filter the caller asked for, once, into an image of the drawn size. Opacity is
        /// deliberately left out so that changing it does not throw the copy away.
        /// </summary>
        private SKImage? Resize(int w, int h, ImageInterpolation interpolation)
        {
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);

            using var surface = SKSurface.Create(info);
            if (surface is null) return null;

            using var paint = new SKPaint { IsAntialias = true };

            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawImage(Source,
                new SKRect(0, 0, Source.Width, Source.Height),
                new SKRect(0, 0, w, h),
                MapInterpolation(interpolation), paint);

            return surface.Snapshot();
        }

        public void Dispose()
        {
            _scaled?.Dispose();
            _scaled = null;
            Source.Dispose();
        }
    }

    private sealed class LayerScope : IDisposable
    {
        private readonly SKCanvas _canvas;
        private readonly int _saveCount;

        public LayerScope(SKCanvas canvas, int saveCount)
        {
            _canvas = canvas;
            _saveCount = saveCount;
        }

        public void Dispose()
        {
            _canvas.RestoreToCount(_saveCount);
        }
    }
}

/// <summary>
/// SkiaSharp implementation of <see cref="ICanvasBitmap"/>.
/// </summary>
public sealed class SkiaCanvasBitmap : ICanvasBitmap
{
    private readonly SKBitmap _bitmap;
    private bool _disposed;

    public SkiaCanvasBitmap(SKBitmap bitmap)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
    }

    public SkiaCanvasBitmap(byte[] pixels, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _bitmap = new SKBitmap(info);
        var handle = _bitmap.GetPixels();
        Marshal.Copy(pixels, 0, handle, pixels.Length);
    }

    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;
    public object NativeBitmap => _bitmap;

    public void Dispose()
    {
        if (!_disposed)
        {
            _bitmap.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// SkiaSharp implementation of <see cref="ITextFormat"/>.
/// </summary>
public sealed class SkiaTextFormat : ITextFormat
{
    private readonly SKFont _font;
    private readonly SKTypeface _typeface;
    private bool _disposed;

    public SkiaTextFormat(string fontFamily, float fontSize, FontWeight fontWeight)
    {
        FontFamily = fontFamily;
        FontSize = fontSize;
        FontWeight = fontWeight;

        var weight = fontWeight switch
        {
            Rendering.FontWeight.Thin => SKFontStyleWeight.Thin,
            Rendering.FontWeight.ExtraLight => SKFontStyleWeight.ExtraLight,
            Rendering.FontWeight.Light => SKFontStyleWeight.Light,
            Rendering.FontWeight.Normal => SKFontStyleWeight.Normal,
            Rendering.FontWeight.Medium => SKFontStyleWeight.Medium,
            Rendering.FontWeight.SemiBold => SKFontStyleWeight.SemiBold,
            Rendering.FontWeight.Bold => SKFontStyleWeight.Bold,
            Rendering.FontWeight.ExtraBold => SKFontStyleWeight.ExtraBold,
            Rendering.FontWeight.Black => SKFontStyleWeight.Black,
            _ => SKFontStyleWeight.Normal
        };

        _typeface = SKTypeface.FromFamilyName(fontFamily, weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                    ?? SKTypeface.Default;
        _font = new SKFont(_typeface, fontSize);
    }

    public string FontFamily { get; }
    public float FontSize { get; }
    public FontWeight FontWeight { get; }
    public object NativeFormat => _font;

    public void Dispose()
    {
        if (!_disposed)
        {
            _font.Dispose();
            _typeface.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// SkiaSharp implementation of <see cref="ITextLayout"/>.
/// </summary>
public sealed class SkiaTextLayout : ITextLayout
{
    private readonly SKRect _bounds;

    public SkiaTextLayout(string text, SKFont font, float maxWidth, float maxHeight)
    {
        font.GetFontMetrics(out var metrics);
        
        var width = font.MeasureText(text);
        var height = font.Size;

        _bounds = new SKRect(0, 0, Math.Min(width, maxWidth), Math.Min(height, maxHeight));
        LayoutWidth = width;
        LayoutHeight = height;
    }

    public float LayoutWidth { get; }
    public float LayoutHeight { get; }
    public Rect LayoutBounds => new(0, 0, LayoutWidth, LayoutHeight);

    public void Dispose()
    {
        // Nothing to dispose for layout
    }
}

/// <summary>
/// SkiaSharp implementation of <see cref="ICanvasBrush"/>.
/// </summary>
public sealed class SkiaCanvasBrush : ICanvasBrush
{
    private SKShader _shader;
    private Matrix3x2 _transform = Matrix3x2.Identity;
    private bool _disposed;

    public SkiaCanvasBrush(SKShader shader)
    {
        _shader = shader ?? throw new ArgumentNullException(nameof(shader));
    }

    public Matrix3x2 Transform
    {
        get => _transform;
        set => _transform = value;
    }

    public object NativeBrush => _shader;

    public void Dispose()
    {
        if (!_disposed)
        {
            _shader.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Extension methods for converting between Windows and SkiaSharp types.
/// </summary>
public static class SkiaExtensions
{
    public static SKColor ToSKColor(this Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    public static Color ToWindowsColor(this SKColor color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }

    public static SKRect ToSKRect(this Rect rect)
    {
        return new SKRect((float)rect.X, (float)rect.Y, (float)(rect.X + rect.Width), (float)(rect.Y + rect.Height));
    }

    public static Rect ToWindowsRect(this SKRect rect)
    {
        return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public static SKPoint ToSKPoint(this Windows.Foundation.Point point)
    {
        return new SKPoint((float)point.X, (float)point.Y);
    }

    public static SKSize ToSKSize(this Windows.Foundation.Size size)
    {
        return new SKSize((float)size.Width, (float)size.Height);
    }
}
