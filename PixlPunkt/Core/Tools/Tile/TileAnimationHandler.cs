using System;
using System.Collections.Generic;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.Core.Tools.Tile
{
    /// <summary>
    /// Handler for the Tile Animation tool.
    /// </summary>
    /// <remarks>
    /// <para><strong>Actions:</strong></para>
    /// <list type="bullet">
    /// <item>LMB drag: Select tile positions on canvas for animation frames (row-major order)</item>
    /// <item>Shift + LMB: Add to existing frames instead of replacing</item>
    /// <item>RMB: Sample tile (tile dropper)</item>
    /// </list>
    /// <para>
    /// Tiles are selected based on their grid position on the canvas (row-major order).
    /// Each frame stores a tile position (tileX, tileY) which references pixels drawn
    /// at that grid location - NOT necessarily mapped TileSet tiles.
    /// </para>
    /// </remarks>
    public sealed class TileAnimationHandler : ITileHandler
    {
        private readonly ITileContext _context;
        private readonly TileAnimationToolSettings _settings;
        private readonly Func<TileAnimationState?> _getAnimationState;

        private bool _isActive;
        private int _hoverTileX;
        private int _hoverTileY;
        private int _startTileX;
        private int _startTileY;
        private int _endTileX;
        private int _endTileY;
        private bool _isDragging;
        private bool _addToExisting;

        /// <inheritdoc/>
        public string ToolId => ToolIds.TileAnimation;

        /// <inheritdoc/>
        public bool IsActive => _isActive;

        /// <inheritdoc/>
        public TileCursorHint CursorHint => _isActive ? TileCursorHint.Default : TileCursorHint.Default;

        /// <summary>
        /// Creates a new Tile Animation handler.
        /// </summary>
        /// <param name="context">The tile context for host services.</param>
        /// <param name="settings">The animation tool settings.</param>
        /// <param name="getAnimationState">Function to get the current animation state.</param>
        public TileAnimationHandler(
            ITileContext context,
            TileAnimationToolSettings settings,
            Func<TileAnimationState?> getAnimationState)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _getAnimationState = getAnimationState ?? throw new ArgumentNullException(nameof(getAnimationState));
        }

        /// <inheritdoc/>
        public bool PointerPressed(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            // RMB: Sample tile (tile dropper)
            if (isRightButton)
            {
                SampleTileAt(docX, docY);
                return true;
            }

            if (!isLeftButton)
                return false;

            var (tileX, tileY) = _context.DocToTile(docX, docY);

            // Start drag selection
            _startTileX = tileX;
            _startTileY = tileY;
            _endTileX = tileX;
            _endTileY = tileY;
            _hoverTileX = tileX;
            _hoverTileY = tileY;
            _isDragging = true;
            _isActive = true;

            // Check if shift is held to add to existing
            _addToExisting = isShiftHeld;

            _context.Invalidate();
            return true;
        }

        /// <inheritdoc/>
        public bool PointerMoved(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            var (tileX, tileY) = _context.DocToTile(docX, docY);
            _hoverTileX = tileX;
            _hoverTileY = tileY;

            if (_isDragging)
            {
                _endTileX = tileX;
                _endTileY = tileY;
            }

            _context.Invalidate();
            return _isDragging;
        }

        /// <inheritdoc/>
        public bool PointerReleased(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            if (!_isDragging)
            {
                _isActive = false;
                return false;
            }

            _isDragging = false;
            _isActive = false;

            // Commit the selection to the animation
            CommitFrameSelection();

            _context.Invalidate();
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _isActive = false;
            _isDragging = false;
        }

        /// <inheritdoc/>
        public TileOverlayPreview? GetOverlayPreview()
        {
            // When dragging, show the animation selection overlay
            if (_isDragging)
            {
                return new TileOverlayPreview(
                    TileX: _hoverTileX,
                    TileY: _hoverTileY,
                    TileId: -1,
                    ShowGhost: false,
                    ShowOutline: true,
                    SnapToGrid: true,
                    PixelOffsetX: 0,
                    PixelOffsetY: 0,
                    AnimationSelection: new AnimationSelectionOverlay(
                        StartTileX: _startTileX,
                        StartTileY: _startTileY,
                        EndTileX: _endTileX,
                        EndTileY: _endTileY,
                        IsDragging: true
                    )
                );
            }

            // When not dragging, show hover tile outline
            return new TileOverlayPreview(
                TileX: _hoverTileX,
                TileY: _hoverTileY,
                TileId: -1,
                ShowGhost: false,
                ShowOutline: true,
                SnapToGrid: true,
                PixelOffsetX: 0,
                PixelOffsetY: 0,
                AnimationSelection: null
            );
        }

        /// <summary>
        /// Gets the start tile position for the current selection.
        /// </summary>
        public (int tileX, int tileY) StartTile => (_startTileX, _startTileY);

        /// <summary>
        /// Gets the end tile position for the current selection.
        /// </summary>
        public (int tileX, int tileY) EndTile => (_endTileX, _endTileY);

        /// <summary>
        /// Gets whether a drag selection is in progress.
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Gets the list of tile grid positions in the current selection (row-major order).
        /// </summary>
        public List<(int tileX, int tileY)> GetSelectedTilePositions()
        {
            var positions = new List<(int, int)>();

            // Normalize start/end to ensure start <= end in row-major order
            int startX = Math.Min(_startTileX, _endTileX);
            int startY = Math.Min(_startTileY, _endTileY);
            int endX = Math.Max(_startTileX, _endTileX);
            int endY = Math.Max(_startTileY, _endTileY);

            // Clamp to document bounds
            startX = Math.Max(0, startX);
            startY = Math.Max(0, startY);
            endX = Math.Min(_context.TileCountX - 1, endX);
            endY = Math.Min(_context.TileCountY - 1, endY);

            // Get positions in row-major order
            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    positions.Add((x, y));
                }
            }

            return positions;
        }

        /// <summary>
        /// Samples the tile at a document position (RMB tile dropper).
        /// </summary>
        private void SampleTileAt(int docX, int docY)
        {
            var (tileId, _, _) = _context.SampleTileAt(docX, docY);
            if (tileId >= 0)
            {
                _context.SetSelectedTile(tileId);
            }
        }

        /// <summary>
        /// Commits the current frame selection to the animation reel.
        /// </summary>
        private void CommitFrameSelection()
        {
            var animState = _getAnimationState();
            if (animState == null)
                return;

            // Create a reel if none exists
            if (animState.Reels.Count == 0)
            {
                animState.AddReel("Animation 1");
            }

            // Select the first reel if none selected
            if (animState.SelectedReel == null && animState.Reels.Count > 0)
            {
                animState.SelectReel(animState.Reels[0]);
            }

            var reel = animState.SelectedReel;
            if (reel == null)
                return;

            var positions = GetSelectedTilePositions();
            if (positions.Count == 0)
                return;

            // Clear existing frames if not adding
            if (!_addToExisting)
            {
                reel.Clear();
            }

            // Add frames for each tile position
            reel.AddFrames(positions);

            // Reset to first frame
            animState.SetCurrentFrame(0);
        }
    }
}
