using System;
using Microsoft.UI.Input;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.History;
using PixlPunkt.Uno.Core.Viewport;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using static PixlPunkt.Uno.Core.Helpers.GraphicsStructHelper;
using static PixlPunkt.Uno.UI.CanvasHost.Selection.SelectionSubsystem;
using SelectionRegion = PixlPunkt.Uno.Core.Selection.SelectionRegion;

namespace PixlPunkt.Uno.UI.CanvasHost.Selection
{
    /// <summary>
    /// Handles clipboard operations (copy, cut, paste, delete) for selections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionClipboard manages the following operations:
    /// - Copy: Copies selected pixels to internal and system clipboard
    /// - Cut: Copies selected pixels then clears the selection
    /// - Paste: Creates a floating selection from clipboard data
    /// - Delete: Clears selected pixels or discards floating buffer
    /// </para>
    /// <para>
    /// <strong>Clipboard Architecture</strong>: The class maintains an internal staging buffer
    /// for quick paste operations and also synchronizes with the system clipboard for
    /// cross-application support.
    /// </para>
    /// </remarks>
    public sealed class SelectionClipboard
    {
        private readonly SelectionSubsystem _state;

        // Internal clipboard staging
        private static (byte[] buf, int w, int h)? _clipboard;

        // External dependencies
        private Func<RasterLayer?>? _getActiveLayer;
        private Func<int>? _getDocWidth;
        private Func<int>? _getDocHeight;
        private Func<ZoomController>? _getZoom;
        private Func<(int x, int y, bool valid)>? _getHoverPosition;
        private Action? _requestRedraw;
        private Action? _commitFloating;
        private Action<RectInt32, Action<byte[]>>? _applyWithHistory;
        private Action<InputSystemCursorShape>? _setCursor;
        private Action<RectInt32>? _propagateTileChanges;
        private Action<SelectionChangeItem>? _pushSelectionHistory;

        /// <summary>
        /// Gets or sets the function to get the active layer.
        /// </summary>
        public Func<RasterLayer?>? GetActiveLayer
        {
            get => _getActiveLayer;
            set => _getActiveLayer = value;
        }

        /// <summary>
        /// Gets or sets the function to get document width.
        /// </summary>
        public Func<int>? GetDocWidth
        {
            get => _getDocWidth;
            set => _getDocWidth = value;
        }

        /// <summary>
        /// Gets or sets the function to get document height.
        /// </summary>
        public Func<int>? GetDocHeight
        {
            get => _getDocHeight;
            set => _getDocHeight = value;
        }

        /// <summary>
        /// Gets or sets the function to get the zoom controller.
        /// </summary>
        public Func<ZoomController>? GetZoom
        {
            get => _getZoom;
            set => _getZoom = value;
        }

        /// <summary>
        /// Gets or sets the function to get hover position.
        /// </summary>
        public Func<(int x, int y, bool valid)>? GetHoverPosition
        {
            get => _getHoverPosition;
            set => _getHoverPosition = value;
        }

        /// <summary>
        /// Gets or sets the action to request a canvas redraw.
        /// </summary>
        public Action? RequestRedraw
        {
            get => _requestRedraw;
            set => _requestRedraw = value;
        }

        /// <summary>
        /// Gets or sets the action to commit floating selection.
        /// </summary>
        public Action? CommitFloating
        {
            get => _commitFloating;
            set => _commitFloating = value;
        }

        /// <summary>
        /// Gets or sets the action to apply changes with history.
        /// </summary>
        public Action<RectInt32, Action<byte[]>>? ApplyWithHistory
        {
            get => _applyWithHistory;
            set => _applyWithHistory = value;
        }

        /// <summary>
        /// Gets or sets the action to set cursor shape.
        /// </summary>
        public Action<InputSystemCursorShape>? SetCursor
        {
            get => _setCursor;
            set => _setCursor = value;
        }

        /// <summary>
        /// Gets or sets the action to propagate changes to mapped tiles after delete.
        /// /// </summary>
        public Action<RectInt32>? PropagateTileChanges
        {
            get => _propagateTileChanges;
            set => _propagateTileChanges = value;
        }

