using System;
using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using Windows.Graphics;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for canvas resize operations, storing complete layer snapshots
    /// to enable full undo/redo of dimension changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Canvas resize is a destructive operation that changes document dimensions and
    /// may crop or pad all layer content. This item captures the complete state
    /// (dimensions + all layer pixels) before and after the resize.
    /// </para>
    /// <para>
    /// While this uses more memory than delta-based history, resize operations are
    /// relatively rare and the simplicity of full snapshots ensures correctness.
    /// </para>
    /// </remarks>
    public sealed class CanvasResizeItem : IHistoryItem
    {
        private readonly CanvasDocument _document;

        // Before state
        private readonly int _beforeWidth;
        private readonly int _beforeHeight;
        private readonly SizeInt32 _beforeTileCounts;
        private readonly List<LayerSnapshot> _beforeSnapshots;

        // After state (captured after resize completes)
        private int _afterWidth;
        private int _afterHeight;
        private SizeInt32 _afterTileCounts;
        private List<LayerSnapshot>? _afterSnapshots;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => $"Resize Canvas ({_beforeWidth}×{_beforeHeight} → {_afterWidth}x{_afterHeight})";

        /// <summary>
        /// Snapshot of a single layer's pixel data.
        /// </summary>
        private sealed class LayerSnapshot
        {
            public RasterLayer Layer { get; }
            public int Width { get; }
            public int Height { get; }
            public byte[] Pixels { get; }

            public LayerSnapshot(RasterLayer layer)
            {
                Layer = layer;
                Width = layer.Surface.Width;
                Height = layer.Surface.Height;
                Pixels = new byte[layer.Surface.Pixels.Length];
                Buffer.BlockCopy(layer.Surface.Pixels, 0, Pixels, 0, Pixels.Length);
            }
        }

        /// <summary>
        /// Gets or sets the icon representing this history item.
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.Resize;

        /// <summary>
        /// Creates a new canvas resize item, capturing the BEFORE state.
        /// Call <see cref="CaptureAfterState"/> after the resize completes.
        /// </summary>
        /// <param name="document">The document being resized.</param>
        public CanvasResizeItem(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            // Capture before state immediately
            _beforeWidth = document.PixelWidth;
            _beforeHeight = document.PixelHeight;
            _beforeTileCounts = document.TileCounts;
            _beforeSnapshots = CaptureAllLayers();
        }

        /// <summary>
        /// Captures the AFTER state. Must be called after the resize operation completes.
        /// </summary>
        public void CaptureAfterState()
        {
            _afterWidth = _document.PixelWidth;
            _afterHeight = _document.PixelHeight;
            _afterTileCounts = _document.TileCounts;
            _afterSnapshots = CaptureAllLayers();
        }

        private List<LayerSnapshot> CaptureAllLayers()
        {
            var snapshots = new List<LayerSnapshot>();
            foreach (var layer in _document.GetAllRasterLayers())
            {
                snapshots.Add(new LayerSnapshot(layer));
            }
            return snapshots;
        }

        /// <summary>
        /// Undoes the resize, restoring the document to its previous dimensions and layer content.
        /// </summary>
        public void Undo()
        {
            RestoreState(_beforeWidth, _beforeHeight, _beforeTileCounts, _beforeSnapshots);
        }

        /// <summary>
        /// Redoes the resize, re-applying the dimension change and content.
        /// </summary>
        public void Redo()
        {
            if (_afterSnapshots == null)
                throw new InvalidOperationException("CaptureAfterState must be called before Redo.");

            RestoreState(_afterWidth, _afterHeight, _afterTileCounts, _afterSnapshots);
        }

        private void RestoreState(int width, int height, SizeInt32 tileCounts, List<LayerSnapshot> snapshots)
        {
            _document.RaiseBeforeStructureChanged();

            // Restore document dimensions
            _document.RestoreDimensions(width, height, tileCounts);

            // Restore each layer's pixels
            foreach (var snapshot in snapshots)
            {
                if (snapshot.Layer?.Surface != null)
                {
                    snapshot.Layer.Surface.Resize(snapshot.Width, snapshot.Height, snapshot.Pixels);
                    snapshot.Layer.UpdatePreview();
                }
            }

            // Restore composite surface
            _document.Surface.Resize(width, height, null);
            _document.Surface.Clear(0x00000000);
            _document.CompositeTo(_document.Surface);

            _document.RaiseStructureChanged();
        }
    }
}
