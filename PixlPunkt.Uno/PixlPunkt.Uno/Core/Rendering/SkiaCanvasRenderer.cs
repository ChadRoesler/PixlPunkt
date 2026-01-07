using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.Uno.Core.Rendering;

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
    private int _cachedBitmap3Width;
    private int _cachedBitmap3Height;

    /// <summary>Cached paint for image drawing operations.</summary>
    private readonly SKPaint _imagePaint = new()
    {
        FilterQuality = SKFilterQuality.None,
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

    // ????????????????????????????????????????????????????????????????????
    // CLEAR
    // ????????????????????????????????????????????????????????????????????

    public void Clear(Color color)
    {
        _canvas.Clear(color.ToSKColor());
    }

    // ????????????????????????????????????????????????????????????????????
    // LINE DRAWING
    // ????????????????????????????????????????????????????????????????????

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

    // ????????????????????????????????????????????????????????????????????
    // RECTANGLE DRAWING
    // ????????????????????????????????????????????????????????????????????

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

    // ????????????????????????????????????????????????????????????????????
    // ELLIPSE DRAWING
    // ????????????????????????????????????????????????????????????????????

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

    // ????????????????????????????????????????????????????????????????????
    // IMAGE DRAWING
    // ????????????????????????????????????????????????????????????????????

    public void DrawImage(ICanvasBitmap bitmap, Rect destRect, Rect srcRect, float opacity, ImageInterpolation interpolation)
    {
        if (bitmap.NativeBitmap is not SKBitmap skBitmap)
            throw new ArgumentException("Expected SKBitmap", nameof(bitmap));

        _imagePaint.Color = new SKColor(255, 255, 255, (byte)(opacity * 255));
        _imagePaint.FilterQuality = MapInterpolation(interpolation);
        _imagePaint.IsAntialias = _antialiasing && interpolation != ImageInterpolation.NearestNeighbor;

        _canvas.DrawBitmap(skBitmap, srcRect.ToSKRect(), destRect.ToSKRect(), _imagePaint);
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
        _imagePaint.FilterQuality = MapInterpolation(interpolation);
        _imagePaint.IsAntialias = _antialiasing && interpolation != ImageInterpolation.NearestNeighbor;

        _canvas.DrawBitmap(bitmap, srcRect.ToSKRect(), destRect.ToSKRect(), _imagePaint);
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

    // ????????????????????????????????????????????????????????????????????
    // TEXT DRAWING
    // ????????????????????????????????????????????????????????????????????

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

    // ????????????????????????????????????????????????????????????????????
    // TRANSFORMS & CLIPPING
    // ????????????????????????????????????????????????????????????????????

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

    // ????????????????????????????????????????????????????????????????????
    // PATTERN/BRUSH FILLS
    // ????????????????????????????????????????????????????????????????????

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

    // ????????????????????????????????????????????????????????????????????
    // HELPER METHODS
    // ????????????????????????????????????????????????????????????????????

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

    private static SKFilterQuality MapInterpolation(ImageInterpolation interpolation)
    {
        return interpolation switch
        {
            ImageInterpolation.NearestNeighbor => SKFilterQuality.None,
            ImageInterpolation.Linear => SKFilterQuality.Low,
            ImageInterpolation.HighQualityCubic => SKFilterQuality.High,
            _ => SKFilterQuality.None
        };
    }

    // ????????????????????????????????????????????????????????????????????
    // NESTED TYPES
    // ????????????????????????????????????????????????????????????????????

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
        using var paint = new SKPaint { IsAntialias = true };
        paint.GetFontMetrics(out var metrics);
        
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
