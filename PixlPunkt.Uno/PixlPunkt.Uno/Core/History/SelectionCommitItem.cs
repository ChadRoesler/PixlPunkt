using System;
using FluentIcons.Common;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Selection;
using Windows.Graphics;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// History item for committing a floating selection to the canvas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This item captures both the pixel changes (for canvas restoration) and the
    /// floating selection state (for restoring the selection with marquee on undo).
    /// </para>
    /// <para>
    /// On <strong>Undo</strong>: Restores the canvas pixels AND restores the floating selection state.
    /// On <strong>Redo</strong>: Commits the selection again (applies pixel changes, clears floating state).
    /// </para>
    /// </remarks>
    public sealed class SelectionCommitItem : IHistoryItem
    {
        /// <summary>
        /// Snapshot of the floating selection state before commit.
        /// </summary>
        public readonly struct FloatingSnapshot
        {
            public readonly byte[] Buffer;
            public readonly int BufferWidth;
            public readonly int BufferHeight;
            public readonly int FloatX;
            public readonly int FloatY;
            public readonly int OrigW;
            public readonly int OrigH;
            public readonly int OrigCenterX;
            public readonly int OrigCenterY;
            public readonly double ScaleX;
            public readonly double ScaleY;
            public readonly double AngleDeg;
            public readonly double CumulativeAngleDeg;
            public readonly SelectionRegion Region;

            public FloatingSnapshot(
                byte[] buffer, int bufferWidth, int bufferHeight,
                int floatX, int floatY,
                int origW, int origH,
                int origCenterX, int origCenterY,
                double scaleX, double scaleY,
                double angleDeg, double cumulativeAngleDeg,
                SelectionRegion region)
            {
                Buffer = buffer;
                BufferWidth = bufferWidth;
                BufferHeight = bufferHeight;
                FloatX = floatX;
                FloatY = floatY;
                OrigW = origW;
                OrigH = origH;
                OrigCenterX = origCenterX;
                OrigCenterY = origCenterY;
                ScaleX = scaleX;
                ScaleY = scaleY;
                AngleDeg = angleDeg;
                CumulativeAngleDeg = cumulativeAngleDeg;
                Region = region;
            }
        }

        private readonly RasterLayer _layer;
        private readonly RectInt32 _pixelBounds;
        private readonly byte[] _pixelsBefore;
        private readonly byte[] _pixelsAfter;
        private readonly FloatingSnapshot _floatingBefore;
        private readonly SelectionRegion _regionAfter;
        private readonly Action<FloatingSnapshot> _restoreFloating;
        private readonly Action _clearFloating;
        private readonly Action<RasterLayer, RectInt32, byte[]> _applyPixels;

        /// <inheritdoc/>
        public Icon HistoryIcon { get; set; } = Icon.Checkmark;

        /// <inheritdoc/>
        public string Description => "Commit Selection";

        /// <summary>
        /// Creates a new selection commit history item.
        /// </summary>
        /// <param name="layer">The layer being modified.</param>
        /// <param name="pixelBounds">The bounds of the pixel region affected.</param>
        /// <param name="pixelsBefore">Pixel data before the commit.</param>
        /// <param name="pixelsAfter">Pixel data after the commit.</param>
        /// <param name="floatingBefore">The floating selection state before commit.</param>
        /// <param name="regionAfter">The selection region after commit (may be empty).</param>
        /// <param name="restoreFloating">Callback to restore floating selection state.</param>
        /// <param name="clearFloating">Callback to clear floating selection state.</param>
        /// <param name="applyPixels">Callback to apply pixel data to layer.</param>
        public SelectionCommitItem(
            RasterLayer layer,
            RectInt32 pixelBounds,
            byte[] pixelsBefore,
            byte[] pixelsAfter,
            FloatingSnapshot floatingBefore,
            SelectionRegion regionAfter,
            Action<FloatingSnapshot> restoreFloating,
            Action clearFloating,
            Action<RasterLayer, RectInt32, byte[]> applyPixels)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            _pixelBounds = pixelBounds;
            _pixelsBefore = pixelsBefore ?? throw new ArgumentNullException(nameof(pixelsBefore));
            _pixelsAfter = pixelsAfter ?? throw new ArgumentNullException(nameof(pixelsAfter));
            _floatingBefore = floatingBefore;
            _regionAfter = regionAfter?.Clone() ?? new SelectionRegion();
            _restoreFloating = restoreFloating ?? throw new ArgumentNullException(nameof(restoreFloating));
            _clearFloating = clearFloating ?? throw new ArgumentNullException(nameof(clearFloating));
            _applyPixels = applyPixels ?? throw new ArgumentNullException(nameof(applyPixels));
        }

        /// <inheritdoc/>
        public void Undo()
        {
            // Restore pixels to before state
            _applyPixels(_layer, _pixelBounds, _pixelsBefore);

            // Restore floating selection state (brings back marquee)
            _restoreFloating(_floatingBefore);

            LoggingService.Info("Undo commit selection");
        }

        /// <inheritdoc/>
        public void Redo()
        {
            // Apply the committed pixels
            _applyPixels(_layer, _pixelBounds, _pixelsAfter);

            // Clear floating state
            _clearFloating();

            LoggingService.Info("Redo commit selection");
        }
    }
}
