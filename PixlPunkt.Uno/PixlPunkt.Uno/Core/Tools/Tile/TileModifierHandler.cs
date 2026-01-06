using System;
using System.Collections.Generic;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Tile;

namespace PixlPunkt.Uno.Core.Tools.Tile
{
    /// <summary>
    /// Handler for the Tile Modifier tool.
    /// </summary>
    /// <remarks>
    /// <para><strong>Actions:</strong></para>
    /// <list type="bullet">
    /// <item>LMB drag: Offset tile content (tessellating wrap)</item>
    /// <item>Ctrl + LMB drag: Rotate tile content within bounds</item>
    /// <item>Shift + LMB drag: Scale tile content in drag direction</item>
    /// <item>RMB: Sample tile and mapping (tile dropper)</item>
    /// </list>
    /// <para>
    /// On release, content outside tile bounds is clipped. Only pixels within
    /// the tile boundary are preserved. Changes are propagated to all mapped
    /// instances of the tile.
    /// </para>
    /// </remarks>
    public sealed class TileModifierHandler : ITileHandler
    {
        private readonly ITileContext _context;
        private readonly TileModifierToolSettings _settings;

        private bool _isActive;
        private ModifyMode _mode = ModifyMode.None;
        private int _activeTileX;
        private int _activeTileY;
        private int _activeTileId = -1;
        private int _startDocX;
        private int _startDocY;
        private int _currentDocX;
        private int _currentDocY;

        // Original tile pixels for transform operations
        private byte[]? _originalTilePixels;
        private int _tileWidth;
        private int _tileHeight;

        // All positions mapped to the same tile ID (for propagation)
        private List<(int tx, int ty)>? _allMappedPositions;

        // History tracking
        private Dictionary<int, byte[]>? _tileBeforeStates;
        private byte[]? _layerBeforeSnapshot;

        private enum ModifyMode
        {
            None,
            Offset,
            Rotate,
            Scale
        }

        /// <inheritdoc/>
        public string ToolId => ToolIds.TileModifier;

        /// <inheritdoc/>
        public bool IsActive => _isActive;

        /// <inheritdoc/>
        public TileCursorHint CursorHint => _mode switch
        {
            ModifyMode.Offset => TileCursorHint.Offset,
            ModifyMode.Rotate => TileCursorHint.Rotate,
            ModifyMode.Scale => TileCursorHint.Scale,
            _ => TileCursorHint.Default
        };

        /// <summary>
        /// Creates a new Tile Modifier handler.
        /// </summary>
        /// <param name="context">The tile context for host services.</param>
        /// <param name="settings">The modifier tool settings.</param>
        public TileModifierHandler(ITileContext context, TileModifierToolSettings settings)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

            // Determine modification mode based on modifiers
            if (isCtrlHeld)
                _mode = ModifyMode.Rotate;
            else if (isShiftHeld)
                _mode = ModifyMode.Scale;
            else
                _mode = ModifyMode.Offset;

            // Get the tile at this position
            var (tileX, tileY) = _context.DocToTile(docX, docY);
            _activeTileX = tileX;
            _activeTileY = tileY;
            _startDocX = docX;
            _startDocY = docY;
            _currentDocX = docX;
            _currentDocY = docY;

            // Get the tile ID at this position
            _activeTileId = _context.GetMappedTileId(tileX, tileY);

            // Capture original tile pixels from tile definition (if mapped) or layer
            var (tileDocX, tileDocY, width, height) = _context.GetTileRect(tileX, tileY);
            _tileWidth = width;
            _tileHeight = height;

            if (_activeTileId >= 0)
            {
                // Get from tile definition for consistency
                var tilePixels = _context.GetTilePixels(_activeTileId);
                _originalTilePixels = tilePixels != null ? (byte[])tilePixels.Clone() : _context.ReadLayerRect(tileDocX, tileDocY, width, height);
            }
            else
            {
                _originalTilePixels = _context.ReadLayerRect(tileDocX, tileDocY, width, height);
            }

            // Capture layer snapshot for history
            _layerBeforeSnapshot = (byte[])_context.GetActiveLayerPixels().Clone();

            // Capture tile definition before states for history
            _tileBeforeStates = new Dictionary<int, byte[]>();
            int tileCountX = _context.TileCountX;
            int tileCountY = _context.TileCountY;

