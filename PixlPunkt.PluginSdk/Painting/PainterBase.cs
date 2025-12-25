using PixlPunkt.PluginSdk.Imaging;

namespace PixlPunkt.PluginSdk.Painting
{
    /// <summary>
    /// Accumulation record for tracking pixel changes during a stroke.
    /// </summary>
    /// <remarks>
    /// Used to track the before/after state of each pixel modified during a brush stroke.
    /// This enables proper undo/redo support.
    /// </remarks>
    public struct AccumRec
    {
        /// <summary>Original pixel value before any modifications in this stroke.</summary>
        public uint before;

        /// <summary>Current pixel value after modifications.</summary>
        public uint after;

        /// <summary>Maximum alpha applied to this pixel (for hard-opaque brush behavior).</summary>
        public byte maxA;
    }

    /// <summary>
    /// Abstract base class for stroke painters providing common accumulation and line-drawing logic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="PainterBase"/> encapsulates the shared infrastructure used by all painting tools:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Accumulation Dictionary</strong>: Tracks before/after pixel states for history.</item>
    /// <item><strong>Touched Set</strong>: Prevents over-blending for hard-opaque brushes.</item>
    /// <item><strong>Default StampLine</strong>: Bresenham-style iteration calling StampAt at intervals.</item>
    /// </list>
    /// <para>
    /// Derived painters override <see cref="StampAt"/> to implement their specific pixel manipulation
    /// logic (blending, erasing, blurring, etc.) while reusing the common infrastructure.
    /// </para>
    /// <para><strong>Plugin Implementation:</strong></para>
    /// <para>
    /// Plugin authors should extend this class rather than implementing <see cref="IStrokePainter"/>
    /// directly to benefit from standard accumulation tracking and line interpolation.
    /// </para>
    /// </remarks>
    public abstract class PainterBase : IStrokePainter
    {
        /// <summary>Target pixel surface for painting operations.</summary>
        protected PixelSurface? Surface;

        /// <summary>Optional snapshot of surface at stroke start (for blur/smudge/jumble).</summary>
        protected byte[]? Snapshot;

        /// <summary>Accumulation dictionary mapping pixel byte-index to before/after state.</summary>
        protected readonly Dictionary<int, AccumRec> Accum = new();

        /// <summary>Set of pixel indices touched by current stroke (prevents over-blending).</summary>
        protected readonly HashSet<int> Touched = new();

        /// <inheritdoc/>
        public abstract bool NeedsSnapshot { get; }

        /// <inheritdoc/>
        public virtual void Begin(PixelSurface surface, byte[]? snapshot)
        {
            Surface = surface;
            Snapshot = snapshot;
            Accum.Clear();
            Touched.Clear();
        }

        /// <inheritdoc/>
        public abstract void StampAt(int cx, int cy, StrokeContext context);

        /// <inheritdoc/>
        /// <remarks>
        /// Default implementation uses Bresenham-style iteration calling StampAt for each pixel step.
        /// This ensures smooth line drawing without gaps. Specialized painters (like smudge) may 
        /// override to track stroke direction.
        /// </remarks>
        public virtual void StampLine(int x0, int y0, int x1, int y1, StrokeContext context)
        {
            int dx = x1 - x0, dy = y1 - y0;
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));

            if (steps == 0)
            {
                StampAt(x0, y0, context);
                return;
            }

            // Use stride of 1 for smooth, continuous line drawing
            // The max-alpha accumulation in painters prevents over-blending
            double sx = dx / (double)steps;
            double sy = dy / (double)steps;

            double x = x0, y = y0;
            for (int i = 0; i <= steps; i++)
            {
                StampAt((int)Math.Round(x), (int)Math.Round(y), context);
                x += sx;
                y += sy;
            }
        }

        /// <inheritdoc/>
        public virtual IRenderResult? End(string description = "Brush Stroke")
        {
            if (Surface == null)
            {
                Accum.Clear();
                Touched.Clear();
                Snapshot = null;
                return null;
            }

            // Create result with accumulated changes
            var result = new PixelChangeResult(Surface, description);
            foreach (var kv in Accum)
            {
                var rec = kv.Value;
                if (rec.before != rec.after)
                {
                    result.Add(kv.Key, rec.before, rec.after);
                }
            }

            // Cleanup
            Accum.Clear();
            Touched.Clear();
            Surface = null;
            Snapshot = null;

            return result.HasChanges ? result : null;
        }

        /// <summary>
        /// Helper to read a pixel from a byte array at the given byte index.
        /// </summary>
        /// <param name="pixels">The pixel byte array.</param>
        /// <param name="idx">Byte index (not pixel index).</param>
        /// <returns>The BGRA pixel value.</returns>
        protected static uint ReadPixel(byte[] pixels, int idx)
        {
            return (uint)(pixels[idx + 3] << 24 | pixels[idx + 2] << 16 | pixels[idx + 1] << 8 | pixels[idx]);
        }

        /// <summary>
        /// Helper to write a pixel to a byte array at the given byte index.
        /// </summary>
        /// <param name="pixels">The pixel byte array.</param>
        /// <param name="idx">Byte index (not pixel index).</param>
        /// <param name="value">The BGRA pixel value.</param>
        protected static void WritePixel(byte[] pixels, int idx, uint value)
        {
            pixels[idx + 0] = (byte)(value & 0xFF);
            pixels[idx + 1] = (byte)((value >> 8) & 0xFF);
            pixels[idx + 2] = (byte)((value >> 16) & 0xFF);
            pixels[idx + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>
        /// Gets or creates an accumulation record for the pixel at the given index.
        /// If a new record is created, it is immediately added to the Accum dictionary
        /// to preserve the original 'before' value for subsequent stamps.
        /// </summary>
        /// <param name="idx">Byte index into the pixel array.</param>
        /// <param name="currentValue">Current pixel value (used as 'before' if new record).</param>
        /// <returns>The accumulation record for this pixel.</returns>
        protected AccumRec GetOrCreateAccumRec(int idx, uint currentValue)
        {
            if (!Accum.TryGetValue(idx, out var rec))
            {
                rec = new AccumRec { before = currentValue, after = currentValue, maxA = 0 };
                // Store immediately to preserve the original 'before' value
                // even if we skip committing changes on this stamp
                Accum[idx] = rec;
            }
            return rec;
        }

        /// <summary>
        /// Updates the accumulation record and writes the pixel to the surface.
        /// </summary>
        /// <param name="idx">Byte index into the pixel array.</param>
        /// <param name="rec">The updated accumulation record.</param>
        protected void CommitAccumRec(int idx, AccumRec rec)
        {
            if (Surface != null)
            {
                WritePixel(Surface.Pixels, idx, rec.after);
            }
            Accum[idx] = rec;
        }
    }
}
