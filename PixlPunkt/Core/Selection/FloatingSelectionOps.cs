using System;
using System.Collections.Generic;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Tile;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Core.Selection
{
    /// <summary>
    /// The floating-selection lifecycle on a document: lift, commit, cancel, discard, paste.
    /// Each operation mutates the document and returns the history item that undoes it; the
    /// caller pushes the item. Nothing here touches the view, so history, save and tests all use
    /// the same code the UI does.
    /// </summary>
    public static class FloatingSelectionOps
    {
        /// <summary>
        /// Lifts the selected pixels of <paramref name="layer"/> into a floating selection, clearing
        /// them on the layer. Returns null when there is nothing to lift.
        /// </summary>
        public static SelectionLiftItem? Lift(CanvasDocument doc, RasterLayer layer)
        {
            if (doc.Floating != null) return null;

            var region = doc.Selection;
            region.EnsureSize(doc.PixelWidth, doc.PixelHeight);
            if (region.IsEmpty) return null;

            var surf = layer.Surface;
            int sw = surf.Width, sh = surf.Height;
            var bounds = ClampToSurface(region.Bounds, sw, sh);
            if (bounds.Width == 0 || bounds.Height == 0) return null;

            bool nonRect = !SelectionRegionBuilders.IsRectangular(region, bounds);
            var tilesBefore = TileLayerPropagation.CaptureAffectedTiles(layer, doc.TileSet, bounds);

            int bw = bounds.Width, bh = bounds.Height;
            var before = PixelRectOps.CopyRect(surf.Pixels, sw, sh, bounds);
            var after = (byte[])before.Clone();
            var lifted = new byte[bw * bh * 4];
            var mask = new byte[bw * bh];
            int boxStride = bw * 4;

            for (int y = 0; y < bh; y++)
            {
                int sy = bounds.Y + y;
                int row = y * boxStride;
                for (int x = 0; x < bw; x++)
                {
                    if (!region.Contains(bounds.X + x, sy)) continue;
                    mask[y * bw + x] = 1;
                    int i = row + x * 4;
                    lifted[i] = before[i]; lifted[i + 1] = before[i + 1]; lifted[i + 2] = before[i + 2]; lifted[i + 3] = before[i + 3];
                    after[i] = 0; after[i + 1] = 0; after[i + 2] = 0; after[i + 3] = 0;
                }
            }

            PixelRectOps.Blit(surf.Pixels, sw, sh, bounds.X, bounds.Y, after, bw, bh);

            Dictionary<int, byte[]>? tilesAfter = null;
            if (tilesBefore != null)
            {
                TileLayerPropagation.PropagateFromLayer(layer, doc.TileSet, bounds);
                tilesAfter = TileLayerPropagation.CaptureTileStates(doc.TileSet!, tilesBefore.Keys);
            }

            var floating = new FloatingSelection(layer, lifted, bw, bh, bounds.X, bounds.Y, bounds, before, mask)
            {
                RegionNonRectangular = nonRect
            };
            doc.SetFloating(floating);
            layer.UpdatePreview();
            doc.CompositeTo(doc.Surface);

            return new SelectionLiftItem(doc, layer, bounds, before, after, tilesBefore, tilesAfter, null, null, floating.Clone());
        }

        /// <summary>
        /// Creates a floating selection from pasted pixels. Nothing is lifted from the layer; the
        /// returned item's undo simply removes the floating selection and restores the old mask.
        /// </summary>
        public static SelectionLiftItem Paste(CanvasDocument doc, RasterLayer layer, byte[] pixels, int w, int h, int x, int y)
        {
            var regionBefore = doc.Selection.Clone();
            doc.Selection.EnsureSize(doc.PixelWidth, doc.PixelHeight);
            doc.Selection.Clear();
            doc.Selection.AddRect(CreateRect(x, y, w, h));

            var floating = new FloatingSelection(layer, (byte[])pixels.Clone(), w, h, x, y, CreateRect(0, 0, 0, 0), Array.Empty<byte>());
            doc.SetFloating(floating);

            return new SelectionLiftItem(doc, layer, CreateRect(0, 0, 0, 0), Array.Empty<byte>(), Array.Empty<byte>(),
                null, null, regionBefore, doc.Selection.Clone(), floating.Clone(), "Paste");
        }

        /// <summary>
        /// Rasterizes the floating selection (scale, then rotation) onto its layer and rebuilds the
        /// selection mask as the transformed marquee. Returns null when nothing is floating.
        /// </summary>
        public static SelectionCommitItem? Commit(CanvasDocument doc)
        {
            var f = doc.Floating;
            if (f == null) return null;

            var layer = f.Layer;
            var surf = layer.Surface;
            int sw = surf.Width, sh = surf.Height;
            var regionBefore = doc.Selection.Clone();
            var floatingBefore = f.Clone();

            var (rotBuf, rotW, rotH, baseW, baseH, totalRotation) = Rasterize(f);
            int cx = f.OrigCenterX;
            int cy = f.OrigCenterY;
            var dstRect = CreateRect(cx - rotW / 2, cy - rotH / 2, rotW, rotH);
            var dstClamp = ClampToSurface(dstRect, sw, sh);

            if (dstClamp.Width == 0 || dstClamp.Height == 0)
            {
                // Landed entirely off-canvas: the pixels are gone, the selection is gone.
                doc.Selection.Clear();
                doc.SetFloating(null);
                doc.RaiseSelectionChanged();
                return new SelectionCommitItem(doc, layer, dstClamp, Array.Empty<byte>(), Array.Empty<byte>(),
                    null, null, regionBefore, doc.Selection.Clone(), floatingBefore);
            }

            var tilesBefore = TileLayerPropagation.CaptureAffectedTiles(layer, doc.TileSet, dstClamp);
            var before = PixelRectOps.CopyRect(surf.Pixels, sw, sh, dstClamp);

            PixelRectOps.BlitAlphaOver(surf.Pixels, sw, sh, dstRect.X, dstRect.Y, rotBuf, rotW, rotH);
            SelectionRegionBuilders.RebuildAsRotatedRect(doc.Selection, cx, cy, baseW, baseH, totalRotation, dstClamp, sw, sh);

            var after = PixelRectOps.CopyRect(surf.Pixels, sw, sh, dstClamp);

            Dictionary<int, byte[]>? tilesAfter = null;
            if (tilesBefore != null)
            {
                TileLayerPropagation.PropagateFromLayer(layer, doc.TileSet, dstClamp);
                tilesAfter = TileLayerPropagation.CaptureTileStates(doc.TileSet!, tilesBefore.Keys);
            }

            doc.SetFloating(null);
            layer.UpdatePreview();
            doc.CompositeTo(doc.Surface);
            doc.RaiseSelectionChanged();

            return new SelectionCommitItem(doc, layer, dstClamp, before, after, tilesBefore, tilesAfter, regionBefore, doc.Selection.Clone(), floatingBefore);
        }

        /// <summary>
        /// Cancels the floating selection: the lifted pixels go back where they came from
        /// (untransformed) and the selection is cleared. Returns null when nothing is floating.
        /// </summary>
        public static SelectionDiscardItem? Cancel(CanvasDocument doc) => Drop(doc, restoreSource: true, "Cancel Selection");

        /// <summary>
        /// Deletes the floating selection: the pixels are discarded and the hole they left stays.
        /// Returns null when nothing is floating.
        /// </summary>
        public static SelectionDiscardItem? Discard(CanvasDocument doc) => Drop(doc, restoreSource: false, "Delete Selection");

        private static SelectionDiscardItem? Drop(CanvasDocument doc, bool restoreSource, string description)
        {
            var f = doc.Floating;
            if (f == null) return null;

            var layer = f.Layer;
            var surf = layer.Surface;
            int sw = surf.Width, sh = surf.Height;
            var regionBefore = doc.Selection.Clone();
            var snapshot = f.Clone();

            var bounds = CreateRect(0, 0, 0, 0);
            byte[] before = Array.Empty<byte>(), after = Array.Empty<byte>();
            Dictionary<int, byte[]>? tilesBefore = null, tilesAfter = null;

            if (restoreSource && f.HasSource)
            {
                bounds = f.SourceBounds;
                tilesBefore = TileLayerPropagation.CaptureAffectedTiles(layer, doc.TileSet, bounds);
                before = PixelRectOps.CopyRect(surf.Pixels, sw, sh, bounds);
                PixelRectOps.Blit(surf.Pixels, sw, sh, bounds.X, bounds.Y, f.SourcePixelsBefore, bounds.Width, bounds.Height);
                after = PixelRectOps.CopyRect(surf.Pixels, sw, sh, bounds);
                if (tilesBefore != null)
                {
                    TileLayerPropagation.PropagateFromLayer(layer, doc.TileSet, bounds);
                    tilesAfter = TileLayerPropagation.CaptureTileStates(doc.TileSet!, tilesBefore.Keys);
                }
                layer.UpdatePreview();
            }

            doc.Selection.Clear();
            doc.SetFloating(null);
            doc.CompositeTo(doc.Surface);
            doc.RaiseSelectionChanged();

            return new SelectionDiscardItem(doc, layer, bounds, before, after, tilesBefore, tilesAfter, regionBefore, doc.Selection.Clone(), snapshot, description);
        }

        /// <summary>
        /// Draws the floating selection onto a <em>copy</em> of layer pixels, for serialization.
        /// The document is not modified, so a save (or autosave) while floating writes what the
        /// user sees without committing anything.
        /// </summary>
        public static byte[] RasterizeOnto(byte[] layerPixels, int layerW, int layerH, FloatingSelection f)
        {
            var copy = (byte[])layerPixels.Clone();
            var (rotBuf, rotW, rotH, baseW, baseH, _) = Rasterize(f);
            int cx = f.OrigCenterX;
            int cy = f.OrigCenterY;
            PixelRectOps.BlitAlphaOver(copy, layerW, layerH, cx - rotW / 2, cy - rotH / 2, rotBuf, rotW, rotH);
            return copy;
        }

        private static (byte[] buf, int w, int h, int baseW, int baseH, double totalRotation) Rasterize(FloatingSelection f)
        {
            var (scaled, scaledW, scaledH) = SelectionBufferOps.BuildScaled(f.Pixels, f.Width, f.Height, f.ScaleX, f.ScaleY, f.ScaleFilter);
            double totalRotation = f.CumulativeAngleDeg + f.AngleDeg;
            var (rotated, rotW, rotH) = SelectionBufferOps.BuildRotated(scaled, scaledW, scaledH, totalRotation, f.RotMode);
            return (rotated, rotW, rotH, scaledW, scaledH, totalRotation);
        }

        private static RectInt32 ClampToSurface(RectInt32 r, int w, int h)
        {
            int x0 = Math.Clamp(r.X, 0, w), y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w), y1 = Math.Clamp(r.Y + r.Height, 0, h);
            return CreateRect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
        }
    }
}
