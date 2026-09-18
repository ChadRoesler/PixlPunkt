using System;
using System.Collections.Generic;
using System.IO;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Tile;
using Windows.Graphics;

namespace PixlPunkt.Core.History
{
    /// <summary>Length-prefixed blob helpers shared by the selection history items.</summary>
    internal static class HistoryBlobs
    {
        public static void Write(BinaryWriter bw, byte[]? blob)
        {
            bw.Write(blob?.Length ?? -1);
            if (blob != null) bw.Write(blob);
        }

        public static byte[]? Read(BinaryReader br)
        {
            int len = br.ReadInt32();
            return len < 0 ? null : br.ReadBytes(len);
        }

        public static void WriteTiles(BinaryWriter bw, Dictionary<int, byte[]>? tiles)
        {
            bw.Write(tiles?.Count ?? -1);
            if (tiles == null) return;
            foreach (var (id, px) in tiles) { bw.Write(id); Write(bw, px); }
        }

        public static Dictionary<int, byte[]>? ReadTiles(BinaryReader br)
        {
            int n = br.ReadInt32();
            if (n < 0) return null;
            var d = new Dictionary<int, byte[]>(n);
            for (int i = 0; i < n; i++) { int id = br.ReadInt32(); d[id] = Read(br) ?? Array.Empty<byte>(); }
            return d;
        }
    }

    /// <summary>
    /// Shared machinery for the items that move pixels between a layer and a floating
    /// selection (lift, commit, discard). Each holds: a rectangle of layer pixels before/after,
    /// the tiles that rectangle touched before/after (tile-mapped layers), the selection mask
    /// before/after, and a snapshot of the floating selection. Subclasses decide which side of
    /// each pair undo and redo apply.
    /// </summary>
    public abstract class FloatingSelectionItemBase : OffloadableHistoryItemBase
    {
        protected readonly CanvasDocument Doc;
        protected readonly RasterLayer Layer;
        protected readonly RectInt32 Bounds;
        protected byte[] PixelsBefore;
        protected byte[] PixelsAfter;
        protected Dictionary<int, byte[]>? TilesBefore;
        protected Dictionary<int, byte[]>? TilesAfter;
        protected readonly byte[]? RegionBefore;   // RLE-encoded, null = unchanged
        protected readonly byte[]? RegionAfter;
        protected FloatingSelection? Floating;      // snapshot; pixels released when offloaded

        protected FloatingSelectionItemBase(
            CanvasDocument doc, RasterLayer layer, RectInt32 bounds,
            byte[] pixelsBefore, byte[] pixelsAfter,
            Dictionary<int, byte[]>? tilesBefore, Dictionary<int, byte[]>? tilesAfter,
            SelectionRegion? regionBefore, SelectionRegion? regionAfter,
            FloatingSelection? floating)
        {
            Doc = doc ?? throw new ArgumentNullException(nameof(doc));
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
            Bounds = bounds;
            PixelsBefore = pixelsBefore ?? Array.Empty<byte>();
            PixelsAfter = pixelsAfter ?? Array.Empty<byte>();
            TilesBefore = tilesBefore;
            TilesAfter = tilesAfter;
            RegionBefore = regionBefore == null ? null : SelectionRegionCodec.Encode(regionBefore);
            RegionAfter = regionAfter == null ? null : SelectionRegionCodec.Encode(regionAfter);
            Floating = floating;
        }

        // ── the three things every subclass restores ────────────────────

        protected void ApplyPixels(byte[] pixels, Dictionary<int, byte[]>? tiles)
        {
            if (pixels.Length > 0 && Bounds.Width > 0 && Bounds.Height > 0)
            {
                var surf = Layer.Surface;
                PixelRectOps.Blit(surf.Pixels, surf.Width, surf.Height, Bounds.X, Bounds.Y, pixels, Bounds.Width, Bounds.Height);
            }
            TileLayerPropagation.RestoreTileStates(Layer, Doc.TileSet, tiles);
            Layer.UpdatePreview();
        }

        protected void ApplyRegion(byte[]? encoded)
        {
            if (encoded == null) return;
            SelectionRegionCodec.Decode(encoded, Doc.Selection);
        }

        protected void SetFloating(bool present)
        {
            Doc.SetFloating(present && Floating != null ? Floating.Clone() : null);
        }

        protected void Finish()
        {
            Doc.CompositeTo(Doc.Surface);
            Doc.RaiseSelectionChanged();
            Doc.RaiseDocumentModified();
        }

        // ── memory budget ──────────────────────────────────────────────

        protected override long PayloadBytes
        {
            get
            {
                long total = PixelsBefore.Length + PixelsAfter.Length + (Floating?.Pixels.Length ?? 0) + (Floating?.Mask.Length ?? 0)
                             + (RegionBefore?.Length ?? 0) + (RegionAfter?.Length ?? 0);
                if (TilesBefore != null) foreach (var t in TilesBefore.Values) total += t.Length;
                if (TilesAfter != null) foreach (var t in TilesAfter.Values) total += t.Length;
                return total;
            }
        }

        protected override byte[]? SerializePayload()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            HistoryBlobs.Write(bw, PixelsBefore);
            HistoryBlobs.Write(bw, PixelsAfter);
            HistoryBlobs.WriteTiles(bw, TilesBefore);
            HistoryBlobs.WriteTiles(bw, TilesAfter);
            HistoryBlobs.Write(bw, Floating?.Pixels);
            HistoryBlobs.Write(bw, Floating?.Mask);
            return ms.ToArray();
        }