            // Capture all tile definitions that might be affected
            var capturedIds = new HashSet<int>();
            for (int ty = 0; ty < tileCountY; ty++)
            {
                for (int tx = 0; tx < tileCountX; tx++)
                {
                    int id = _context.GetMappedTileId(tx, ty);
                    if (id >= 0 && !capturedIds.Contains(id))
                    {
                        var pixels = _context.GetTilePixels(id);
                        if (pixels != null)
                        {
                            _tileBeforeStates[id] = (byte[])pixels.Clone();
                            capturedIds.Add(id);
                        }
                    }
                }
            }

            // Find all positions mapped to the same tile ID (for propagation)
            _allMappedPositions = null;
            if (_activeTileId >= 0)
            {
                _allMappedPositions = new List<(int tx, int ty)>();

                for (int ty = 0; ty < tileCountY; ty++)
                {
                    for (int tx = 0; tx < tileCountX; tx++)
                    {
                        if (_context.GetMappedTileId(tx, ty) == _activeTileId)
                        {
                            _allMappedPositions.Add((tx, ty));
                        }
                    }
                }
            }

            _isActive = true;
            _context.CapturePointer();

            return true;
        }

        /// <inheritdoc/>
        public bool PointerMoved(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            if (!_isActive || _originalTilePixels == null)
                return false;

            _currentDocX = docX;
            _currentDocY = docY;

            // Calculate delta from start
            int deltaX = docX - _startDocX;
            int deltaY = docY - _startDocY;

            // Apply transformation based on mode
            byte[] transformedPixels = _mode switch
            {
                ModifyMode.Offset => ApplyOffset(_originalTilePixels, deltaX, deltaY),
                ModifyMode.Rotate => ApplyRotation(_originalTilePixels, deltaX, deltaY),
                ModifyMode.Scale => ApplyScale(_originalTilePixels, deltaX, deltaY),
                _ => _originalTilePixels
            };

            // Update the tile definition if this tile is mapped
            if (_activeTileId >= 0)
            {
                _context.UpdateTilePixels(_activeTileId, transformedPixels);
            }

            // Propagate to all mapped instances
            if (_allMappedPositions != null && _allMappedPositions.Count > 0)
            {
                foreach (var (tx, ty) in _allMappedPositions)
                {
                    var (tdocX, tdocY, _, _) = _context.GetTileRect(tx, ty);
                    WriteLayerRectDirect(tdocX, tdocY, _tileWidth, _tileHeight, transformedPixels);
                }
            }
            else
            {
                // No mapping - just write to the active tile position
                var (tileDocX, tileDocY, _, _) = _context.GetTileRect(_activeTileX, _activeTileY);
                WriteLayerRectDirect(tileDocX, tileDocY, _tileWidth, _tileHeight, transformedPixels);
            }

            _context.Invalidate();

            return true;
        }

        /// <summary>
        /// Writes pixels directly to the layer without going through ITileContext.WriteLayerRect
        /// which would trigger display refresh on each call. We'll refresh once at the end.
        /// </summary>
        private void WriteLayerRectDirect(int x, int y, int width, int height, byte[] pixels)
        {
            var layerPixels = _context.GetActiveLayerPixels();
            if (layerPixels.Length == 0)
                return;

            int docW = _context.DocumentWidth;
            int docH = _context.DocumentHeight;
            int layerStride = docW * 4;

            for (int row = 0; row < height; row++)
            {
                int dstY = y + row;
                if (dstY < 0 || dstY >= docH) continue;

                int srcOffset = row * width * 4;
                int dstOffset = dstY * layerStride + x * 4;

                int copyWidth = width;
                if (x < 0)
                {
                    int skip = -x;
                    copyWidth -= skip;
                    srcOffset += skip * 4;
                    dstOffset = dstY * layerStride;
                }
                if (x + width > docW)
                {
                    copyWidth = Math.Max(0, docW - Math.Max(0, x));
                }

                if (copyWidth > 0 && srcOffset >= 0 && srcOffset + copyWidth * 4 <= pixels.Length &&
                    dstOffset >= 0 && dstOffset + copyWidth * 4 <= layerPixels.Length)
                {
                    Buffer.BlockCopy(pixels, srcOffset, layerPixels, dstOffset, copyWidth * 4);
                }
            }
        }