        /// <summary>
        /// Gets or sets the action to push a selection change to history.
        /// </summary>
        public Action<SelectionChangeItem>? PushSelectionHistory
        {
            get => _pushSelectionHistory;
            set => _pushSelectionHistory = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionClipboard"/> class.
        /// </summary>
        /// <param name="state">The selection subsystem state.</param>
        public SelectionClipboard(SelectionSubsystem state)
        {
            _state = state;
        }

        /// <summary>
        /// Copies the current selection to clipboard.
        /// </summary>
        public void Copy()
        {
            if (!_state.Active) return;

            if (_state.Floating && _state.Buffer != null)
            {
                _clipboard = (_state.Buffer.AsSpan().ToArray(), _state.BufferWidth, _state.BufferHeight);
            }
            else
            {
                var rl = _getActiveLayer?.Invoke();
                if (rl != null)
                {
                    var surf = rl.Surface;
                    // Copy only selected pixels (respecting selection region, not just bounding box)
                    _clipboard = (CopySelectedPixels(surf.Pixels, surf.Width, surf.Height),
                                 _state.Rect.Width, _state.Rect.Height);
                }
            }

            TrySetSystemClipboardPng();
        }

        /// <summary>
        /// Cuts the current selection (copy then delete).
        /// </summary>
        public void Cut()
        {
            if (!_state.Active) return;
            Copy();
            Delete();
        }

        /// <summary>
        /// Pastes from clipboard into a new floating selection.
        /// </summary>
        public void Paste()
        {
            if (_clipboard == null) return;

            var rl = _getActiveLayer?.Invoke();
            if (rl == null) return;

            var (buf, w, h) = _clipboard.Value;

            int px, py;
            var hoverInfo = _getHoverPosition?.Invoke() ?? (0, 0, false);

            if (hoverInfo.valid)
            {
                px = hoverInfo.x - w / 2;
                py = hoverInfo.y - h / 2;
            }
            else
            {
                var zoom = _getZoom?.Invoke();
                if (zoom != null)
                {
                    var dest = zoom.GetDestRect();
                    var cx = (int)Math.Round(((-dest.X + dest.Width * 0.5) / zoom.Scale));
                    var cy = (int)Math.Round(((-dest.Y + dest.Height * 0.5) / zoom.Scale));
                    px = cx - w / 2;
                    py = cy - h / 2;
                }
                else
                {
                    var docW = _getDocWidth?.Invoke() ?? 100;
                    var docH = _getDocHeight?.Invoke() ?? 100;
                    px = docW / 2 - w / 2;
                    py = docH / 2 - h / 2;
                }
            }

            // Clear any existing selection region and create new one
            var docW2 = _getDocWidth?.Invoke() ?? 100;
            var docH2 = _getDocHeight?.Invoke() ?? 100;
            _state.Region.EnsureSize(docW2, docH2);
            _state.Region.Clear();

            var pasteRect = CreateRect(px, py, w, h);
            _state.Region.AddRect(pasteRect);

            _state.Active = true;
            _state.Floating = true;
            _state.Buffer = [.. buf];
            _state.BufferWidth = w;
            _state.BufferHeight = h;
            _state.FloatX = px;
            _state.FloatY = py;
            _state.Rect = pasteRect;
            _state.State = SelectionState.Armed;
            _state.PendingCs = new PixelChangeItem(rl, "Paste");

            // Set original dimensions and center
            _state.OrigW = w;
            _state.OrigH = h;
            _state.OrigCenterX = px + w / 2;
            _state.OrigCenterY = py + h / 2;

            _state.ResetTransform();
            _state.NotifyToolState();

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Deletes the current selection.
        /// </summary>
        public void Delete()
        {
            if (!_state.Active) return;

            if (_state.Floating)
            {
                // Just discard the floating buffer
                _state.Buffer = null;
                _state.Floating = false;
                _state.Active = false;
                _state.Region.Clear();
                _requestRedraw?.Invoke();
                return;
            }

            // Clear only pixels that are actually selected in the region
            var rl = _getActiveLayer?.Invoke();
            if (rl != null)
            {
                var bounds = _state.Region.Bounds;
                _applyWithHistory?.Invoke(bounds, dst =>
                {
                    ClearSelectedPixels(dst, rl.Surface.Width, rl.Surface.Height);
                });

                // Propagate deletion to mapped tiles
                _propagateTileChanges?.Invoke(bounds);
            }

            _state.Active = false;
            _state.State = SelectionState.None;
            _state.Region.Clear();
            _setCursor?.Invoke(InputSystemCursorShape.Arrow);
            _state.ToolState?.SetSelectionPresence(false, false);
            _state.ToolState?.SetRotationAngle(0.0);
        }

        /// <summary>
        /// Cancels the active selection.
        /// </summary>
        public void Cancel()
        {
            if (_state.Floating)
            {
                var rl = _getActiveLayer?.Invoke();
                if (rl != null && _state.Buffer != null)
                {
                    // Restore floating pixels to layer
                    BlitBytes(rl.Surface.Pixels, rl.Surface.Width, rl.Surface.Height,
                             _state.Rect.X, _state.Rect.Y, _state.Buffer, _state.BufferWidth, _state.BufferHeight);
                }
            }

            _state.Clear();
            _setCursor?.Invoke(InputSystemCursorShape.Arrow);
            _state.ToolState?.SetSelectionPresence(false, false);
            _state.ToolState?.SetRotationAngle(0.0);
            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Selects the entire document.
        /// </summary>
        public void SelectAll()
        {
            var docW = _getDocWidth?.Invoke() ?? 0;
            var docH = _getDocHeight?.Invoke() ?? 0;
            if (docW <= 0 || docH <= 0) return;

            var rl = _getActiveLayer?.Invoke();
            if (rl == null) return;

            // Capture before state for history
            var beforeRegion = _state.Region.Clone();

            // Kill any in-progress drag
            _state.Drag = SelDrag.None;
            _state.HavePreview = false;

            // Commit floating if needed
            if (_state.Floating)
                _commitFloating?.Invoke();

            var r = CreateRect(0, 0, docW, docH);

            _state.Region.EnsureSize(docW, docH);
            _state.Region.Clear();
            _state.Region.AddRect(r);

            _state.Rect = _state.Region.Bounds;
            _state.Active = true;
            _state.Floating = false;
            _state.Buffer = null;
            _state.State = SelectionState.Armed;
            _state.HavePreview = false;

            // Set original dimensions and center for transform handles
            _state.OrigW = docW;
            _state.OrigH = docH;
            _state.OrigCenterX = docW / 2;
            _state.OrigCenterY = docH / 2;

            _state.ResetTransform();
            _state.NotifyToolState();

            // Push history
            var afterRegion = _state.Region.Clone();
            PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind.SelectAll, beforeRegion, afterRegion);

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Inverts the current selection (selects unselected pixels, deselects selected pixels).
        /// </summary>
        public void InvertSelection()
        {
            var docW = _getDocWidth?.Invoke() ?? 0;
            var docH = _getDocHeight?.Invoke() ?? 0;
            if (docW <= 0 || docH <= 0) return;

            var rl = _getActiveLayer?.Invoke();
            if (rl == null) return;

            // If no selection, select all
            if (!_state.Active)
            {
                SelectAll();
                return;
            }

            // Capture before state for history
            var beforeRegion = _state.Region.Clone();

            // Commit floating if needed before inverting
            if (_state.Floating)
                _commitFloating?.Invoke();

            // Kill any in-progress drag
            _state.Drag = SelDrag.None;
            _state.HavePreview = false;

            // Invert the selection region
            _state.Region.Invert(docW, docH);

            // Update state
            _state.Rect = _state.Region.Bounds;
            _state.Active = !_state.Region.IsEmpty;
            _state.State = _state.Active ? SelectionState.Armed : SelectionState.None;
            _state.Floating = false;
            _state.Buffer = null;

            if (_state.Active)
            {
                // Set original dimensions and center for transform handles
                _state.OrigW = _state.Rect.Width;
                _state.OrigH = _state.Rect.Height;
                _state.OrigCenterX = _state.Rect.X + _state.Rect.Width / 2;
                _state.OrigCenterY = _state.Rect.Y + _state.Rect.Height / 2;
            }

            _state.ResetTransform();
            _state.NotifyToolState();

            // Push history
            var afterRegion = _state.Region.Clone();
            PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind.Invert, beforeRegion, afterRegion);

            _requestRedraw?.Invoke();
        }

        /// <summary>
        /// Pushes a selection change to history if there was an actual change.
        /// </summary>
        private void PushSelectionChangeHistory(
            SelectionChangeItem.SelectionChangeKind kind,
            SelectionRegion beforeRegion,
            SelectionRegion afterRegion)
        {
            var item = new SelectionChangeItem(kind, beforeRegion, afterRegion, ApplySelectionRegion);
            if (item.HasChanges)
            {
                _pushSelectionHistory?.Invoke(item);
            }
        }

        /// <summary>
        /// Applies a selection region snapshot (used for undo/redo).
        /// </summary>
        private void ApplySelectionRegion(SelectionRegion region)
        {
            var docW = _getDocWidth?.Invoke() ?? 0;
            var docH = _getDocHeight?.Invoke() ?? 0;

            // Copy the region data
            _state.Region.EnsureSize(docW, docH);
            _state.Region.Clear();

            if (!region.IsEmpty)
            {
                // Copy bounds and add as rect (simplified - for full fidelity would need pixel-by-pixel copy)
                var bounds = region.Bounds;
                // We need to iterate and copy the actual mask data
                for (int y = 0; y < docH; y++)
                {
                    for (int x = 0; x < docW; x++)
                    {
                        if (region.Contains(x, y))
                        {
                            _state.Region.AddRect(CreateRect(x, y, 1, 1));
                        }
                    }
                }
            }

            // Update state
            _state.Rect = _state.Region.Bounds;
            _state.Active = !_state.Region.IsEmpty;
            _state.State = _state.Active ? SelectionState.Armed : SelectionState.None;
            _state.Floating = false;
            _state.Buffer = null;

            if (_state.Active)
            {
                _state.OrigW = _state.Rect.Width;
                _state.OrigH = _state.Rect.Height;
                _state.OrigCenterX = _state.Rect.X + _state.Rect.Width / 2;
                _state.OrigCenterY = _state.Rect.Y + _state.Rect.Height / 2;
            }

            _state.ResetTransform();
            _state.NotifyToolState();
            _requestRedraw?.Invoke();
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static byte[] CopyRectBytes(byte[] src, int w, int h, RectInt32 r)
        {
            int x0 = Math.Max(0, r.X);
            int y0 = Math.Max(0, r.Y);
            int x1 = Math.Min(w, r.X + r.Width);
            int y1 = Math.Min(h, r.Y + r.Height);
            int rw = Math.Max(0, x1 - x0);
            int rh = Math.Max(0, y1 - y0);
            var dst = new byte[rw * rh * 4];
            if (rw == 0 || rh == 0) return dst;
            int srcStride = w * 4;
            int dstStride = rw * 4;
            for (int y = 0; y < rh; y++)
                System.Buffer.BlockCopy(src, (y0 + y) * srcStride + x0 * 4, dst, y * dstStride, dstStride);
            return dst;
        }

        private static void ClearRectBytes(byte[] dst, int w, int h, RectInt32 r)
        {
            int x0 = Math.Clamp(r.X, 0, w);
            int y0 = Math.Clamp(r.Y, 0, h);
            int x1 = Math.Clamp(r.X + r.Width, 0, w);
            int y1 = Math.Clamp(r.Y + r.Height, 0, h);
            int dstStride = w * 4;
            int bytes = (x1 - x0) * 4;
            for (int y = y0; y < y1; y++)
                Array.Clear(dst, y * dstStride + x0 * 4, bytes);
        }

        private static void BlitBytes(byte[] dst, int w, int h, int dx, int dy, byte[] buf, int bw, int bh)
        {
            int x0 = Math.Max(0, dx);
            int y0 = Math.Max(0, dy);
            int x1 = Math.Min(w, dx + bw);
            int y1 = Math.Min(h, dy + bh);
            if (x1 <= x0 || y1 <= y0) return;
            int dstStride = w * 4;
            int srcStride = bw * 4;
            for (int y = y0; y < y1; y++)
            {
                int sy = y - dy;
                System.Buffer.BlockCopy(buf, sy * srcStride + (x0 - dx) * 4, dst, y * dstStride + x0 * 4, (x1 - x0) * 4);
            }
        }

        private async void TrySetSystemClipboardPng()
        {
            try
            {
                if (_clipboard == null) return;

                var (buf, w, h) = _clipboard.Value;
                var mem = new InMemoryRandomAccessStream();
                var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, mem);
                enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)w, (uint)h, 96, 96, buf);
                await enc.FlushAsync();
                mem.Seek(0);

                var dp = new DataPackage();
                dp.SetBitmap(RandomAccessStreamReference.CreateFromStream(mem.CloneStream()));
                Clipboard.SetContent(dp);

                mem.Dispose();
            }
            catch
            {
                // Silently ignore clipboard errors
            }
        }

