namespace PixlPunkt.PluginSdk.Tile
{
    /// <summary>
    /// Context interface providing host services to tile handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tile handlers receive this context at construction time to interact with the host
    /// application. It provides access to document properties, tile set management,
    /// tile mapping operations, and layer pixel access.
    /// </para>
    /// </remarks>
    public interface ITileContext
    {
        //////////////////////////////////////////////////////////////////
        // DOCUMENT GEOMETRY
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the document width in pixels.
        /// </summary>
        int DocumentWidth { get; }

        /// <summary>
        /// Gets the document height in pixels.
        /// </summary>
        int DocumentHeight { get; }

        /// <summary>
        /// Gets the tile width in pixels.
        /// </summary>
        int TileWidth { get; }

        /// <summary>
        /// Gets the tile height in pixels.
        /// </summary>
        int TileHeight { get; }

        /// <summary>
        /// Gets the number of tile columns in the document.
        /// </summary>
        int TileCountX { get; }

        /// <summary>
        /// Gets the number of tile rows in the document.
        /// </summary>
        int TileCountY { get; }

        //////////////////////////////////////////////////////////////////
        // COORDINATE CONVERSION
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts document pixel coordinates to tile grid coordinates.
        /// </summary>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <returns>Tile grid coordinates (tileX, tileY).</returns>
        (int tileX, int tileY) DocToTile(int docX, int docY);

        /// <summary>
        /// Converts tile grid coordinates to document pixel coordinates (top-left corner).
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        /// <returns>Document pixel coordinates (docX, docY) of the tile's top-left corner.</returns>
        (int docX, int docY) TileToDoc(int tileX, int tileY);

        /// <summary>
        /// Gets the tile rectangle in document pixel coordinates.
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        /// <returns>Rectangle (x, y, width, height) in document pixels.</returns>
        (int x, int y, int width, int height) GetTileRect(int tileX, int tileY);

        //////////////////////////////////////////////////////////////////
        // TILESET ACCESS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the currently selected tile ID in the tile panel.
        /// </summary>
        int SelectedTileId { get; }

        /// <summary>
        /// Gets the number of tiles in the tile set.
        /// </summary>
        int TileCount { get; }

        /// <summary>
        /// Gets all tile IDs in the tile set.
        /// </summary>
        /// <returns>Collection of tile IDs.</returns>
        System.Collections.Generic.IEnumerable<int> GetAllTileIds();

        /// <summary>
        /// Gets the tile ID at a specific index in the tile set.
        /// </summary>
        /// <param name="index">Zero-based index.</param>
        /// <returns>The tile ID, or -1 if index is out of range.</returns>
        int GetTileIdAtIndex(int index);

        /// <summary>
        /// Gets the pixel data for a tile.
        /// </summary>
        /// <param name="tileId">The tile ID.</param>
        /// <returns>BGRA pixel data (width * height * 4 bytes), or null if not found.</returns>
        byte[]? GetTilePixels(int tileId);

        /// <summary>
        /// Creates a new tile from pixel data.
        /// </summary>
        /// <param name="pixels">BGRA pixel data (must be TileWidth * TileHeight * 4 bytes).</param>
        /// <returns>The ID of the newly created tile.</returns>
        int CreateTile(byte[] pixels);

        /// <summary>
        /// Deletes a tile from the tile set.
        /// </summary>
        /// <param name="tileId">The tile ID to delete.</param>
        /// <returns>True if the tile was deleted; false if not found.</returns>
        bool DeleteTile(int tileId);

        /// <summary>
        /// Duplicates an existing tile.
        /// </summary>
        /// <param name="tileId">The tile ID to duplicate.</param>
        /// <returns>The ID of the new duplicate tile, or -1 if not found.</returns>
        int DuplicateTile(int tileId);

        /// <summary>
        /// Updates the pixel data of an existing tile.
        /// </summary>
        /// <param name="tileId">The tile ID to update.</param>
        /// <param name="pixels">New BGRA pixel data.</param>
        /// <returns>True if updated; false if tile not found.</returns>
        bool UpdateTilePixels(int tileId, byte[] pixels);

        //////////////////////////////////////////////////////////////////
        // TILE MAPPING (per-layer)
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the tile ID mapped at a grid position on the active layer.
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        /// <returns>The tile ID, or -1 if no tile is mapped at this position.</returns>
        int GetMappedTileId(int tileX, int tileY);

        /// <summary>
        /// Sets the tile mapping at a grid position on the active layer.
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        /// <param name="tileId">The tile ID to map, or -1 to clear the mapping.</param>
        void SetTileMapping(int tileX, int tileY, int tileId);

        /// <summary>
        /// Clears the tile mapping at a grid position on the active layer.
        /// </summary>
        /// <param name="tileX">Tile grid X coordinate.</param>
        /// <param name="tileY">Tile grid Y coordinate.</param>
        void ClearTileMapping(int tileX, int tileY);

