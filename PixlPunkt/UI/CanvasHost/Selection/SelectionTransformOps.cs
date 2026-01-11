using System;
using PixlPunkt.Core.Tools;
using Windows.Foundation;
using static PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem;

namespace PixlPunkt.UI.CanvasHost.Selection
{
    /// <summary>
    /// Handles transform operations (scale, rotate, pivot) for selections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionTransformOps provides the mathematical calculations for interactive transform operations:
    /// - Scale handle dragging with proper anchor-based scaling
    /// - Rotation calculations around the pivot point
    /// - Pivot point positioning and snapping
    /// </para>
    /// <para>
    /// <strong>Coordinate Spaces</strong>: Transform operations work in document space but account
    /// for the cumulative rotation when computing handle positions and scale deltas.
    /// </para>
    /// </remarks>
    public sealed class SelectionTransformOps
    {
        private readonly SelectionSubsystem _state;

        // External callbacks
        private Action? _requestRedraw;
        private Func<ToolState?>? _getToolState;
        private Func<bool>? _getApplyFromTool;
        private Action<bool>? _setPushToTool;

        /// <summary>
        /// Gets or sets the action to request a canvas redraw.
        /// </summary>
        public Action? RequestRedraw
        {
            get => _requestRedraw;
            set => _requestRedraw = value;
        }

        /// <summary>
        /// Gets or sets the function to get the tool state.
        /// </summary>
        public Func<ToolState?>? GetToolState
        {
            get => _getToolState;
            set => _getToolState = value;
        }

        /// <summary>
        /// Gets or sets the function to check if applying from tool state.
        /// </summary>
        public Func<bool>? GetApplyFromTool
        {
            get => _getApplyFromTool;
            set => _getApplyFromTool = value;
        }