        /// <inheritdoc/>
        public bool PointerReleased(double screenX, double screenY, int docX, int docY,
            bool isLeftButton, bool isRightButton, bool isShiftHeld, bool isCtrlHeld)
        {
            if (!_isActive)
                return false;

            _context.ReleasePointer();

            // Create and push history item via extended context
            if (_context is ITileModifierHistoryContext historyContext)
            {
                historyContext.PushTileModifierHistory(
                    _activeTileId,
                    _allMappedPositions,
                    _tileBeforeStates,
                    _layerBeforeSnapshot,
                    $"Modify tile at ({_activeTileX}, {_activeTileY})");
            }

            _isActive = false;
            _mode = ModifyMode.None;
            _originalTilePixels = null;
            _allMappedPositions = null;
            _activeTileId = -1;
            _tileBeforeStates = null;
            _layerBeforeSnapshot = null;

            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (_isActive)
            {
                _context.ReleasePointer();

                // Revert changes
                if (_layerBeforeSnapshot != null)
                {
                    var layerPixels = _context.GetActiveLayerPixels();
                    if (layerPixels.Length == _layerBeforeSnapshot.Length)
                    {
                        Buffer.BlockCopy(_layerBeforeSnapshot, 0, layerPixels, 0, _layerBeforeSnapshot.Length);
                    }
                }

                // Revert tile definitions
                if (_tileBeforeStates != null)
                {
                    foreach (var (tileId, beforePixels) in _tileBeforeStates)
                    {
                        _context.UpdateTilePixels(tileId, beforePixels);
                    }
                }

                _context.Invalidate();
            }

            _isActive = false;
            _mode = ModifyMode.None;
            _originalTilePixels = null;
            _allMappedPositions = null;
            _activeTileId = -1;
            _tileBeforeStates = null;
            _layerBeforeSnapshot = null;
        }

        /// <inheritdoc/>
        public TileOverlayPreview? GetOverlayPreview()
        {
            if (_isActive)
            {
                return new TileOverlayPreview(
                    TileX: _activeTileX,
                    TileY: _activeTileY,
                    TileId: -1,
                    ShowGhost: false,
                    ShowOutline: true,
                    SnapToGrid: true
                );
            }

            // When not active, show outline at hover position
            var (tileX, tileY) = _context.DocToTile(_currentDocX, _currentDocY);
            return new TileOverlayPreview(
                TileX: tileX,
                TileY: tileY,
                TileId: -1,
                ShowGhost: false,
                ShowOutline: true,
                SnapToGrid: true
            );
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
        /// Applies offset transformation with optional wrapping.
        /// </summary>
        private byte[] ApplyOffset(byte[] source, int deltaX, int deltaY)
        {
            var result = new byte[source.Length];
            int w = _tileWidth;
            int h = _tileHeight;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int srcX, srcY;

                    if (_settings.WrapContent)
                    {
                        // Wrap around tile edges (tessellation)
                        srcX = ((x - deltaX) % w + w) % w;
                        srcY = ((y - deltaY) % h + h) % h;
                    }
                    else
                    {
                        srcX = x - deltaX;
                        srcY = y - deltaY;

                        // Clip to bounds
                        if (srcX < 0 || srcX >= w || srcY < 0 || srcY >= h)
                        {
                            // Leave as transparent
                            continue;
                        }
                    }

                    int srcIdx = (srcY * w + srcX) * 4;
                    int dstIdx = (y * w + x) * 4;

                    result[dstIdx] = source[srcIdx];
                    result[dstIdx + 1] = source[srcIdx + 1];
                    result[dstIdx + 2] = source[srcIdx + 2];
                    result[dstIdx + 3] = source[srcIdx + 3];
                }
            }

