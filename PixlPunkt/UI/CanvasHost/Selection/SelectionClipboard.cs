using System;
using Microsoft.UI.Input;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Selection;
using PixlPunkt.Core.Viewport;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;
using static PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem;

namespace PixlPunkt.UI.CanvasHost.Selection
{
    /// <summary>
    /// Copy / cut / paste / delete / cancel / select-all / invert. Every operation that changes
    /// the document does so through <see cref="FloatingSelectionOps"/> or a history item, so
    /// each one is undoable and none of them can drop lifted pixels on the floor.
    /// </summary>
    public sealed class SelectionClipboard
    {
        private readonly SelectionSubsystem _state;

        // Internal clipboard staging
        private static (byte[] buf, int w, int h)? _clipboard;

        // External dependencies
        public Func<CanvasDocument>? GetDocument { get; set; }
        public Func<RasterLayer?>? GetActiveLayer { get; set; }
        public Func<ZoomController>? GetZoom { get; set; }
        public Func<(int x, int y, bool valid)>? GetHoverPosition { get; set; }
        public Action? RequestRedraw { get; set; }
        public Action? CommitFloating { get; set; }
        public Action<RectInt32, Action<byte[]>>? ApplyWithHistory { get; set; }
        public Action<InputSystemCursorShape>? SetCursor { get; set; }
        public Action<RectInt32>? PropagateTileChanges { get; set; }
        public Action<IHistoryItem>? PushHistory { get; set; }
        /// <summary>Called after a paste so the host can switch to a tool that can drag the float.</summary>
        public Action? EnsureSelectToolActive { get; set; }

        /// <summary>Whether anything has been copied or cut in this session.</summary>
        public static bool HasClipboard => _clipboard != null;

        /// <summary>Size of the internal clipboard image, or null when it is empty.</summary>
        public static (int w, int h)? ClipboardSize => _clipboard is { } c ? (c.w, c.h) : null;

        public SelectionClipboard(SelectionSubsystem state)
        {
            _state = state;
        }

        public void Copy()
        {
            if (!_state.Active) return;

            if (_state.Floating && _state.Buffer != null)
            {
                _clipboard = (_state.Buffer.AsSpan().ToArray(), _state.BufferWidth, _state.BufferHeight);
            }
            else
            {
                var rl = GetActiveLayer?.Invoke();
                if (rl != null)
                {
                    var surf = rl.Surface;
                    _clipboard = (CopySelectedPixels(surf.Pixels, surf.Width, surf.Height), _state.Rect.Width, _state.Rect.Height);
                }
            }
            TrySetSystemClipboardPng();
        }

        public void Cut()
        {
            if (!_state.Active) return;
            Copy();
            Delete();
        }

        /// <summary>Pastes centred on the hover point (or the view centre), kept on the canvas.</summary>
        public void Paste() => PasteCore(null);

        /// <summary>Pastes with the top-left corner at a document position.</summary>
        public void PasteAt(int x, int y) => PasteCore((x, y));

        private void PasteCore((int x, int y)? at)
        {
            if (_clipboard == null) return;
            var doc = GetDocument?.Invoke();
            var rl = GetActiveLayer?.Invoke();
            if (doc == null || rl == null) return;

            // A paste replaces whatever is floating; commit it first so nothing is lost.
            // The commit and the paste undo together.
            doc.History.BeginGroup("Paste");
            if (_state.Floating)
                CommitFloating?.Invoke();

            var (buf, w, h) = _clipboard.Value;
            int px, py;
            var hoverInfo = GetHoverPosition?.Invoke() ?? (0, 0, false);
            if (at is { } fixedAt)
            {
                px = fixedAt.x;
                py = fixedAt.y;
            }
            else if (hoverInfo.valid)
            {
                px = hoverInfo.x - w / 2;
                py = hoverInfo.y - h / 2;
            }
            else
            {
                var zoom = GetZoom?.Invoke();
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
                    px = doc.PixelWidth / 2 - w / 2;
                    py = doc.PixelHeight / 2 - h / 2;
                }
            }

            // Keep the paste on the canvas whenever it fits, so a clipboard the size of the
            // canvas lands exactly on it instead of hanging off an edge.
            if (at is null)
            {
                px = w >= doc.PixelWidth ? 0 : Math.Clamp(px, 0, doc.PixelWidth - w);
                py = h >= doc.PixelHeight ? 0 : Math.Clamp(py, 0, doc.PixelHeight - h);
            }

            _state.Drag = SelDrag.None;
            _state.HavePreview = false;

            try
            {
                var item = FloatingSelectionOps.Paste(doc, rl, buf, w, h, px, py);
                PushHistory?.Invoke(item);
            }
            finally
            {
                doc.History.EndGroup();
            }
            EnsureSelectToolActive?.Invoke();
            RequestRedraw?.Invoke();
        }