        /// <summary>
        /// Gets or sets the action to set push-to-tool flag.
        /// </summary>
        public Action<bool>? SetPushToTool
        {
            get => _setPushToTool;
            set => _setPushToTool = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionTransformOps"/> class.
        /// </summary>
        /// <param name="state">The selection subsystem state.</param>
        public SelectionTransformOps(SelectionSubsystem state)
        {
            _state = state;
        }

        /// <summary>
        /// Updates scale based on handle drag position.
        /// </summary>
        /// <param name="px">Pointer X in document space.</param>
        /// <param name="py">Pointer Y in document space.</param>
        /// <remarks>
        /// Uses anchor-based scaling with proper rotation support:
        /// 1. Computes handle position in global space at drag start
        /// 2. Computes new scale based on pointer movement in local space
        /// 3. Repositions selection so anchor stays fixed in global space
        /// </remarks>
        public void UpdateScaleFromHandle(int px, int py)
        {
            // Use original dimensions as base for scaling
            int baseW = _state.OrigW > 0 ? _state.OrigW : _state.BufferWidth;
            int baseH = _state.OrigH > 0 ? _state.OrigH : _state.BufferHeight;

            // Current center in global space
            double centerX = _state.ScaleStartFX + _state.ScaleStartW / 2.0;
            double centerY = _state.ScaleStartFY + _state.ScaleStartH / 2.0;

            // Rotation parameters
            double radians = _state.CumulativeAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            // Transform pointer from global to local space (relative to center)
            double globalDx = px - centerX;
            double globalDy = py - centerY;

            // Inverse rotation to get local coordinates
            double localPx = globalDx * cos + globalDy * sin;
            double localPy = -globalDx * sin + globalDy * cos;

            // Local half-dimensions at start of drag
            double halfW = _state.ScaleStartW / 2.0;
            double halfH = _state.ScaleStartH / 2.0;

            // Determine which edges move based on handle
            double localLeft = -halfW;
            double localRight = halfW;
            double localTop = -halfH;
            double localBottom = halfH;

            bool moveLeft = false, moveRight = false, moveTop = false, moveBottom = false;

            switch (_state.ActiveHandle)
            {
                case SelHandle.N: moveTop = true; break;
                case SelHandle.S: moveBottom = true; break;
                case SelHandle.E: moveRight = true; break;
                case SelHandle.W: moveLeft = true; break;
                case SelHandle.NW: moveTop = true; moveLeft = true; break;
                case SelHandle.NE: moveTop = true; moveRight = true; break;
                case SelHandle.SE: moveBottom = true; moveRight = true; break;
                case SelHandle.SW: moveBottom = true; moveLeft = true; break;
                default: return;
            }

            // Update moving edges based on pointer position in local space
            if (moveLeft) localLeft = Math.Min(localPx, localRight - 1);
            if (moveRight) localRight = Math.Max(localPx, localLeft + 1);
            if (moveTop) localTop = Math.Min(localPy, localBottom - 1);
            if (moveBottom) localBottom = Math.Max(localPy, localTop + 1);

            // Calculate new local dimensions
            double newLocalW = localRight - localLeft;
            double newLocalH = localBottom - localTop;

            // Calculate scale relative to original dimensions
            double rx = newLocalW / Math.Max(1, baseW);
            double ry = newLocalH / Math.Max(1, baseH);

            // Handle linked scaling (maintain aspect ratio)
            if (_state.ScaleLink)
            {
                bool affectsX = moveLeft || moveRight;
                bool affectsY = moveTop || moveBottom;

                double r;
                if (affectsX && affectsY)
                    r = Math.Max(rx, ry);
                else if (affectsX)
                    r = rx;
                else
                    r = ry;

                rx = ry = Math.Max(0.01, r);

                // Recalculate dimensions with linked scale
                newLocalW = baseW * rx;
                newLocalH = baseH * ry;

                // Adjust edges for linked scaling - keep anchor edge fixed
                if (moveLeft && !moveRight)
                    localLeft = localRight - newLocalW;
                else if (moveRight && !moveLeft)
                    localRight = localLeft + newLocalW;
                else if (moveLeft && moveRight)
                {
                    double midX = (localLeft + localRight) / 2;
                    localLeft = midX - newLocalW / 2;
                    localRight = midX + newLocalW / 2;
                }

                if (moveTop && !moveBottom)
                    localTop = localBottom - newLocalH;
                else if (moveBottom && !moveTop)
                    localBottom = localTop + newLocalH;
                else if (moveTop && moveBottom)
                {
                    double midY = (localTop + localBottom) / 2;
                    localTop = midY - newLocalH / 2;
                    localBottom = midY + newLocalH / 2;
                }
            }

            // Snap scale to whole percent
            int pX = Math.Max(1, (int)Math.Round(rx * 100.0));
            int pY = Math.Max(1, (int)Math.Round(ry * 100.0));
            rx = pX / 100.0;
            ry = pY / 100.0;

            // Recompute final local dimensions from snapped scale
            newLocalW = baseW * rx;
            newLocalH = baseH * ry;

            // Recalculate local edges after snapping
            if (moveLeft && !moveRight)
                localLeft = localRight - newLocalW;
            else if (moveRight && !moveLeft)
                localRight = localLeft + newLocalW;
            else
            {
                double midX = moveLeft ? (localRight - newLocalW / 2) :
                              moveRight ? (localLeft + newLocalW / 2) : 0;
                if (moveLeft && moveRight) midX = (localLeft + localRight) / 2;
                localLeft = midX - newLocalW / 2;
                localRight = midX + newLocalW / 2;
            }

            if (moveTop && !moveBottom)
                localTop = localBottom - newLocalH;
            else if (moveBottom && !moveTop)
                localBottom = localTop + newLocalH;
            else
            {
                double midY = moveTop ? (localBottom - newLocalH / 2) :
                              moveBottom ? (localTop + newLocalH / 2) : 0;
                if (moveTop && moveBottom) midY = (localTop + localBottom) / 2;
                localTop = midY - newLocalH / 2;
                localBottom = midY + newLocalH / 2;
            }

            // Calculate new local center
            double newLocalCenterX = (localLeft + localRight) / 2;
            double newLocalCenterY = (localTop + localBottom) / 2;

            // Transform new local center back to global space
            double newGlobalCenterX = centerX + newLocalCenterX * cos - newLocalCenterY * sin;
            double newGlobalCenterY = centerY + newLocalCenterX * sin + newLocalCenterY * cos;

            // Calculate new top-left in global space
            int newW = Math.Max(1, (int)Math.Round(newLocalW));
            int newH = Math.Max(1, (int)Math.Round(newLocalH));

            _state.FloatX = (int)Math.Round(newGlobalCenterX - newW / 2.0);
            _state.FloatY = (int)Math.Round(newGlobalCenterY - newH / 2.0);
            _state.ScaleX = rx;
            _state.ScaleY = ry;

            // Update the center position
            _state.OrigCenterX = (int)Math.Round(newGlobalCenterX);
            _state.OrigCenterY = (int)Math.Round(newGlobalCenterY);

            if (!(_getApplyFromTool?.Invoke() ?? false))
            {
                _setPushToTool?.Invoke(true);
                _getToolState?.Invoke()?.SetSelectionScale(pX, pY, _state.ScaleLink);
                _getToolState?.Invoke()?.SetRotationAngle(_state.AngleDeg);
                _setPushToTool?.Invoke(false);
            }

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Updates pivot position from a drag operation.
        /// </summary>
        /// <param name="docX">Pointer X in document space.</param>
        /// <param name="docY">Pointer Y in document space.</param>
        public void UpdatePivotFromDrag(int docX, int docY)
        {
            // Get current center
            double centerX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.ScaledW / 2.0);
            double centerY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.ScaledH / 2.0);

            // Transform pointer to local space (relative to center, unrotated)
            double globalDx = docX - centerX;
            double globalDy = docY - centerY;

            double radians = _state.CumulativeAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            // Inverse rotation to get local coordinates
            double localX = globalDx * cos + globalDy * sin;
            double localY = -globalDx * sin + globalDy * cos;

            // Check for snapping (8 pixels as snap distance in doc space)
            double snapDist = 8.0;
            var (snapTo, shouldSnap) = FindNearestPivotSnap(localX, localY, snapDist);

            if (shouldSnap)
            {
                var (sx, sy) = GetSnapPositionOffset(snapTo);
                _state.PivotOffsetX = sx;
                _state.PivotOffsetY = sy;
                _state.PivotSnappedTo = snapTo;
                _state.PivotCustom = (snapTo != PivotSnap.Center);
            }
            else
            {
                _state.PivotOffsetX = localX;
                _state.PivotOffsetY = localY;
                _state.PivotSnappedTo = PivotSnap.None;
                _state.PivotCustom = true;
            }

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Gets the current pivot position in document space.
        /// </summary>
        /// <returns>The pivot position (X, Y) in document coordinates.</returns>
        public (double X, double Y) GetPivotPositionDoc()
        {
            double centerX = _state.OrigCenterX != 0 ? _state.OrigCenterX : (_state.FloatX + _state.ScaledW / 2.0);
            double centerY = _state.OrigCenterY != 0 ? _state.OrigCenterY : (_state.FloatY + _state.ScaledH / 2.0);

            if (!_state.PivotCustom || (_state.PivotOffsetX == 0 && _state.PivotOffsetY == 0))
                return (centerX, centerY);

            // Transform pivot offset from local to global space
            double radians = _state.CumulativeAngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            double globalOffsetX = _state.PivotOffsetX * cos - _state.PivotOffsetY * sin;
            double globalOffsetY = _state.PivotOffsetX * sin + _state.PivotOffsetY * cos;

            return (centerX + globalOffsetX, centerY + globalOffsetY);
        }

        /// <summary>
        /// Gets the pivot position in view space.
        /// </summary>
        /// <param name="dest">The destination rect.</param>
        /// <param name="scale">The zoom scale.</param>
        /// <returns>The pivot position in view coordinates.</returns>
        public (float X, float Y) GetPivotPositionView(Rect dest, double scale)
        {
            if (_state.Drag == SelDrag.Rotate)
            {
                return ((float)(_state.RotFixedPivotX * scale + dest.X),
                        (float)(_state.RotFixedPivotY * scale + dest.Y));
            }

            var (docX, docY) = GetPivotPositionDoc();
            return ((float)(dest.X + docX * scale), (float)(dest.Y + docY * scale));
        }

        /// <summary>
        /// Gets the snap position offset for a given snap type.
        /// </summary>
        /// <param name="snap">The snap position type.</param>
        /// <returns>Offset from center in local (unrotated) space.</returns>
        public (double X, double Y) GetSnapPositionOffset(PivotSnap snap)
        {
            int handleW = (int)Math.Round((_state.OrigW > 0 ? _state.OrigW : _state.BufferWidth) * _state.ScaleX);
            int handleH = (int)Math.Round((_state.OrigH > 0 ? _state.OrigH : _state.BufferHeight) * _state.ScaleY);
            double halfW = handleW / 2.0;
            double halfH = handleH / 2.0;

            return snap switch
            {
                PivotSnap.Center => (0, 0),
                PivotSnap.NW => (-halfW, -halfH),
                PivotSnap.N => (0, -halfH),
                PivotSnap.NE => (halfW, -halfH),
                PivotSnap.E => (halfW, 0),
                PivotSnap.SE => (halfW, halfH),
                PivotSnap.S => (0, halfH),
                PivotSnap.SW => (-halfW, halfH),
                PivotSnap.W => (-halfW, 0),
                _ => (0, 0)
            };
        }

        /// <summary>
        /// Finds the nearest snap position for the given local offset.
        /// </summary>
        /// <param name="localX">Local X offset from center.</param>
        /// <param name="localY">Local Y offset from center.</param>
        /// <param name="snapDistance">Maximum snap distance.</param>
        /// <returns>Snap type and whether snapping should occur.</returns>
        public (PivotSnap snap, bool shouldSnap) FindNearestPivotSnap(double localX, double localY, double snapDistance)
        {
            var snaps = new[] { PivotSnap.Center, PivotSnap.NW, PivotSnap.N, PivotSnap.NE,
                               PivotSnap.E, PivotSnap.SE, PivotSnap.S, PivotSnap.SW, PivotSnap.W };

            PivotSnap nearest = PivotSnap.None;
            double minDist = double.MaxValue;

            foreach (var snap in snaps)
            {
                var (sx, sy) = GetSnapPositionOffset(snap);
                double dx = localX - sx;
                double dy = localY - sy;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = snap;
                }
            }

            return (nearest, minDist <= snapDistance);
        }

