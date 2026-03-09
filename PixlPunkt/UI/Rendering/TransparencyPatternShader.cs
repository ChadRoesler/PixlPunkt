using SkiaSharp;

namespace PixlPunkt.UI.Rendering;

/// <summary>
/// Centralized factory for diagonal-stripe transparency pattern shaders.
/// Replaces the previously-duplicated checkerboard shader code that was
/// copy-pasted across six UI control files.
/// </summary>
internal static class TransparencyPatternShader
{
    private readonly record struct CacheKey(int BandSize, uint Light, uint Dark);

    private static readonly Dictionary<CacheKey, SKShader> _cache = new();
    private static readonly object _lock = new();

    /// <summary>
    /// Returns a cached diagonal-stripe <see cref="SKShader"/> for the given
    /// band size and colours.  Creates the shader on first call for a given
    /// key; returns the cached instance thereafter.
    /// </summary>
    public static SKShader? GetShader(int bandSize, SKColor light, SKColor dark)
    {
        var key = new CacheKey(bandSize, (uint)light, (uint)dark);

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var shader = CreateDiagonalStripeShader(bandSize, light, dark);
            if (shader != null)
                _cache[key] = shader;

            return shader;
        }
    }

    /// <summary>
    /// Disposes all cached shaders.  Call when theme colours change.
    /// </summary>
    public static void InvalidateAll()
    {
        lock (_lock)
        {
            foreach (var shader in _cache.Values)
                shader?.Dispose();
            _cache.Clear();
        }
    }

    private static SKShader? CreateDiagonalStripeShader(int bandSize, SKColor light, SKColor dark)
    {
        // A tile of 2*bandSize tiles seamlessly: (x + 2B + y) / B has the
        // same parity as (x + y) / B because 2B / B = 2 (even).
        int tileSize = bandSize * 2;
        using var bitmap = new SKBitmap(tileSize, tileSize, SKColorType.Bgra8888, SKAlphaType.Premul);

        for (int y = 0; y < tileSize; y++)
        {
            for (int x = 0; x < tileSize; x++)
            {
                bool isLight = (((x + y) / bandSize) & 1) == 0;
                bitmap.SetPixel(x, y, isLight ? light : dark);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        return image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
    }
}
