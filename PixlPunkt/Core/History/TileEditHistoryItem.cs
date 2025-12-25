using System;
using FluentIcons.Common;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Tile;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for direct tile pixel edits (e.g., from the Tile Tessellation window).
    /// </summary>
    /// <remarks>
    /// <para>
    /// TileEditHistoryItem tracks before/after pixel states for a single tile definition.
    /// Unlike TileStampHistoryItem which handles tile placement on the canvas, this handles
    /// edits to the tile definition itself.
    /// </para>
    /// <para>
    /// On undo/redo, this updates the TileSet which fires TileUpdated events, causing
    /// any UI bound to the tile (TilePanel, main canvas mapped tiles) to refresh.
    /// </para>
    /// </remarks>
    public sealed class TileEditHistoryItem : IHistoryItem
    {
        private readonly TileSet _tileSet;
        private readonly int _tileId;
        private readonly byte[] _pixelsBefore;
        private readonly byte[] _pixelsAfter;
        private readonly string _description;

        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.TableCellEdit;

        /// <inheritdoc/>
        public string Description => _description;

        /// <summary>
        /// Creates a new tile edit history item.
        /// </summary>
        /// <param name="tileSet">The tile set containing the tile.</param>
        /// <param name="tileId">The ID of the tile that was edited.</param>
        /// <param name="pixelsBefore">Tile pixels before the edit (BGRA).</param>
        /// <param name="pixelsAfter">Tile pixels after the edit (BGRA).</param>
        /// <param name="description">Description of the operation.</param>
        public TileEditHistoryItem(
            TileSet tileSet,
            int tileId,
            byte[] pixelsBefore,
            byte[] pixelsAfter,
            string description = "Edit Tile")
        {
            _tileSet = tileSet ?? throw new ArgumentNullException(nameof(tileSet));
            _tileId = tileId;
            _pixelsBefore = (byte[])pixelsBefore.Clone();
            _pixelsAfter = (byte[])pixelsAfter.Clone();
            _description = description;
        }

        /// <summary>
        /// Gets the tile ID this history item affects.
        /// </summary>
        public int TileId => _tileId;

        /// <summary>
        /// Gets whether there are actual pixel changes between before and after.
        /// </summary>
        public bool HasChanges
        {
            get
            {
                if (_pixelsBefore.Length != _pixelsAfter.Length)
                    return true;

                for (int i = 0; i < _pixelsBefore.Length; i++)
                {
                    if (_pixelsBefore[i] != _pixelsAfter[i])
                        return true;
                }
                return false;
            }
        }

        /// <inheritdoc/>
        public void Undo()
        {
            LoggingService.Info("Undo tile edit tileId={TileId} desc={Desc}", _tileId, _description);
            _tileSet.UpdateTilePixels(_tileId, _pixelsBefore);
        }

        /// <inheritdoc/>
        public void Redo()
        {
            LoggingService.Info("Redo tile edit tileId={TileId} desc={Desc}", _tileId, _description);
            _tileSet.UpdateTilePixels(_tileId, _pixelsAfter);
        }

        /// <summary>
        /// Creates a tile edit history item by comparing current tile state with a before snapshot.
        /// </summary>
        /// <param name="tileSet">The tile set.</param>
        /// <param name="tileId">The tile ID.</param>
        /// <param name="pixelsBefore">Pixels captured before the edit.</param>
        /// <param name="description">Description of the operation.</param>
        /// <returns>A new history item, or null if no changes occurred.</returns>
        public static TileEditHistoryItem? FromBeforeState(
            TileSet tileSet,
            int tileId,
            byte[] pixelsBefore,
            string description = "Edit Tile")
        {
            var currentPixels = tileSet.GetTilePixels(tileId);
            if (currentPixels == null)
                return null;

            var item = new TileEditHistoryItem(tileSet, tileId, pixelsBefore, currentPixels, description);

            if (!item.HasChanges)
                return null;

            LoggingService.Debug("Created TileEditHistoryItem tileId={TileId} desc={Desc}", tileId, description);
            return item;
        }
    }
}
