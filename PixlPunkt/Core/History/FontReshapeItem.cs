using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using Windows.Graphics;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// One reshape of a font: a change to the em box, the drawing room or the character set.
    /// </summary>
    /// <remarks>
    /// It carries whole-sheet snapshots on both sides rather than a description of what moved,
    /// because a reshape can crop ink and drop characters. Nothing else would bring those back, and
    /// a font sheet is small enough that copying it is cheaper than being clever.
    /// </remarks>
    public sealed class FontReshapeItem : IHistoryItem, IStructuralHistoryItem
    {
        private readonly CanvasDocument _document;
        private readonly Snapshot _before;
        private Snapshot? _after;

        public Icon HistoryIcon { get; set; } = Icon.TextFont;
        public string Description { get; }

        /// <summary>Everything a reshape changes, captured at one instant.</summary>
        private sealed class Snapshot
        {
            public int Width;
            public int Height;
            public SizeInt32 TileSize;
            public SizeInt32 TileCounts;
            public FontDocumentState Font = new();
            public List<(RasterLayer Layer, int Width, int Height, byte[] Pixels)> Layers = new();

            public static Snapshot Take(CanvasDocument doc)
            {
                var snapshot = new Snapshot
                {
                    Width = doc.PixelWidth,
                    Height = doc.PixelHeight,
                    TileSize = doc.TileSize,
                    TileCounts = doc.TileCounts,
                    Font = doc.FontState.Clone(),
                };

                foreach (var layer in doc.GetAllRasterLayers())
                {
                    var pixels = new byte[layer.Surface.Pixels.Length];
                    Buffer.BlockCopy(layer.Surface.Pixels, 0, pixels, 0, pixels.Length);
                    snapshot.Layers.Add((layer, layer.Surface.Width, layer.Surface.Height, pixels));
                }

                return snapshot;
            }

            public void Restore(CanvasDocument doc)
            {
                doc.RaiseBeforeStructureChanged();

                foreach (var (layer, width, height, pixels) in Layers)
                {
                    var copy = new byte[pixels.Length];
                    Buffer.BlockCopy(pixels, 0, copy, 0, pixels.Length);
                    layer.Surface.Resize(width, height, copy);
                }

                doc.RestoreDimensions(Width, Height, TileCounts);
                doc.SetTileSize(TileSize);
                doc.FontState.CopyFrom(Font);

                doc.Surface.Resize(Width, Height, null);
                doc.CompositeTo(doc.Surface);

                doc.RaiseStructureChanged();
                doc.RaiseFontChanged();
            }
        }

        /// <summary>Captures the state before the reshape. Call <see cref="CaptureAfter"/> once done.</summary>
        public FontReshapeItem(CanvasDocument document, string description)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _before = Snapshot.Take(document);
            Description = description;
        }

        /// <summary>Records the result, which is what redo puts back.</summary>
        public void CaptureAfter() => _after = Snapshot.Take(_document);

        public void Undo() => _before.Restore(_document);

        public void Redo() => _after?.Restore(_document);
    }
}
