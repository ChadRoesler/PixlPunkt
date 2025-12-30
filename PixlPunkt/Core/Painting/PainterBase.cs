using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Structs;

namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Accumulation record for tracking pixel changes during a stroke.
    /// </summary>
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
    /// Plugin authors should extend <see cref="PluginSdk.Painting.PainterBase"/> rather than this class.
    /// This class is for built-in painters that need direct access to <see cref="RasterLayer"/> for history.
    /// </para>
    /// </remarks>
    public abstract class PainterBase : IStrokePainter
    {
        /// <summary>Target raster layer for painting operations and history tracking.</summary>
        protected RasterLayer? Layer;

        /// <summary>Target pixel surface for painting operations (convenience reference to Layer.Surface).</summary>
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
        public virtual void Begin(RasterLayer layer, byte[]? snapshot)
        {
            Layer = layer;
            // Use GetPaintingSurface() to support mask editing mode
            Surface = layer.GetPaintingSurface();
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
        public virtual IRenderResult? End(string description = "Brush Stroke", Icon icon = Icon.History)
        {
            if (Layer == null)
            {
                Logging.LoggingService.Warning("PainterBase.End called with null Layer, Accum has {AccumCount} entries", Accum.Count);
                Accum.Clear();
                Touched.Clear();
                Surface = null;
                Snapshot = null;
                return null;
            }

            var item = new PixelChangeItem(Layer, description, icon);

            int changedCount = 0;
            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            int surfaceWidth = Surface?.Width ?? 1;
            
            foreach (var kv in Accum)
            {
                var rec = kv.Value;
                if (rec.before != rec.after)
                {
                    item.Add(kv.Key, rec.before, rec.after);
                    changedCount++;
                    
                    // Track pixel bounds for logging
                    int pixelIndex = kv.Key / 4;
                    int x = pixelIndex % surfaceWidth;
                    int y = pixelIndex / surfaceWidth;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (changedCount > 0)
            {
                Logging.LoggingService.Info("PainterBase.End: {ChangedCount} changes, bounds X=[{MinX},{MaxX}] Y=[{MinY},{MaxY}]", 
                    changedCount, minX, maxX, minY, maxY);
            }
            else
            {
                Logging.LoggingService.Info("PainterBase.End: No pixel changes recorded (Accum had {AccumCount} entries)", Accum.Count);
            }

            // Cleanup
            Accum.Clear();
            Touched.Clear();
            Layer = null;
            Surface = null;
            Snapshot = null;

            return item.IsEmpty ? null : item;
        }

        /// <summary>
        /// Helper to read a pixel from the surface at the given byte index.
        /// </summary>
        protected static uint ReadPixel(byte[] pixels, int idx)
            => Bgra.ReadUIntFromBytes(pixels, idx);

        /// <summary>
        /// Helper to write a pixel to the surface at the given byte index.
        /// </summary>
        protected static void WritePixel(byte[] pixels, int idx, uint value)
            => Bgra.WriteUIntToBytes(pixels, idx, value);

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
