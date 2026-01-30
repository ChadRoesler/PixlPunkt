using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.History;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;
using SelectionState = PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem.SelectionState;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// History, selection lift/commit, and undo/redo orchestration for CanvasViewHost.
    /// Uses the unified history stack on the document for all operations.
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ────────────────────────────────────────────────────────────────────
        // HISTORY STATE
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the history state changes (after undo, redo, or commit operations).
        /// </summary>
        public event Action? HistoryStateChanged;

        /// <summary>Gets whether an undo operation is available.</summary>
        public bool CanUndo => Document.History.CanUndo;

        /// <summary>Gets whether a redo operation is available.</summary>
        public bool CanRedo => Document.History.CanRedo;

        /// <summary>Gets the description of the next undo operation.</summary>
        public string? UndoDescription => Document.History.UndoDescription;

        /// <summary>Gets the description of the next redo operation.</summary>
        public string? RedoDescription => Document.History.RedoDescription;

        // ────────────────────────────────────────────────────────────────────
        // HISTORY HELPERS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pushes a pixel change to the unified history stack.
        /// </summary>
        private void PushPixelChange(RasterLayer layer, RectInt32 rect, byte[] before, byte[] after, string description = "Pixel Change")
        {
            var item = PixelChangeItem.FromRegion(layer, rect, before, after, description);
            Document.History.Push(item);
        }

        /// <summary>
        /// Pushes a multi-region pixel change to the unified history stack.
        /// </summary>
        private void PushMultiRegionChange(RasterLayer layer, (RectInt32 rect, byte[] before, byte[] after)[] regions, string description = "Pixel Change")
        {
            var item = PixelChangeItem.FromMultiRegion(layer, regions, description);
            Document.History.Push(item);
        }

        // ────────────────────────────────────────────────────────────────────
        // SELECTION: LIFT TO FLOATING WITH HISTORY
        // ────────────────────────────────────────────────────────────────────

        // Pending pixel change item for selection operations (lift + commit combined)
        private PixelChangeItem? _selPendingItem;

        /// <summary>
        /// Lifts the current selection to a floating buffer and records BEFORE/AFTER history.
        /// Selected pixels are copied into a floating buffer and cleared from the layer.
        /// Changes are propagated to all mapped tile instances.
        /// </summary>
        private void LiftSelectionWithHistory()
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;

            // Ensure the region is ready
            _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);

            // If no region, fall back to selection rectangle
            var bounds = !_selRegion.IsEmpty
                ? ClampToSurface(_selRegion.Bounds, rl.Surface.Width, rl.Surface.Height)
                : ClampToSurface(Normalize(_selRect), rl.Surface.Width, rl.Surface.Height);

            if (bounds.Width == 0 || bounds.Height == 0) return;

            var surf = rl.Surface;
            int sw = surf.Width, sh = surf.Height;
            int bw = bounds.Width, bh = bounds.Height;

            // Capture tile states BEFORE the lift (for undo)
            bool hasTileMapping = rl.TileMapping != null && Document.TileSet != null;
            if (hasTileMapping)
            {
                _liftTileBeforeStates = new Dictionary<int, byte[]>();
                foreach (var tileId in Document.TileSet!.TileIds)
                {
                    var pixels = Document.TileSet.GetTilePixels(tileId);
                    if (pixels != null)
                    {
                        _liftTileBeforeStates[tileId] = (byte[])pixels.Clone();
                    }
                }
            }
            else
            {
                _liftTileBeforeStates = null;
            }

            // BEFORE snapshot for history - store for combining with commit
            var before = CopyRectBytes(surf.Pixels, sw, sh, bounds);
            _liftBoundsForHistory = bounds;
            _liftPixelsBeforeForHistory = (byte[])before.Clone();

            // Floating buffer with only selected pixels (others transparent)
            var sel = new byte[bw * bh * 4];

            // AFTER snapshot starts as BEFORE, then zeros only selected pixels
            var after = before.AsSpan().ToArray();

            int surfStride = sw * 4;
            int boxStride = bw * 4;

            for (int y = 0; y < bh; y++)
            {
                int sy = bounds.Y + y;
                int srcRow = sy * surfStride + bounds.X * 4;
                int bRow = y * boxStride;

                for (int x = 0; x < bw; x++)
                {
                    int sx = bounds.X + x;
                    int si = srcRow + x * 4;
                    int di = bRow + x * 4;

                    bool inside = !_selRegion.IsEmpty && _selRegion.Contains(sx, sy);

                    if (inside)
                    {
                        // Copy pixel into floating buffer
                        sel[di + 0] = surf.Pixels[si + 0];
                        sel[di + 1] = surf.Pixels[si + 1];
                        sel[di + 2] = surf.Pixels[si + 2];
                        sel[di + 3] = surf.Pixels[si + 3];

                        // Zero in AFTER snapshot
                        after[di + 0] = 0;
                        after[di + 1] = 0;
                        after[di + 2] = 0;
                        after[di + 3] = 0;
                    }
                    else
                    {
                        // Outside region → transparent in floating buffer
                        sel[di + 0] = 0;
                        sel[di + 1] = 0;
                        sel[di + 2] = 0;
                        sel[di + 3] = 0;
                    }
                }
            }

            // Write the AFTER box back to the surface
            for (int y = 0; y < bh; y++)
            {
                int dstOff = (bounds.Y + y) * surfStride + bounds.X * 4;
                int srcOff = y * boxStride;
                Buffer.BlockCopy(after, srcOff, surf.Pixels, dstOff, boxStride);
            }
            UpdateActiveLayerPreview();

            // Propagate the lift (cleared pixels) to all mapped tiles
            if (hasTileMapping)
            {
                PropagateSelectionChangesToMappedTiles(bounds);
            }

            // For non-tile case, create pending history item
            if (!hasTileMapping)
            {
                _selPendingItem = new PixelChangeItem(rl, "Selection Move");
                _selPendingItem.AppendRegionDelta(bounds, before, after);
            }
            else
            {
                _selPendingItem = null;
            }

            // Floating state
            _liftRect = bounds;
            _selBuf = sel;
            _selBW = bw;
            _selBH = bh;

            // Save ORIGINAL dimensions for handle positioning
            _selOrigW = bw;
            _selOrigH = bh;

            // Save ORIGINAL center as stable pivot point for rotation
            _selOrigCenterX = bounds.X + bw / 2;
            _selOrigCenterY = bounds.Y + bh / 2;

            _selFX = bounds.X;
            _selFY = bounds.Y;

            _selFloating = true;
            _selActive = true;
            _selectionState = SelectionState.Armed;
            _selScaleX = _selScaleY = 1.0;
            _selScaleLink = false;
            _selScaleFilter = ScaleMode.NearestNeighbor;
            _selAngleDeg = 0.0;

            Document.RaiseStructureChanged();
            InvalidateMainCanvas();

            _toolState?.SetSelectionPresence(active: true, floating: true);
            _toolState?.SetSelectionScale(100.0, 100.0, false);
            _toolState?.SetSelectionScaleMode(_selScaleFilter);
            _toolState?.SetRotationAngle(0.0);
        }

        // Store the original lift bounds for combining with commit
        private RectInt32 _liftBoundsForHistory;
        private byte[]? _liftPixelsBeforeForHistory;
        private Dictionary<int, byte[]>? _liftTileBeforeStates;

        /// <summary>
        /// Applies scale/rotation to the floating selection, writes it back to the canvas,
        /// and records full BEFORE/AFTER deltas.
        /// </summary>
        private void CommitFloatingWithHistory()
        {
            if (!_selFloating || _selBuf == null) return;
            if (Document.ActiveLayer is not RasterLayer rl) return;

            bool hasTileMapping = rl.TileMapping != null && Document.TileSet != null;

            // Capture the floating state BEFORE commit for undo
            var floatingBefore = new SelectionCommitItem.FloatingSnapshot(
                (byte[])_selBuf.Clone(),
                _selBW, _selBH,
                _selFX, _selFY,
                _selOrigW, _selOrigH,
                _selOrigCenterX, _selOrigCenterY,
                _selScaleX, _selScaleY,
                _selAngleDeg, _selCumulativeAngleDeg,
                _selRegion.Clone()
            );

            // 1) Scale
            var (workBuf, workW, workH) =
                BuildScaledBufferForCommit(_selBuf, _selBW, _selBH,
                                           _selScaleX, _selScaleY, _selScaleFilter);

            int baseW = workW;
            int baseH = workH;

            // 2) Rotate
            double totalRotation = _selCumulativeAngleDeg + _selAngleDeg;
            var (rotBuf, rotW, rotH) =
                BuildRotatedBufferForCommit(workBuf, workW, workH,
                                            totalRotation, _selRotMode);

            int cx = _selOrigCenterX != 0 ? _selOrigCenterX : (_selFX + baseW / 2);
            int cy = _selOrigCenterY != 0 ? _selOrigCenterY : (_selFY + baseH / 2);
            int newFX = cx - rotW / 2;
            int newFY = cy - rotH / 2;

            workBuf = rotBuf; workW = rotW; workH = rotH;
            _selFX = newFX; _selFY = newFY;

            var surf = rl.Surface;
            var dstRect = CreateRect(_selFX, _selFY, workW, workH);
            var dstClamp = ClampToSurface(dstRect, surf.Width, surf.Height);

            // If nothing lands on canvas, push lift history to undo the clearing and clear state
            if (dstClamp.Width == 0 || dstClamp.Height == 0)
            {
                // For tiles: create a history item that undoes just the lift (clear) operation
                if (hasTileMapping && _liftTileBeforeStates != null && _liftPixelsBeforeForHistory != null)
                {
                    // Create history item for the lift operation alone (pixels cleared but not placed anywhere)
                    var liftOnlyItem = new TileAwarePixelChangeItem(
                        rl, Document.TileSet!, _liftBoundsForHistory, _liftPixelsBeforeForHistory,
                        CopyRectBytes(surf.Pixels, surf.Width, surf.Height, _liftBoundsForHistory),
                        "Commit Selection");
                    liftOnlyItem.SetTileBeforeStates(_liftTileBeforeStates);
                    Document.History.Push(liftOnlyItem);
                }
                else if (_selPendingItem != null && !_selPendingItem.IsEmpty)
                {
                    Document.History.Push(_selPendingItem);
                }

                ClearSelectionHistoryState();

                _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);
                _selRegion.Clear();
                _selRect = CreateRect(0, 0, 0, 0);
                _selActive = false;
                _selFloating = false;
                _selBuf = null;

                Document.RaiseStructureChanged();
                InvalidateMainCanvas();
                HistoryStateChanged?.Invoke();
                return;
            }

            // Capture BEFORE snapshot - this is the current layer state with the selection area cleared
            // When we undo, we want to restore this state (not the original pre-lift state)
            // because the floating selection buffer will be restored separately
            var beforeCombined = CopyRectBytes(surf.Pixels, surf.Width, surf.Height, dstClamp);

            // Blit with alpha
            BlitAlphaOver(surf.Pixels, surf.Width, surf.Height,
                          dstRect.X, dstRect.Y,
                          workBuf, workW, workH);

            // Rebuild selection region from transformed buffer
            RebuildSelectionRegionFromTransformedBuffer(
                    dstRect, dstClamp, workBuf, workW, workH,
                    surf.Width, surf.Height);

            // Capture AFTER snapshot for the destination region
            var afterCombined = CopyRectBytes(surf.Pixels, surf.Width, surf.Height, dstClamp);

            // Capture the region after commit
            var regionAfter = _selRegion.Clone();

            // Create history item using SelectionCommitItem for proper undo/redo
            var commitItem = new SelectionCommitItem(
                rl,
                dstClamp,
                beforeCombined,
                afterCombined,
                floatingBefore,
                regionAfter,
                RestoreFloatingSelection,
                ClearFloatingSelection,
                ApplyPixelsToLayer
            );

            // Handle tile mapping propagation
            if (hasTileMapping)
            {
                // Propagate the commit to all mapped tiles
                PropagateSelectionChangesToMappedTiles(dstClamp);
            }

            // Push to history
            Document.History.Push(commitItem);

            ClearSelectionHistoryState();

            // Clear floating state
            _selFloating = false;
            _selBuf = null;
            _selFX = 0;
            _selFY = 0;
            _selBW = 0;
            _selBH = 0;
            _selOrigW = 0;
            _selOrigH = 0;
            _selOrigCenterX = 0;
            _selOrigCenterY = 0;
            _selScaleX = _selScaleY = 1.0;
            _selScaleFilter = ScaleMode.NearestNeighbor;

            HistoryStateChanged?.Invoke();
            Document.RaiseStructureChanged();
            InvalidateMainCanvas();
            SetCursor(InputSystemCursorShape.Arrow);

            _toolState?.SetSelectionPresence(_selActive, _selFloating);
            _toolState?.SetSelectionScale(_selScaleX * 100.0, _selScaleY * 100.0, _selScaleLink);
            _toolState?.SetSelectionScaleMode(_selScaleFilter);

            _selAngleDeg = 0.0;
            _selCumulativeAngleDeg = 0.0;
            _toolState?.SetRotationAngle(0.0);
            UpdateActiveLayerPreview();

            // Raise frame to update tile animation previews (main playback)
            RaiseFrame();

            // Notify document modified to update Frame Edit panel thumbnails
            Document.RaiseDocumentModified();

            // Capture keyframe if in canvas animation mode and layer has animation
            AutoCaptureKeyframeIfNeeded();
        }

        /// <summary>
        /// Restores a floating selection state from a commit undo.
        /// </summary>
        private void RestoreFloatingSelection(SelectionCommitItem.FloatingSnapshot snapshot)
        {
            if (_selState == null) return;

            // Restore the floating buffer
            _selState.Buffer = (byte[])snapshot.Buffer.Clone();
            _selState.BufferWidth = snapshot.BufferWidth;
            _selState.BufferHeight = snapshot.BufferHeight;
            _selState.FloatX = snapshot.FloatX;
            _selState.FloatY = snapshot.FloatY;
            _selState.OrigW = snapshot.OrigW;
            _selState.OrigH = snapshot.OrigH;
            _selState.OrigCenterX = snapshot.OrigCenterX;
            _selState.OrigCenterY = snapshot.OrigCenterY;
            _selState.ScaleX = snapshot.ScaleX;
            _selState.ScaleY = snapshot.ScaleY;
            _selState.AngleDeg = snapshot.AngleDeg;
            _selState.CumulativeAngleDeg = snapshot.CumulativeAngleDeg;

            // Restore the selection region
            _selRegion.CopyFrom(snapshot.Region);

            // Set state to floating/armed
            _selState.Floating = true;
            _selState.Active = true;
            _selState.State = SelectionState.Armed;
            _selState.Rect = _selRegion.Bounds;
            _selState.ResetPivot();

            // Notify tool state
            _selState.NotifyToolState();
            _toolState?.SetSelectionPresence(true, true);
            _toolState?.SetSelectionScale(snapshot.ScaleX * 100.0, snapshot.ScaleY * 100.0, _selState.ScaleLink);
            _toolState?.SetRotationAngle(snapshot.AngleDeg);

            // Recomposite and redraw
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            Document.RaiseStructureChanged();
            InvalidateMainCanvas();
        }

        /// <summary>
        /// Clears the floating selection state (for commit redo).
        /// </summary>
        private void ClearFloatingSelection()
        {
            if (_selState == null) return;

            _selState.Floating = false;
            _selState.Buffer = null;
            _selState.BufferWidth = 0;
            _selState.BufferHeight = 0;
            _selState.FloatX = 0;
            _selState.FloatY = 0;
            _selState.OrigW = 0;
            _selState.OrigH = 0;
            _selState.OrigCenterX = 0;
            _selState.OrigCenterY = 0;
            _selState.ScaleX = 1.0;
            _selState.ScaleY = 1.0;
            _selState.AngleDeg = 0.0;
            _selState.CumulativeAngleDeg = 0.0;

            // Clear selection region
            _selRegion.Clear();
            _selState.Active = false;
            _selState.State = SelectionState.None;
            _selState.Rect = CreateRect(0, 0, 0, 0);

            // Notify tool state
            _toolState?.SetSelectionPresence(false, false);
            _toolState?.SetSelectionScale(100.0, 100.0, false);
            _toolState?.SetRotationAngle(0.0);

            // Recomposite and redraw
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            Document.RaiseStructureChanged();
            InvalidateMainCanvas();
        }

        /// <summary>
        /// Applies pixel data to a layer region (for history undo/redo).
        /// </summary>
        private void ApplyPixelsToLayer(RasterLayer layer, RectInt32 bounds, byte[] pixels)
        {
            if (layer?.Surface?.Pixels == null) return;

            var surf = layer.Surface;
            int sw = surf.Width;
            int stride = sw * 4;
            int regionStride = bounds.Width * 4;

            for (int y = 0; y < bounds.Height; y++)
            {
                int dstY = bounds.Y + y;
                if (dstY < 0 || dstY >= surf.Height) continue;

                int srcOffset = y * regionStride;
                int dstOffset = dstY * stride + bounds.X * 4;

                int copyWidth = bounds.Width;
                if (bounds.X < 0)
                {
                    int skip = -bounds.X;
                    copyWidth -= skip;
                    srcOffset += skip * 4;
                    dstOffset = dstY * stride;
                }
                if (bounds.X + bounds.Width > sw)
                {
                    copyWidth = Math.Max(0, sw - Math.Max(0, bounds.X));
                }

                if (copyWidth > 0 && srcOffset >= 0 && srcOffset + copyWidth * 4 <= pixels.Length &&
                    dstOffset >= 0 && dstOffset + copyWidth * 4 <= surf.Pixels.Length)
                {
                    Buffer.BlockCopy(pixels, srcOffset, surf.Pixels, dstOffset, copyWidth * 4);
                }
            }

            // Recomposite
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
        }

        /// <summary>
        /// Clears selection history state after commit or cancel.
        /// </summary>
        private void ClearSelectionHistoryState()
        {
            _selPendingItem = null;
            _liftPixelsBeforeForHistory = null;
            _liftTileBeforeStates = null;
        }

        /// <summary>
        /// Creates a layer snapshot with a region restored to its previous state.
        /// Used to compute combined before state for history.
        /// </summary>
        private byte[] RestoreLayerSnapshot(byte[] current, int layerW, int layerH, RectInt32 restoreRegion, byte[] restorePixels)
        {
            var result = (byte[])current.Clone();
            int stride = layerW * 4;
            int regionStride = restoreRegion.Width * 4;

            for (int y = 0; y < restoreRegion.Height; y++)
            {
                int dstY = restoreRegion.Y + y;
                if (dstY < 0 || dstY >= layerH) continue;

                int srcOffset = y * regionStride;
                int dstOffset = dstY * stride + restoreRegion.X * 4;

                int copyWidth = restoreRegion.Width;
                if (restoreRegion.X < 0)
                {
                    int skip = -restoreRegion.X;
                    copyWidth -= skip;
                    srcOffset += skip * 4;
                    dstOffset = dstY * stride;
                }
                if (restoreRegion.X + restoreRegion.Width > layerW)
                {
                    copyWidth = Math.Max(0, layerW - Math.Max(0, restoreRegion.X));
                }

                if (copyWidth > 0 && srcOffset >= 0 && srcOffset + copyWidth * 4 <= restorePixels.Length &&
                    dstOffset >= 0 && dstOffset + copyWidth * 4 <= result.Length)
                {
                    Buffer.BlockCopy(restorePixels, srcOffset, result, dstOffset, copyWidth * 4);
                }
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────────────
        // SELECTION REGION REBUILDERS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the selection region as a rotated rectangle after transforms.
        /// </summary>
        private void RebuildSelectionRegionAsRotatedRect(
            int centerX, int centerY, int baseW, int baseH,
            double angleDeg, RectInt32 dstClamp, int surfW, int surfH)
        {
            _selRegion.EnsureSize(surfW, surfH);
            _selRegion.Clear();

            if (baseW <= 0 || baseH <= 0 || dstClamp.Width <= 0 || dstClamp.Height <= 0)
            {
                _selRect = CreateRect(0, 0, 0, 0);
                _selActive = false;
                return;
            }

            double rad = angleDeg * Math.PI / 180.0;
            double cosA = Math.Cos(rad);
            double sinA = Math.Sin(rad);
            double halfW = baseW / 2.0;
            double halfH = baseH / 2.0;

            int x0 = dstClamp.X, y0 = dstClamp.Y;
            int x1 = dstClamp.X + dstClamp.Width;
            int y1 = dstClamp.Y + dstClamp.Height;

            for (int y = y0; y < y1; y++)
            {
                int runStart = -1;
                double fy = y + 0.5;
                double dy = fy - centerY;

                for (int x = x0; x < x1; x++)
                {
                    double fx = x + 0.5;
                    double dx = fx - centerX;
                    double localX = cosA * dx + sinA * dy;
                    double localY = -sinA * dx + cosA * dy;

                    bool inside = localX >= -halfW && localX < halfW &&
                                  localY >= -halfH && localY < halfH;

                    if (inside)
                    {
                        if (runStart < 0) runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        _selRegion.AddRect(CreateRect(runStart, y, x - runStart, 1));
                        runStart = -1;
                    }
                }

                if (runStart >= 0)
                    _selRegion.AddRect(CreateRect(runStart, y, x1 - runStart, 1));
            }

            _selRect = _selRegion.Bounds;
            _selActive = !_selRegion.IsEmpty;
        }

        /// <summary>
        /// Builds the selection region from the transformed buffer's alpha,
        /// walking the destination area and including pixels where alpha != 0.
        /// Use when you want ants to trace actual pixels (after scale/rotate),
        /// rather than parametric geometry.
        /// </summary>
        private void RebuildSelectionRegionFromTransformedBuffer(
            RectInt32 dstRect, RectInt32 dstClamp, byte[] buf,
            int bufW, int bufH, int surfW, int surfH)
        {
            if (buf == null || bufW <= 0 || bufH <= 0) return;

            var r = ClampToSurface(dstClamp, surfW, surfH);
            if (r.Width <= 0 || r.Height <= 0)
            {
                _selRegion.Clear();
                _selRect = CreateRect(0, 0, 0, 0);
                _selActive = false;
                return;
            }

            _selRegion.EnsureSize(surfW, surfH);
            _selRegion.Clear();

            int x0 = r.X, y0 = r.Y;
            int x1 = r.X + r.Width, y1 = r.Y + r.Height;

            for (int y = y0; y < y1; y++)
            {
                int sy = y - dstRect.Y;
                if ((uint)sy >= (uint)bufH) continue;

                int runStart = -1;
                for (int x = x0; x < x1; x++)
                {
                    int sx = x - dstRect.X;
                    if ((uint)sx >= (uint)bufW)
                    {
                        if (runStart >= 0)
                        {
                            _selRegion.AddRect(CreateRect(runStart, y, x - runStart, 1));
                            runStart = -1;
                        }
                        continue;
                    }

                    int si = (sy * bufW + sx) * 4;
                    bool on = buf[si + 3] != 0;

                    if (on)
                    {
                        if (runStart < 0) runStart = x;
                    }
                    else if (runStart >= 0)
                    {
                        _selRegion.AddRect(CreateRect(runStart, y, x - runStart, 1));
                        runStart = -1;
                    }
                }

                if (runStart >= 0)
                    _selRegion.AddRect(CreateRect(runStart, y, x1 - runStart, 1));
            }

            _selRect = _selRegion.Bounds;
            _selActive = !_selRegion.IsEmpty;
        }

        // ────────────────────────────────────────────────────────────────────
        // REGION MUTATION + HISTORY
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs a mutator over a clamped rectangle and pushes to unified history.
        /// </summary>
        private void ApplyWithHistory(RectInt32 rect, Action<byte[]> mutator, string description = "Edit")
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;
            var surf = rl.Surface;

            var r = ClampToSurface(Normalize(rect), surf.Width, surf.Height);
            if (r.Width == 0 || r.Height == 0) return;

            var before = CopyRectBytes(surf.Pixels, surf.Width, surf.Height, r);
            mutator(surf.Pixels);
            var after = CopyRectBytes(surf.Pixels, surf.Width, surf.Height, r);

            // Check if this affects mapped tiles - if so, use TileAwarePixelChangeItem
            if (rl.TileMapping != null && Document.TileSet != null)
            {
                var item = new TileAwarePixelChangeItem(rl, Document.TileSet, r, before, after, description);
                Document.History.Push(item);
            }
            else
            {
                PushPixelChange(rl, r, before, after, description);
            }

            // Recomposite after pixel changes
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();

            HistoryStateChanged?.Invoke();
            Document.RaiseStructureChanged();
            Document.RaiseDocumentModified(); // Notify animation panels of pixel changes
            InvalidateMainCanvas();

            // Capture keyframe if in canvas animation mode and layer has animation
            AutoCaptureKeyframeIfNeeded();
        }

        // ────────────────────────────────────────────────────────────────────
        // UNDO / REDO
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Undoes the last operation from the unified history stack.
        /// </summary>
        public void Undo()
        {
            if (!Document.History.CanUndo) return;

            // Check if next item is a structural change or selection-related
            var nextItem = Document.History.PeekUndo();
            bool isStructural = nextItem is CanvasResizeItem or LayerAddItem or LayerRemoveItem or LayerReorderItem;
            bool isSelectionTransform = nextItem is SelectionTransformItem;
            bool isSelectionCommit = nextItem is SelectionCommitItem;

            Document.History.Undo();

            // Re-sync after structural changes
            if (isStructural)
            {
                _zoom.SetDocSize(Document.PixelWidth, Document.PixelHeight);
                _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);
                EnsureComposite();
                ResetStrokeForActive();
            }

            // Selection transform/commit undo needs UI refresh
            if (isSelectionTransform || isSelectionCommit)
            {
                InvalidateMainCanvas();
                HistoryStateChanged?.Invoke();
                Document.RaiseDocumentModified(); // Notify animation panels
                return;
            }

            // Always recomposite after pixel-changing undo
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            InvalidateMainCanvas();
            HistoryStateChanged?.Invoke();
            Document.RaiseDocumentModified(); // Notify animation panels
            RaiseFrame();
        }

        /// <summary>
        /// Redoes the last undone operation from the unified history stack.
        /// </summary>
        public void Redo()
        {
            if (!Document.History.CanRedo) return;

            // Check if next item is a structural change or selection-related
            var nextItem = Document.History.PeekRedo();
            bool isStructural = nextItem is CanvasResizeItem or LayerAddItem or LayerRemoveItem or LayerReorderItem;
            bool isSelectionTransform = nextItem is SelectionTransformItem;
            bool isSelectionCommit = nextItem is SelectionCommitItem;

            Document.History.Redo();

            // Re-sync after structural changes
            if (isStructural)
            {
                _zoom.SetDocSize(Document.PixelWidth, Document.PixelHeight);
                _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);
                EnsureComposite();
                ResetStrokeForActive();
            }

            // Selection transform/commit redo needs UI refresh
            if (isSelectionTransform || isSelectionCommit)
            {
                InvalidateMainCanvas();
                HistoryStateChanged?.Invoke();
                Document.RaiseDocumentModified(); // Notify animation panels
                return;
            }

            // Always recomposite after pixel-changing redo
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            InvalidateMainCanvas();
            HistoryStateChanged?.Invoke();
            Document.RaiseDocumentModified(); // Notify animation panels
            RaiseFrame();
        }

        public void JumpHistoryTo(int appliedCount)
        {
            // appliedCount is "how many ops applied", 0 = Start
            appliedCount = Math.Clamp(appliedCount, 0, Document.History.TotalCount);

            while (Document.History.AppliedCount > appliedCount) Undo();
            while (Document.History.AppliedCount < appliedCount) Redo();
        }
    }
}
