using PixlPunkt.PluginSdk.Painting;

namespace PixlPunkt.ExamplePlugin.Tools
{
    /// <summary>
    /// A sparkle brush painter that creates random sparkle particles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an example implementation of <see cref="PainterBase"/> that demonstrates:
    /// </para>
    /// <list type="bullet">
    /// <item>Reading settings from a custom settings class</item>
    /// <item>Implementing stamp-based painting</item>
    /// <item>Using random variation for artistic effects</item>
    /// <item>Using PainterBase for automatic change tracking</item>
    /// </list>
    /// </remarks>
    public sealed class SparklePainter : PainterBase
    {
        private readonly SparkleSettings _settings;
        private readonly Random _random = new();

        /// <summary>
        /// Creates a new sparkle painter with the specified settings.
        /// </summary>
        /// <param name="settings">The sparkle brush settings.</param>
        public SparklePainter(SparkleSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc/>
        public override bool NeedsSnapshot => false;

        /// <inheritdoc/>
        public override void StampAt(int cx, int cy, StrokeContext context)
        {
            if (Surface == null) return;

            int size = context.BrushSize;
            int radius = size / 2;
            byte opacity = context.BrushOpacity;

            // Create sparkle particles
            for (int i = 0; i < _settings.SparkleCount; i++)
            {
                // Random position within brush radius
                double angle = _random.NextDouble() * Math.PI * 2;
                double distance = _random.NextDouble() * radius * _settings.SparkleSpread;

                int px = cx + (int)(Math.Cos(angle) * distance);
                int py = cy + (int)(Math.Sin(angle) * distance);

                // Skip if out of bounds
                if (!context.IsInBounds(px, py))
                    continue;

                // Get the pixel color
                uint color;
                if (_settings.RandomColors)
                {
                    // Random bright color
                    byte r = (byte)_random.Next(200, 256);
                    byte g = (byte)_random.Next(200, 256);
                    byte b = (byte)_random.Next(200, 256);
                    color = (uint)((opacity << 24) | (r << 16) | (g << 8) | b);
                }
                else
                {
                    // Use foreground color with adjusted alpha
                    color = (context.ForegroundColor & 0x00FFFFFF) | ((uint)opacity << 24);
                }

                // Random size for this sparkle (1-3 pixels)
                int sparkleSize = _random.Next(1, 4);

                // Draw the sparkle as a small cluster
                for (int dy = -sparkleSize / 2; dy <= sparkleSize / 2; dy++)
                {
                    for (int dx = -sparkleSize / 2; dx <= sparkleSize / 2; dx++)
                    {
                        int sx = px + dx;
                        int sy = py + dy;

                        if (!context.IsInBounds(sx, sy))
                            continue;

                        // Random chance to draw this pixel (for sparkle effect)
                        if (_random.NextDouble() > 0.7)
                            continue;

                        int idx = context.IndexOf(sx, sy);

                        // Read current pixel value
                        uint before = ReadPixel(Surface.Pixels, idx);

                        // Track for undo using base class mechanism
                        var rec = GetOrCreateAccumRec(idx, before);

                        // Alpha blend the sparkle onto the surface
                        uint blended = BlendOver(rec.before, color);
                        rec.after = blended;

                        // Commit the change
                        CommitAccumRec(idx, rec);
                    }
                }
            }
        }

        /// <summary>
        /// Alpha blends src over dst (Porter-Duff "source over").
        /// </summary>
        private static uint BlendOver(uint dst, uint src)
        {
            // Extract components
            byte srcA = (byte)(src >> 24);
            byte srcR = (byte)(src >> 16);
            byte srcG = (byte)(src >> 8);
            byte srcB = (byte)src;

            byte dstA = (byte)(dst >> 24);
            byte dstR = (byte)(dst >> 16);
            byte dstG = (byte)(dst >> 8);
            byte dstB = (byte)dst;

            // Porter-Duff "source over"
            int invA = 255 - srcA;
            byte outR = (byte)((srcR * srcA + dstR * invA) / 255);
            byte outG = (byte)((srcG * srcA + dstG * invA) / 255);
            byte outB = (byte)((srcB * srcA + dstB * invA) / 255);
            byte outA = (byte)Math.Min(255, srcA + dstA * invA / 255);

            return (uint)((outA << 24) | (outR << 16) | (outG << 8) | outB);
        }
    }
}