        /// <summary>
        /// Gets the local handle position for a given handle.
        /// </summary>
        /// <param name="handle">The handle type.</param>
        /// <param name="fx">Floating X position.</param>
        /// <param name="fy">Floating Y position.</param>
        /// <param name="w">Width.</param>
        /// <param name="h">Height.</param>
        /// <returns>Local position (X, Y) relative to center.</returns>
        public static (double X, double Y) GetLocalHandlePosition(SelHandle handle, int fx, int fy, int w, int h)
        {
            return handle switch
            {
                SelHandle.NW => (-w / 2.0, -h / 2.0),
                SelHandle.N => (0, -h / 2.0),
                SelHandle.NE => (w / 2.0, -h / 2.0),
                SelHandle.E => (w / 2.0, 0),
                SelHandle.SE => (w / 2.0, h / 2.0),
                SelHandle.S => (0, h / 2.0),
                SelHandle.SW => (-w / 2.0, h / 2.0),
                SelHandle.W => (-w / 2.0, 0),
                _ => (0, 0)
            };
        }

        /// <summary>
        /// Sets selection scale in percent, respecting link behavior.
        /// Also updates the floating position to keep the selection centered.
        /// </summary>
        /// <param name="percentX">X scale percent.</param>
        /// <param name="percentY">Y scale percent.</param>
        /// <param name="link">Whether to link X and Y scales.</param>
        public void SetScale(double percentX, double percentY, bool link)
        {
            _state.ScaleLink = link;

            percentX = Math.Max(1.0, Math.Round(percentX));
            percentY = link ? percentX : Math.Max(1.0, Math.Round(percentY));

            double oldScaleX = _state.ScaleX;
            double oldScaleY = _state.ScaleY;

            _state.ScaleX = percentX / 100.0;
            _state.ScaleY = percentY / 100.0;

            // Update floating position to keep the selection centered around its center point
            if (_state.Floating && _state.Buffer != null)
            {
                // Get current center
                int centerX = _state.OrigCenterX;
                int centerY = _state.OrigCenterY;

                if (centerX == 0 && centerY == 0)
                {
                    // Calculate center from current position if not set
                    int baseW = _state.OrigW > 0 ? _state.OrigW : _state.BufferWidth;
                    int baseH = _state.OrigH > 0 ? _state.OrigH : _state.BufferHeight;
                    int oldW = (int)Math.Round(baseW * oldScaleX);
                    int oldH = (int)Math.Round(baseH * oldScaleY);
                    centerX = _state.FloatX + oldW / 2;
                    centerY = _state.FloatY + oldH / 2;
                    _state.OrigCenterX = centerX;
                    _state.OrigCenterY = centerY;
                }

                // Calculate new dimensions
                int baseWidth = _state.OrigW > 0 ? _state.OrigW : _state.BufferWidth;
                int baseHeight = _state.OrigH > 0 ? _state.OrigH : _state.BufferHeight;
                int newW = (int)Math.Round(baseWidth * _state.ScaleX);
                int newH = (int)Math.Round(baseHeight * _state.ScaleY);

                // Update float position to keep centered
                _state.FloatX = centerX - newW / 2;
                _state.FloatY = centerY - newH / 2;
            }

            _requestRedraw?.Invoke();

            if (!(_getApplyFromTool?.Invoke() ?? false))
            {
                _getToolState?.Invoke()?.SetSelectionScale(percentX, percentY, link);
            }
        }