        //////////////////////////////////////////////////////////////////
        // LAYER PIXEL ACCESS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the active layer's pixel data.
        /// </summary>
        /// <returns>BGRA pixel data for the entire layer.</returns>
        byte[] GetActiveLayerPixels();

        /// <summary>
        /// Reads pixels from a rectangular region of the active layer.
        /// </summary>
        /// <param name="x">X coordinate in document pixels.</param>
        /// <param name="y">Y coordinate in document pixels.</param>
        /// <param name="width">Width of the region.</param>
        /// <param name="height">Height of the region.</param>
        /// <returns>BGRA pixel data (width * height * 4 bytes).</returns>
        byte[] ReadLayerRect(int x, int y, int width, int height);

        /// <summary>
        /// Writes pixels to a rectangular region of the active layer.
        /// </summary>
        /// <param name="x">X coordinate in document pixels.</param>
        /// <param name="y">Y coordinate in document pixels.</param>
        /// <param name="width">Width of the region.</param>
        /// <param name="height">Height of the region.</param>
        /// <param name="pixels">BGRA pixel data (width * height * 4 bytes).</param>
        void WriteLayerRect(int x, int y, int width, int height, byte[] pixels);

        /// <summary>
        /// Blends pixels into a rectangular region of the active layer using alpha compositing.
        /// Unlike <see cref="WriteLayerRect"/>, this respects the alpha channel of source pixels.
        /// </summary>
        /// <param name="x">X coordinate in document pixels.</param>
        /// <param name="y">Y coordinate in document pixels.</param>
        /// <param name="width">Width of the region.</param>
        /// <param name="height">Height of the region.</param>
        /// <param name="pixels">BGRA pixel data (width * height * 4 bytes).</param>
        void BlendLayerRect(int x, int y, int width, int height, byte[] pixels);

        /// <summary>
        /// Blends pixels into a rectangular region and propagates changes to all mapped tiles.
        /// Use this for free-form tile stamping that should update tile definitions.
        /// </summary>
        /// <param name="x">X coordinate in document pixels.</param>
        /// <param name="y">Y coordinate in document pixels.</param>
        /// <param name="width">Width of the region.</param>
        /// <param name="height">Height of the region.</param>
        /// <param name="pixels">BGRA pixel data (width * height * 4 bytes).</param>
        void BlendAndPropagateTiles(int x, int y, int width, int height, byte[] pixels);

        /// <summary>
        /// Clears a rectangular region of the active layer to transparent.
        /// </summary>
        /// <param name="x">X coordinate in document pixels.</param>
        /// <param name="y">Y coordinate in document pixels.</param>
        /// <param name="width">Width of the region.</param>
        /// <param name="height">Height of the region.</param>
        void ClearLayerRect(int x, int y, int width, int height);

        //////////////////////////////////////////////////////////////////
        // TILE SAMPLING (RMB dropper for tiles)
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Samples the tile and mapping at a document position.
        /// </summary>
        /// <param name="docX">X position in document pixel coordinates.</param>
        /// <param name="docY">Y position in document pixel coordinates.</param>
        /// <returns>The tile ID and its mapping, or (-1, -1, -1) if no tile found.</returns>
        (int tileId, int tileX, int tileY) SampleTileAt(int docX, int docY);

        /// <summary>
        /// Sets the selected tile in the tile panel.
        /// </summary>
        /// <param name="tileId">The tile ID to select.</param>
        void SetSelectedTile(int tileId);

        //////////////////////////////////////////////////////////////////
        // HISTORY & REFRESH
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Begins a history transaction for undo/redo.
        /// </summary>
        /// <param name="description">Description of the operation.</param>
        void BeginHistoryTransaction(string description);

        /// <summary>
        /// Commits the current history transaction.
        /// </summary>
        void CommitHistoryTransaction();

        /// <summary>
        /// Cancels the current history transaction without recording changes.
        /// </summary>
        void CancelHistoryTransaction();

        /// <summary>
        /// Requests a canvas redraw.
        /// </summary>
        void Invalidate();

        /// <summary>
        /// Captures pointer for drag operations.
        /// </summary>
        void CapturePointer();

        /// <summary>
        /// Releases the captured pointer.
        /// </summary>
        void ReleasePointer();
    }

    /// <summary>
    /// Extended tile context interface that provides animation state access.
    /// </summary>
    /// <remarks>
    /// This interface is defined here but implemented by core components that need animation access.
    /// The actual TileAnimationState type is in Core.Animation and cannot be referenced from the SDK.
    /// Use the object-based method and cast in the implementation.
    /// </remarks>
    public interface ITileAnimationContext : ITileContext
    {
        /// <summary>
        /// Gets the tile animation state for the current document as an object.
        /// </summary>
        /// <returns>The animation state object, or null if no document is open.</returns>
        /// <remarks>
        /// Returns object type to avoid SDK-to-Core dependency. The implementation returns
        /// a TileAnimationState instance which should be cast by the caller.
        /// </remarks>
        object? GetAnimationStateObject();
    }
}