            return result;
        }

        /// <summary>
        /// Applies rotation transformation around tile center.
        /// </summary>
        private byte[] ApplyRotation(byte[] source, int deltaX, int deltaY)
        {
            // Calculate rotation angle from drag distance
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            double angle = distance * 0.02; // Convert distance to rotation angle

            // Use horizontal direction for sign
            if (deltaX < 0) angle = -angle;

            if (_settings.ConstrainRotation)
            {
                // Snap to 15-degree increments
                double snapAngle = 15.0 * Math.PI / 180.0;
                angle = Math.Round(angle / snapAngle) * snapAngle;
            }

            return RotatePixels(source, _tileWidth, _tileHeight, angle, _settings.RotationMode);
        }

        /// <summary>
        /// Applies scale transformation based on drag direction.
        /// </summary>
        private byte[] ApplyScale(byte[] source, int deltaX, int deltaY)
        {
            // Scale factor based on drag distance
            double scaleX = 1.0 + deltaX * 0.01;
            double scaleY = 1.0 + deltaY * 0.01;

            scaleX = Math.Clamp(scaleX, 0.1, 4.0);
            scaleY = Math.Clamp(scaleY, 0.1, 4.0);

            return ScalePixels(source, _tileWidth, _tileHeight, scaleX, scaleY, _settings.ScaleMode);
        }

        /// <summary>
        /// Rotates pixels around center using the specified interpolation mode.
        /// </summary>
        private static byte[] RotatePixels(byte[] source, int w, int h, double angle, RotationMode mode)
        {
            var result = new byte[source.Length];
            double cos = Math.Cos(-angle);
            double sin = Math.Sin(-angle);
            double cx = w / 2.0;
            double cy = h / 2.0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double dx = x - cx;
                    double dy = y - cy;

                    double srcX = dx * cos - dy * sin + cx;
                    double srcY = dx * sin + dy * cos + cy;

                    int dstIdx = (y * w + x) * 4;

                    if (mode == RotationMode.NearestNeighbor)
                    {
                        int sx = (int)Math.Round(srcX);
                        int sy = (int)Math.Round(srcY);

                        if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                        {
                            int srcIdx = (sy * w + sx) * 4;
                            result[dstIdx] = source[srcIdx];
                            result[dstIdx + 1] = source[srcIdx + 1];
                            result[dstIdx + 2] = source[srcIdx + 2];
                            result[dstIdx + 3] = source[srcIdx + 3];
                        }
                    }
                    else // Bilinear
                    {
                        BilinearSample(source, w, h, srcX, srcY, result, dstIdx);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Scales pixels around center using the specified interpolation mode.
        /// </summary>
        private static byte[] ScalePixels(byte[] source, int w, int h, double scaleX, double scaleY, ScaleMode mode)
        {
            var result = new byte[source.Length];
            double cx = w / 2.0;
            double cy = h / 2.0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double srcX = (x - cx) / scaleX + cx;
                    double srcY = (y - cy) / scaleY + cy;

                    int dstIdx = (y * w + x) * 4;

                    if (mode == ScaleMode.NearestNeighbor)
                    {
                        int sx = (int)Math.Round(srcX);
                        int sy = (int)Math.Round(srcY);

                        if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                        {
                            int srcIdx = (sy * w + sx) * 4;
                            result[dstIdx] = source[srcIdx];
                            result[dstIdx + 1] = source[srcIdx + 1];
                            result[dstIdx + 2] = source[srcIdx + 2];
                            result[dstIdx + 3] = source[srcIdx + 3];
                        }
                    }
                    else // Bilinear
                    {
                        BilinearSample(source, w, h, srcX, srcY, result, dstIdx);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Performs bilinear interpolation sampling.
        /// </summary>
        private static void BilinearSample(byte[] source, int w, int h, double srcX, double srcY, byte[] dest, int dstIdx)
        {
            int x0 = (int)Math.Floor(srcX);
            int y0 = (int)Math.Floor(srcY);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            double fx = srcX - x0;
            double fy = srcY - y0;

            // Clamp to bounds
            x0 = Math.Clamp(x0, 0, w - 1);
            x1 = Math.Clamp(x1, 0, w - 1);
            y0 = Math.Clamp(y0, 0, h - 1);
            y1 = Math.Clamp(y1, 0, h - 1);

            // If out of bounds, leave transparent
            if (srcX < -0.5 || srcX > w - 0.5 || srcY < -0.5 || srcY > h - 0.5)
                return;

            int i00 = (y0 * w + x0) * 4;
            int i10 = (y0 * w + x1) * 4;
            int i01 = (y1 * w + x0) * 4;
            int i11 = (y1 * w + x1) * 4;

            for (int c = 0; c < 4; c++)
            {
                double v00 = source[i00 + c];
                double v10 = source[i10 + c];
                double v01 = source[i01 + c];
                double v11 = source[i11 + c];

                double top = v00 * (1 - fx) + v10 * fx;
                double bottom = v01 * (1 - fx) + v11 * fx;
                double value = top * (1 - fy) + bottom * fy;

                dest[dstIdx + c] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }
        }
    }

    /// <summary>
    /// Extended context interface for tile modifier history operations.
    /// </summary>
    public interface ITileModifierHistoryContext
    {
        /// <summary>
        /// Pushes a tile modifier history item to the undo stack.
        /// </summary>
        void PushTileModifierHistory(
            int activeTileId,
            List<(int tx, int ty)>? mappedPositions,
            Dictionary<int, byte[]>? tileBeforeStates,
            byte[]? layerBeforeSnapshot,
            string description);
    }
}
