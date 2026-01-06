using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using PixlPunkt.Uno.Core.Coloring.Helpers;

namespace PixlPunkt.Uno.Core.Compositing.Effects
{
    /// <summary>
    /// Reduces layer colors to a limited palette by snapping each pixel to its nearest palette color.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect performs color quantization by replacing each pixel's RGB values with the closest
    /// color from a predefined palette. Commonly used to create retro pixel art aesthetics, enforce
    /// specific color themes, or simulate limited color hardware (Game Boy, NES, CGA, etc.).
    /// </para>
    /// <para><strong>Parameters:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Colors</strong>: Observable collection of BGRA palette colors. The effect searches
    /// this set to find the best match for each pixel. Empty palette results in no effect.</item>
    /// <item><strong>Palette</strong>: Array accessor for the color collection, provided for convenience.</item>
    /// </list>
    /// <para><strong>Quantization Algorithm:</strong></para>
    /// <list type="number">
    /// <item>For each non-transparent pixel, extract RGB values (alpha is preserved).</item>
    /// <item>Compute Euclidean distance in RGB space to each palette color:
    /// <c>distance² = (R - Rₚ)² + (G - Gₚ)² + (B - Bₚ)²</c></item>
    /// <item>Select palette color with minimum distance (nearest neighbor in RGB cube).</item>
    /// <item>Replace pixel RGB with selected palette color, keeping original alpha.</item>
    /// </list>
    /// <para><strong>Distance Metric:</strong></para>
    /// <para>
    /// Uses squared Euclidean distance in RGB space for performance (avoids sqrt). This treats all
    /// color channels equally, which may not match perceptual color difference. For more accurate
    /// perceptual matching, consider converting to Lab color space (not currently implemented).
    /// </para>
    /// <para><strong>Performance:</strong></para>
    /// <para>
    /// O(pixels × paletteSize) complexity. For large palettes (>256 colors), performance may degrade.
    /// Consider using k-d trees or octrees for acceleration if palette size exceeds ~100 colors.
    /// </para>
    /// <para><strong>Use Cases:</strong></para>
    /// <para>
    /// Retro game aesthetics, enforcing brand color palettes, posterization effects, color theme
    /// consistency, or simulating vintage hardware color limitations.
    /// </para>
    /// </remarks>
    public sealed class PaletteQuantizeEffect : LayerEffectBase
    {
        public override string DisplayName => "Palette Quantize";

        // Editable palette collection used by the UI
        public ObservableCollection<uint> Colors { get; } = new();

        public PaletteQuantizeEffect()
        {
            Colors.CollectionChanged += Colors_CollectionChanged;
        }

        private void Colors_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => OnPropertyChanged(nameof(Colors));

        /// <summary>
        /// Optional backing array if your quantize code expects a raw array.
        /// </summary>
        public uint[] Palette
        {
            get => Colors.ToArray();
            set
            {
                Colors.CollectionChanged -= Colors_CollectionChanged;
                Colors.Clear();
                if (value != null)
                {
                    foreach (var c in value)
                        Colors.Add(c);
                }
                Colors.CollectionChanged += Colors_CollectionChanged;
                OnPropertyChanged(nameof(Colors));
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            int len = width * height;
            if (len == 0 || pixels.Length < len) return;
            if (Palette.Length == 0) return;

            uint[] pal = Palette;

            for (int i = 0; i < len; i++)
            {
                uint c = pixels[i];
                ColorUtil.Unpack(c, out byte a, out byte r, out byte g, out byte b);
                if (a == 0) continue;

                int bestDist = int.MaxValue;
                uint bestColor = c;

                for (int pi = 0; pi < pal.Length; pi++)
                {
                    uint pc = pal[pi];
                    ColorUtil.Unpack(pc, out _, out byte pr, out byte pg, out byte pb);

                    int dr = r - pr;
                    int dg = g - pg;
                    int db = b - pb;
                    int dist = dr * dr + dg * dg + db * db;

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestColor = pc;
                    }
                }

                // Preserve original alpha, use palette RGB
                ColorUtil.Unpack(bestColor, out _, out byte br, out byte bg, out byte bb);
                pixels[i] = ColorUtil.Pack(a, br, bg, bb);
            }
        }
    }
}
