using System;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Imaging;
// Implement SDK interface
using SdkICanvasContext = PixlPunkt.PluginSdk.Plugins.ICanvasContext;

namespace PixlPunkt.Core.Plugins
{
    /// <summary>
    /// Default implementation of <see cref="SdkICanvasContext"/> that wraps a <see cref="CanvasDocument"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides plugins with read-only access to document data through the
    /// SDK's ICanvasContext interface. It's created on-demand by the canvas host
    /// and provided to plugins via IPluginContext.Canvas.
    /// </para>
    /// <para>
    /// <strong>Thread Safety:</strong>
    /// This class should only be accessed from the UI thread. The underlying document
    /// data may change at any time due to user actions.
    /// </para>
    /// </remarks>
    public sealed class CanvasContext : SdkICanvasContext
    {
        private readonly CanvasDocument _document;
        private readonly Func<uint> _getForeground;
        private readonly Func<uint> _getBackground;
        private readonly Func<bool> _hasSelection;
        private readonly Func<(int X, int Y, int Width, int Height)?> _getSelectionBounds;
        private readonly Func<int, int, bool> _isPointSelected;
        private readonly Func<int, int, byte> _getSelectionMask;

        /// <summary>
        /// Creates a new canvas context wrapping the specified document.
        /// </summary>
        /// <param name="document">The document to wrap.</param>
        /// <param name="getForeground">Function to get the current foreground color.</param>
        /// <param name="getBackground">Function to get the current background color.</param>
        /// <param name="hasSelection">Function to check if there's an active selection.</param>
        /// <param name="getSelectionBounds">Function to get selection bounds.</param>
        /// <param name="isPointSelected">Function to check if a point is selected.</param>
        /// <param name="getSelectionMask">Function to get the selection mask value.</param>
        public CanvasContext(
            CanvasDocument document,
            Func<uint> getForeground,
            Func<uint> getBackground,
            Func<bool> hasSelection,
            Func<(int X, int Y, int Width, int Height)?> getSelectionBounds,
            Func<int, int, bool> isPointSelected,
            Func<int, int, byte> getSelectionMask)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _getForeground = getForeground ?? throw new ArgumentNullException(nameof(getForeground));
            _getBackground = getBackground ?? throw new ArgumentNullException(nameof(getBackground));
            _hasSelection = hasSelection ?? throw new ArgumentNullException(nameof(hasSelection));
            _getSelectionBounds = getSelectionBounds ?? throw new ArgumentNullException(nameof(getSelectionBounds));
            _isPointSelected = isPointSelected ?? throw new ArgumentNullException(nameof(isPointSelected));
            _getSelectionMask = getSelectionMask ?? throw new ArgumentNullException(nameof(getSelectionMask));
        }

        //////////////////////////////////////////////////////////////////
        // DOCUMENT INFO
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        public int DocumentWidth => _document.PixelWidth;

        /// <inheritdoc/>
        public int DocumentHeight => _document.PixelHeight;

        /// <inheritdoc/>
        public int LayerCount => _document.Layers.Count;

        /// <inheritdoc/>
        public int ActiveLayerIndex => _document.ActiveLayerIndex;

        //////////////////////////////////////////////////////////////////
        // COLORS
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        public uint ForegroundColor => _getForeground();

        /// <inheritdoc/>
        public uint BackgroundColor => _getBackground();

        //////////////////////////////////////////////////////////////////
        // PIXEL SAMPLING
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        public uint? SampleActiveLayer(int x, int y)
        {
            if (x < 0 || x >= _document.PixelWidth || y < 0 || y >= _document.PixelHeight)
                return null;

            var activeLayer = _document.ActiveLayer;
            if (activeLayer == null)
                return null;

            return SamplePixelSurface(activeLayer.Surface, x, y);
        }

        /// <inheritdoc/>
        public uint? SampleComposite(int x, int y)
        {
            if (x < 0 || x >= _document.PixelWidth || y < 0 || y >= _document.PixelHeight)
                return null;

            return SamplePixelSurface(_document.Surface, x, y);
        }

        /// <inheritdoc/>
        public uint? SampleLayer(int layerIndex, int x, int y)
        {
            if (x < 0 || x >= _document.PixelWidth || y < 0 || y >= _document.PixelHeight)
                return null;

            if (layerIndex < 0 || layerIndex >= _document.Layers.Count)
                return null;

            var layer = _document.Layers[layerIndex];
            return SamplePixelSurface(layer.Surface, x, y);
        }

        /// <summary>
        /// Samples a pixel from a PixelSurface and returns BGRA as a uint.
        /// </summary>
        private static uint? SamplePixelSurface(PixelSurface surface, int x, int y)
        {
            if (surface == null)
                return null;

            int idx = (y * surface.Width + x) * 4;
            if (idx < 0 || idx + 3 >= surface.Pixels.Length)
                return null;

            byte b = surface.Pixels[idx];
            byte g = surface.Pixels[idx + 1];
            byte r = surface.Pixels[idx + 2];
            byte a = surface.Pixels[idx + 3];
            return (uint)((a << 24) | (r << 16) | (g << 8) | b);
        }

        //////////////////////////////////////////////////////////////////
        // SELECTION
        //////////////////////////////////////////////////////////////////

        /// <inheritdoc/>
        public bool HasSelection => _hasSelection();

        /// <inheritdoc/>
        public (int X, int Y, int Width, int Height)? SelectionBounds => _getSelectionBounds();

        /// <inheritdoc/>
        public bool IsPointSelected(int x, int y) => _isPointSelected(x, y);

        /// <inheritdoc/>
        public byte GetSelectionMask(int x, int y) => _getSelectionMask(x, y);
    }
}