        public void Delete()
        {
            if (!_state.Active) return;
            var doc = GetDocument?.Invoke();
            if (doc == null) return;

            if (_state.Floating)
            {
                // Drop the lifted pixels; the hole they left stays. Undoable.
                var discard = FloatingSelectionOps.Discard(doc);
                if (discard != null) PushHistory?.Invoke(discard);
                SetCursor?.Invoke(InputSystemCursorShape.Arrow);
                RequestRedraw?.Invoke();
                return;
            }

            // Clear the selected pixels on the layer, then clear the selection: one undo step.
            doc.History.BeginGroup("Delete Selection");
            try
            {
                var rl = GetActiveLayer?.Invoke();
                if (rl != null)
                {
                    var bounds = _state.Region.Bounds;
                    ApplyWithHistory?.Invoke(bounds, dst => ClearSelectedPixels(dst, rl.Surface.Width, rl.Surface.Height));
                    PropagateTileChanges?.Invoke(bounds);
                }

                var before = _state.Region.Clone();
                _state.Region.Clear();
                doc.RaiseSelectionChanged();
                PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind.Clear, before, _state.Region.Clone());
            }
            finally
            {
                doc.History.EndGroup();
            }

            SetCursor?.Invoke(InputSystemCursorShape.Arrow);
            RequestRedraw?.Invoke();
        }

        public void Cancel()
        {
            var doc = GetDocument?.Invoke();
            if (doc == null) return;

            if (_state.Floating)
            {
                // Put the lifted pixels back where they came from. Undoable.
                var cancel = FloatingSelectionOps.Cancel(doc);
                if (cancel != null) PushHistory?.Invoke(cancel);
            }
            else if (_state.Active)
            {
                var before = _state.Region.Clone();
                _state.Region.Clear();
                doc.RaiseSelectionChanged();
                PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind.Clear, before, _state.Region.Clone());
            }

            _state.Drag = SelDrag.None;
            _state.HavePreview = false;
            SetCursor?.Invoke(InputSystemCursorShape.Arrow);
            RequestRedraw?.Invoke();
        }

        /// <summary>Drops the selection (committing a float first). One undo step.</summary>
        public void Deselect()
        {
            var doc = GetDocument?.Invoke();
            if (doc == null) return;
            if (!_state.Floating && _state.Region.IsEmpty) return;

            _state.Drag = SelDrag.None;
            _state.HavePreview = false;

            doc.History.BeginGroup("Deselect");
            try
            {
                if (_state.Floating)
                    CommitFloating?.Invoke();

                var before = _state.Region.Clone();
                _state.Region.Clear();
                doc.RaiseSelectionChanged();

                PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind.Clear, before, _state.Region.Clone());
            }
            finally
            {
                doc.History.EndGroup();
            }
            RequestRedraw?.Invoke();
        }

        public void SelectAll()
        {
            var doc = GetDocument?.Invoke();
            if (doc == null || doc.PixelWidth <= 0 || doc.PixelHeight <= 0) return;

            _state.Drag = SelDrag.None;
            _state.HavePreview = false;

            doc.History.BeginGroup("Select All");
            try
            {
                if (_state.Floating)
                    CommitFloating?.Invoke();

                var before = _state.Region.Clone();
                _state.Region.EnsureSize(doc.PixelWidth, doc.PixelHeight);
                _state.Region.Clear();
                _state.Region.AddRect(CreateRect(0, 0, doc.PixelWidth, doc.PixelHeight));
                doc.RaiseSelectionChanged();

                PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind.SelectAll, before, _state.Region.Clone());
            }
            finally
            {
                doc.History.EndGroup();
            }
            RequestRedraw?.Invoke();
        }

        public void InvertSelection()
        {
            var doc = GetDocument?.Invoke();
            if (doc == null || doc.PixelWidth <= 0 || doc.PixelHeight <= 0) return;

            if (!_state.Active)
            {
                SelectAll();
                return;
            }

            doc.History.BeginGroup("Invert Selection");
            try
            {
                if (_state.Floating)
                    CommitFloating?.Invoke();

                _state.Drag = SelDrag.None;
                _state.HavePreview = false;

                var before = _state.Region.Clone();
                _state.Region.Invert(doc.PixelWidth, doc.PixelHeight);
                doc.RaiseSelectionChanged();

                PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind.Invert, before, _state.Region.Clone());
            }
            finally
            {
                doc.History.EndGroup();
            }
            RequestRedraw?.Invoke();
        }

        private void PushSelectionChangeHistory(SelectionChangeItem.SelectionChangeKind kind, SelectionRegion before, SelectionRegion after)
        {
            var doc = GetDocument?.Invoke();
            if (doc == null) return;
            var item = new SelectionChangeItem(doc, kind, before, after);
            if (item.HasChanges)
                PushHistory?.Invoke(item);
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════════════════════════════

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

        private void ClearSelectedPixels(byte[] dst, int w, int h)
        {
            var bounds = _state.Region.Bounds;
            int x0 = Math.Clamp(bounds.X, 0, w);
            int y0 = Math.Clamp(bounds.Y, 0, h);
            int x1 = Math.Clamp(bounds.X + bounds.Width, 0, w);
            int y1 = Math.Clamp(bounds.Y + bounds.Height, 0, h);
            int stride = w * 4;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (_state.Region.Contains(x, y))
                    {
                        int idx = y * stride + x * 4;
                        dst[idx] = 0; dst[idx + 1] = 0; dst[idx + 2] = 0; dst[idx + 3] = 0;
                    }
        }

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
                    if (_state.Region.Contains(srcX, srcY))
                    {
                        int srcIdx = srcY * srcStride + srcX * 4;
                        dst[dstIdx] = src[srcIdx]; dst[dstIdx + 1] = src[srcIdx + 1];
                        dst[dstIdx + 2] = src[srcIdx + 2]; dst[dstIdx + 3] = src[srcIdx + 3];
                    }
                }
            }
            return dst;
        }
    }
}