        /// <summary>
        /// Clears only the pixels that are selected in the region (not the bounding box).
        /// </summary>
        private void ClearSelectedPixels(byte[] dst, int w, int h)
        {
            var bounds = _state.Region.Bounds;
            int x0 = Math.Clamp(bounds.X, 0, w);
            int y0 = Math.Clamp(bounds.Y, 0, h);
            int x1 = Math.Clamp(bounds.X + bounds.Width, 0, w);
            int y1 = Math.Clamp(bounds.Y + bounds.Height, 0, h);

            int stride = w * 4;

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    // Only clear if this pixel is actually selected
                    if (_state.Region.Contains(x, y))
                    {
                        int idx = y * stride + x * 4;
                        dst[idx] = 0;     // B
                        dst[idx + 1] = 0; // G
                        dst[idx + 2] = 0; // R
                        dst[idx + 3] = 0; // A
                    }
                }
            }
        }

        /// <summary>
        /// Copies only the pixels that are selected in the region (not the entire bounding box).
        /// Unselected pixels within the bounding box are set to transparent (alpha = 0).
        /// </summary>
        private byte[] CopySelectedPixels(byte[] src, int w, int h)
        {
            var bounds = _state.Region.Bounds;
            int x0 = Math.Clamp(bounds.X, 0, w);
            int y0 = Math.Clamp(bounds.Y, 0, h);
            int x1 = Math.Clamp(bounds.X + bounds.Width, 0, w);
            int y1 = Math.Clamp(bounds.Y + bounds.Height, 0, h);
            int rw = Math.Max(0, x1 - x0);
            int rh = Math.Max(0, y1 - y0);

            var dst = new byte[rw * rh * 4];
            if (rw == 0 || rh == 0) return dst;

            int srcStride = w * 4;
            int dstStride = rw * 4;

            for (int y = 0; y < rh; y++)
            {
                int srcY = y0 + y;
                for (int x = 0; x < rw; x++)
                {
                    int srcX = x0 + x;
                    int dstIdx = y * dstStride + x * 4;

                    // Only copy if this pixel is actually selected in the region
                    if (_state.Region.Contains(srcX, srcY))
                    {
                        int srcIdx = srcY * srcStride + srcX * 4;
                        dst[dstIdx] = src[srcIdx];         // B
                        dst[dstIdx + 1] = src[srcIdx + 1]; // G
                        dst[dstIdx + 2] = src[srcIdx + 2]; // R
                        dst[dstIdx + 3] = src[srcIdx + 3]; // A
                    }
                    else
                    {
                        // Unselected pixel - make transparent
                        dst[dstIdx] = 0;
                        dst[dstIdx + 1] = 0;
                        dst[dstIdx + 2] = 0;
                        dst[dstIdx + 3] = 0;
                    }
                }
            }

            return dst;
        }
    }
}
