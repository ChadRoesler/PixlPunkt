using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Logging;
using Windows.Graphics;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for pixel-level changes on a single layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PixelChangeItem tracks individual pixel changes using delta compression - only pixels
    /// that actually changed are stored, making it memory-efficient for localized edits.
    /// </para>
    /// <para>
    /// Each item stores the layer reference, byte indices, and before/after values for
    /// all modified pixels.
    /// </para>
    /// <para>
    /// Implements both <see cref="IHistoryItem"/> for undo/redo support and
    /// <see cref="IRenderResult"/> for unified tool result handling.
    /// </para>
    /// </remarks>
    public sealed class PixelChangeItem : IHistoryItem, IRenderResult
    {
        private readonly RasterLayer _layer;
        private readonly string _description;
        private readonly Icon _historyIcon;
        private readonly List<int> _indices = new();
        private readonly List<uint> _before = new();
        private readonly List<uint> _after = new();

        // ====================================================================
        // IHistoryItem / IRenderResult SHARED PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets or sets the icon representing this history item.
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.History;

        /// <summary>
        /// Gets the description of this pixel change.
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// Gets whether this change item has any actual pixel changes.
        /// </summary>
        public bool IsEmpty => _indices.Count == 0;

        // ====================================================================
        // IRenderResult IMPLEMENTATION
        // ====================================================================

        /// <inheritdoc/>
        public bool HasChanges => !IsEmpty;

        /// <inheritdoc/>
        public bool CanPushToHistory => HasChanges;

        // ====================================================================
        // BOUNDING RECT
        // ====================================================================

        /// <summary>
        /// Gets the bounding rectangle of all pixel changes.
        /// </summary>
        /// <returns>The bounding rectangle (minX, minY, maxX, maxY), or null if no changes.</returns>
        public (int minX, int minY, int maxX, int maxY)? GetBoundingRect()
        {
            if (_indices.Count == 0)
                return null;

            int surfW = _layer.Surface.Width;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var byteIndex in _indices)
            {
                int pixelIndex = byteIndex / 4;
                int x = pixelIndex % surfW;
                int y = pixelIndex / surfW;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            return (minX, minY, maxX, maxY);
        }

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        /// <summary>
        /// Creates a new pixel change item for the specified layer.
        /// </summary>
        /// <param name="layer">The layer being modified.</param>
        /// <param name="description">Description of the operation (e.g., "Brush Stroke", "Fill").</param>
        /// <param name="historyIcon">Icon representing the operation.</param>
        public PixelChangeItem(RasterLayer layer, string description = "Pixel Change", Icon historyIcon = Icon.History)
        {
            _historyIcon = historyIcon;
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _description = description;
            _historyIcon = historyIcon;
        }

        /// <summary>
        /// Adds a single pixel change to this item.
        /// </summary>
        /// <param name="byteIndex">The byte index in the surface pixel array.</param>
        /// <param name="beforeValue">The BGRA value before the change.</param>
        /// <param name="afterValue">The BGRA value after the change.</param>
        public void Add(int byteIndex, uint beforeValue, uint afterValue)
        {
            _indices.Add(byteIndex);
            _before.Add(beforeValue);
            _after.Add(afterValue);
        }

        /// <summary>
        /// Appends pixel changes from a rectangular region by comparing before/after snapshots.
        /// Only pixels that differ are added.
        /// </summary>
        /// <param name="rect">The rectangle in surface coordinates.</param>
        /// <param name="before">Pixel data before the change (BGRA, row-major).</param>
        /// <param name="after">Pixel data after the change (BGRA, row-major).</param>
        public void AppendRegionDelta(RectInt32 rect, byte[] before, byte[] after)
        {
            int w = rect.Width, h = rect.Height;
            int surfW = _layer.Surface.Width;
            if (w <= 0 || h <= 0) return;

            int bi = 0;
            for (int y = 0; y < h; y++)
            {
                int rowStart = ((rect.Y + y) * surfW + rect.X) * 4;
                for (int x = 0; x < w; x++, bi += 4)
                {
                    uint b = (uint)(before[bi + 0] | (before[bi + 1] << 8) | (before[bi + 2] << 16) | (before[bi + 3] << 24));
                    uint a = (uint)(after[bi + 0] | (after[bi + 1] << 8) | (after[bi + 2] << 16) | (after[bi + 3] << 24));
                    if (a == b) continue;

                    int si = rowStart + x * 4;
                    Add(si, b, a);
                }
            }
        }

        /// <summary>
        /// Undoes the pixel changes, restoring the "before" values.
        /// </summary>
        public void Undo()
        {
            var pixels = _layer.Surface.Pixels;
            for (int i = 0; i < _indices.Count; i++)
            {
                int idx = _indices[i];
                uint val = _before[i];
                WritePixel(pixels, idx, val);
            }

            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Undo pixel change on layer={Layer} desc={Desc} count={Count}", layerName, _description, _indices.Count);

            _layer.UpdatePreview();
        }

        /// <summary>
        /// Redoes the pixel changes, applying the "after" values.
        /// </summary>
        public void Redo()
        {
            var pixels = _layer.Surface.Pixels;
            for (int i = 0; i < _indices.Count; i++)
            {
                int idx = _indices[i];
                uint val = _after[i];
                WritePixel(pixels, idx, val);
            }

            var layerName = _layer.Name ?? "(layer)";
            LoggingService.Info("Redo pixel change on layer={Layer} desc={Desc} count={Count}", layerName, _description, _indices.Count);

            _layer.UpdatePreview();
        }

        private static void WritePixel(byte[] pixels, int idx, uint val)
        {
            pixels[idx + 0] = (byte)(val & 0xFF);
            pixels[idx + 1] = (byte)(val >> 8 & 0xFF);
            pixels[idx + 2] = (byte)(val >> 16 & 0xFF);
            pixels[idx + 3] = (byte)(val >> 24 & 0xFF);
        }

        /// <summary>
        /// Creates a pixel change item from a single region edit.
        /// </summary>
        public static PixelChangeItem FromRegion(
            RasterLayer layer,
            RectInt32 rect,
            byte[] before,
            byte[] after,
            string description = "Pixel Change",
            Icon historyIcon = Icon.History)
        {
            var item = new PixelChangeItem(layer, description, historyIcon);
            item.AppendRegionDelta(rect, before, after);

            var layerName = layer.Name ?? "(layer)";
            LoggingService.Info("Captured PixelChangeItem for layer={Layer} desc={Desc} count={Count}", layerName, description, item.IsEmpty ? 0 : item._indices.Count);

            return item;
        }

        /// <summary>
        /// Creates a pixel change item from multiple region edits.
        /// </summary>
        public static PixelChangeItem FromMultiRegion(
            RasterLayer layer,
            (RectInt32 rect, byte[] before, byte[] after)[] regions,
            string description = "Pixel Change",
            Icon historyIcon = Icon.History)
        {
            var item = new PixelChangeItem(layer, description, historyIcon);
            foreach (var (rect, before, after) in regions)
            {
                item.AppendRegionDelta(rect, before, after);
            }

            var layerName = layer.Name ?? "(layer)";
            LoggingService.Info("Captured PixelChangeItem for layer={Layer} desc={Desc} count={Count}", layerName, description, item.IsEmpty ? 0 : item._indices.Count);

            return item;
        }
    }
}