        /// <summary>
        /// Flips the selection buffer horizontally.
        /// </summary>
        /// <param name="useGlobalAxis">
        /// If true (global): Flips on canvas X axis - simple horizontal mirror, ignoring rotation.
        /// If false (local): Flips on object's local X axis - accounts for rotation by also negating rotation.
        /// </param>
        public void FlipHorizontal(bool useGlobalAxis)
        {
            if (!_state.Floating || _state.Buffer == null || _state.BufferWidth <= 0 || _state.BufferHeight <= 0)
                return;

            if (useGlobalAxis)
            {
                // Global axis: Flip on canvas X axis (left-right mirror)
                // This ignores the object's rotation - just flip the buffer directly
                // The visual result is a mirror on the canvas's vertical center line
                FlipBufferHorizontal(_state.Buffer, _state.BufferWidth, _state.BufferHeight);
                // Negate rotation to flip on the object's own axis
                if (Math.Abs(_state.CumulativeAngleDeg) > 0.1)
                {
                    _state.CumulativeAngleDeg = -_state.CumulativeAngleDeg;
                }
            }
            else
            {
                // Local axis: Flip on object's local X axis
                // Since the buffer stores pre-rotation content, we flip the buffer
                // AND negate the rotation to achieve a local flip effect
                FlipBufferHorizontal(_state.Buffer, _state.BufferWidth, _state.BufferHeight);
            }

            _state.Changed = true;
            _state.BufferFlipped = true; // Mark that buffer differs from original region
            _state.PreviewBuf = null; // Clear preview to force regeneration

            // Trigger immediate preview rebuild by setting preview params to invalid values
            // This ensures the next Draw call will rebuild the preview
            _state.PreviewScaleX = -1;
            _state.PreviewScaleY = -1;

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Flips the selection buffer vertically.
        /// </summary>
        /// <param name="useGlobalAxis">
        /// If true (global): Flips on canvas Y axis - simple vertical mirror, ignoring rotation.
        /// If false (local): Flips on object's local Y axis - accounts for rotation by also negating rotation.
        /// </param>
        public void FlipVertical(bool useGlobalAxis)
        {
            if (!_state.Floating || _state.Buffer == null || _state.BufferWidth <= 0 || _state.BufferHeight <= 0)
                return;

            if (useGlobalAxis)
            {
                // Global axis: Flip on canvas Y axis (top-bottom mirror)
                // This ignores the object's rotation - just flip the buffer directly
                // The visual result is a mirror on the canvas's horizontal center line
                // AND negate the rotation to achieve a local flip effect
                FlipBufferVertical(_state.Buffer, _state.BufferWidth, _state.BufferHeight);

                // Negate rotation to flip on the object's own axis
                if (Math.Abs(_state.CumulativeAngleDeg) > 0.1)
                {
                    _state.CumulativeAngleDeg = -_state.CumulativeAngleDeg;
                }
            }
            else
            {
                // Local axis: Flip on object's local Y axis
                // Since the buffer stores pre-rotation content, we flip the buffer

                FlipBufferVertical(_state.Buffer, _state.BufferWidth, _state.BufferHeight);
            }

            _state.Changed = true;
            _state.BufferFlipped = true; // Mark that buffer differs from original region
            _state.PreviewBuf = null; // Clear preview to force regeneration

            // Trigger immediate preview rebuild by setting preview params to invalid values
            // This ensures the next Draw call will rebuild the preview
            _state.PreviewScaleX = -1;
            _state.PreviewScaleY = -1;

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Flips a BGRA buffer horizontally (mirror left-right).
        /// </summary>
        private static void FlipBufferHorizontal(byte[] buf, int w, int h)
        {
            int stride = w * 4;
            for (int y = 0; y < h; y++)
            {
                int rowStart = y * stride;
                for (int x = 0; x < w / 2; x++)
                {
                    int left = rowStart + x * 4;
                    int right = rowStart + (w - 1 - x) * 4;

                    // Swap pixels
                    for (int c = 0; c < 4; c++)
                    {
                        byte tmp = buf[left + c];
                        buf[left + c] = buf[right + c];
                        buf[right + c] = tmp;
                    }
                }
            }
        }

        /// <summary>
        /// Flips a BGRA buffer vertically (mirror top-bottom).
        /// </summary>
        private static void FlipBufferVertical(byte[] buf, int w, int h)
        {
            int stride = w * 4;
            var rowBuf = new byte[stride];

            for (int y = 0; y < h / 2; y++)
            {
                int topRow = y * stride;
                int bottomRow = (h - 1 - y) * stride;

                // Swap rows
                Buffer.BlockCopy(buf, topRow, rowBuf, 0, stride);
                Buffer.BlockCopy(buf, bottomRow, buf, topRow, stride);
                Buffer.BlockCopy(rowBuf, 0, buf, bottomRow, stride);
            }
        }
    }
}
