namespace PixlPunkt.PluginSdk.Plugins
{
    /// <summary>
    /// Provides plugins with read-only access to the active canvas/document state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ICanvasContext"/> allows plugin tools to:
    /// </para>
    /// <list type="bullet">
    /// <item>Sample pixel colors from the active layer or composite</item>
    /// <item>Query document dimensions</item>
    /// <item>Access current foreground/background colors</item>
    /// <item>Check selection state and bounds</item>
    /// </list>
    /// <para>
    /// <strong>Thread Safety:</strong>
    /// Canvas context methods should only be called from the UI thread during tool operations.
    /// The pixel data may change between calls if the user performs other operations.
    /// </para>
    /// </remarks>
    public interface ICanvasContext
    {
        //////////////////////////////////////////////////////////////////
        // DOCUMENT INFO
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the width of the active document in pixels.
        /// </summary>
        int DocumentWidth { get; }

        /// <summary>
        /// Gets the height of the active document in pixels.
        /// </summary>
        int DocumentHeight { get; }

        /// <summary>
        /// Gets the number of layers in the active document.
        /// </summary>
        int LayerCount { get; }

        /// <summary>
        /// Gets the index of the currently active layer (0-based), or -1 if none.
        /// </summary>
        int ActiveLayerIndex { get; }

        //////////////////////////////////////////////////////////////////
        // COLORS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current foreground color in BGRA format (0xAARRGGBB).
        /// </summary>
        uint ForegroundColor { get; }

        /// <summary>
        /// Gets the current background color in BGRA format (0xAARRGGBB).
        /// </summary>
        uint BackgroundColor { get; }

        //////////////////////////////////////////////////////////////////
        // PIXEL SAMPLING
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Samples a pixel color from the active layer at the given coordinates.
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>The pixel color in BGRA format, or <c>null</c> if out of bounds.</returns>
        uint? SampleActiveLayer(int x, int y);

        /// <summary>
        /// Samples a pixel color from the composited (flattened) view.
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>The composited pixel color in BGRA format, or <c>null</c> if out of bounds.</returns>
        uint? SampleComposite(int x, int y);

        /// <summary>
        /// Samples a pixel color from a specific layer at the given coordinates.
        /// </summary>
        /// <param name="layerIndex">The layer index (0-based).</param>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>The pixel color in BGRA format, or <c>null</c> if out of bounds or invalid layer.</returns>
        uint? SampleLayer(int layerIndex, int x, int y);

        //////////////////////////////////////////////////////////////////
        // SELECTION
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets whether there is an active selection.
        /// </summary>
        bool HasSelection { get; }

        /// <summary>
        /// Gets the bounding box of the current selection.
        /// </summary>
        /// <returns>A tuple of (X, Y, Width, Height), or <c>null</c> if no selection.</returns>
        (int X, int Y, int Width, int Height)? SelectionBounds { get; }

        /// <summary>
        /// Checks if a point is within the current selection mask.
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns><c>true</c> if the point is selected; otherwise <c>false</c>.</returns>
        bool IsPointSelected(int x, int y);

        /// <summary>
        /// Gets the selection mask value at a point (0-255 for feathered selections).
        /// </summary>
        /// <param name="x">X coordinate in document space.</param>
        /// <param name="y">Y coordinate in document space.</param>
        /// <returns>Selection intensity (0 = not selected, 255 = fully selected).</returns>
        byte GetSelectionMask(int x, int y);
    }
}