        protected override void DeserializePayload(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);
            PixelsBefore = HistoryBlobs.Read(br) ?? Array.Empty<byte>();
            PixelsAfter = HistoryBlobs.Read(br) ?? Array.Empty<byte>();
            TilesBefore = HistoryBlobs.ReadTiles(br);
            TilesAfter = HistoryBlobs.ReadTiles(br);
            var fp = HistoryBlobs.Read(br);
            var fm = HistoryBlobs.Read(br);
            if (Floating != null && fp != null) Floating.Pixels = fp;
            if (Floating != null && fm != null) Floating.Mask = fm;
        }

        protected override void ReleasePayload()
        {
            PixelsBefore = Array.Empty<byte>();
            PixelsAfter = Array.Empty<byte>();
            TilesBefore = null;
            TilesAfter = null;
            if (Floating != null)
            {
                Floating.Pixels = Array.Empty<byte>();
                Floating.Mask = Array.Empty<byte>();
            }
        }
    }

    /// <summary>
    /// Pixels lifted off a layer into a floating selection (or a paste, which lifts nothing).
    /// Undo puts the pixels back and drops the floating selection; redo clears them again and
    /// re-creates it. Because this is a real history item, "undo past the lift" no longer needs
    /// any special handling anywhere.
    /// </summary>
    public sealed class SelectionLiftItem : FloatingSelectionItemBase
    {
        public override FluentIcons.Common.Icon HistoryIcon { get; set; } = FluentIcons.Common.Icon.CopySelect;
        public override string Description { get; }

        public SelectionLiftItem(
            CanvasDocument doc, RasterLayer layer, RectInt32 bounds,
            byte[] pixelsBefore, byte[] pixelsAfter,
            Dictionary<int, byte[]>? tilesBefore, Dictionary<int, byte[]>? tilesAfter,
            SelectionRegion? regionBefore, SelectionRegion? regionAfter,
            FloatingSelection floating, string description = "Lift Selection")
            : base(doc, layer, bounds, pixelsBefore, pixelsAfter, tilesBefore, tilesAfter, regionBefore, regionAfter, floating)
        {
            Description = description;
        }

        public override void Undo()
        {
            ApplyPixels(PixelsBefore, TilesBefore);
            ApplyRegion(RegionBefore);
            SetFloating(false);
            Finish();
        }

        public override void Redo()
        {
            ApplyPixels(PixelsAfter, TilesAfter);
            ApplyRegion(RegionAfter);
            SetFloating(true);
            Finish();
        }
    }

    /// <summary>
    /// A floating selection rasterized back onto its layer. Undo restores the destination
    /// pixels and brings the floating selection back exactly as it was.
    /// </summary>
    public sealed class SelectionCommitItem : FloatingSelectionItemBase
    {
        public override FluentIcons.Common.Icon HistoryIcon { get; set; } = FluentIcons.Common.Icon.Checkmark;
        public override string Description => "Commit Selection";

        public SelectionCommitItem(
            CanvasDocument doc, RasterLayer layer, RectInt32 bounds,
            byte[] pixelsBefore, byte[] pixelsAfter,
            Dictionary<int, byte[]>? tilesBefore, Dictionary<int, byte[]>? tilesAfter,
            SelectionRegion regionBefore, SelectionRegion regionAfter,
            FloatingSelection floatingBefore)
            : base(doc, layer, bounds, pixelsBefore, pixelsAfter, tilesBefore, tilesAfter, regionBefore, regionAfter, floatingBefore)
        {
        }

        public override void Undo()
        {
            ApplyPixels(PixelsBefore, TilesBefore);
            ApplyRegion(RegionBefore);
            SetFloating(true);
            Finish();
        }

        public override void Redo()
        {
            ApplyPixels(PixelsAfter, TilesAfter);
            ApplyRegion(RegionAfter);
            SetFloating(false);
            Finish();
        }
    }

    /// <summary>
    /// A floating selection dropped without being committed: Escape (which puts the lifted pixels
    /// back where they came from) or Delete (which leaves the hole). Undo brings the floating
    /// selection back.
    /// </summary>
    public sealed class SelectionDiscardItem : FloatingSelectionItemBase
    {
        public override FluentIcons.Common.Icon HistoryIcon { get; set; } = FluentIcons.Common.Icon.SelectAllOff;
        public override string Description { get; }

        public SelectionDiscardItem(
            CanvasDocument doc, RasterLayer layer, RectInt32 bounds,
            byte[] pixelsBefore, byte[] pixelsAfter,
            Dictionary<int, byte[]>? tilesBefore, Dictionary<int, byte[]>? tilesAfter,
            SelectionRegion regionBefore, SelectionRegion regionAfter,
            FloatingSelection floatingBefore, string description)
            : base(doc, layer, bounds, pixelsBefore, pixelsAfter, tilesBefore, tilesAfter, regionBefore, regionAfter, floatingBefore)
        {
            Description = description;
        }

        public override void Undo()
        {
            ApplyPixels(PixelsBefore, TilesBefore);
            ApplyRegion(RegionBefore);
            SetFloating(true);
            Finish();
        }

        public override void Redo()
        {
            ApplyPixels(PixelsAfter, TilesAfter);
            ApplyRegion(RegionAfter);
            SetFloating(false);
            Finish();
        }
    }
}
