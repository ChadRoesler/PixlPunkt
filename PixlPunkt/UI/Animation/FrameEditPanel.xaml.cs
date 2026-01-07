using System;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tools;
using PixlPunkt.UI.CanvasHost;
using Windows.Foundation;
using Windows.Graphics.DirectX;

namespace PixlPunkt.UI.Animation
{
    /// <summary>
    /// Panel for editing animation frames.
    /// Shows frame strip, transport controls, and an editable frame canvas.
    /// </summary>
    public sealed partial class FrameEditPanel : UserControl
    {
        // ====================================================================
        // FIELDS
        // ====================================================================

        private TileAnimationState? _state;
        private CanvasDocument? _document;
        private ToolState? _toolState;
        private PaletteService? _palette;
        private bool _suppressFrameTimeChange;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when user wants to add a tile position as a frame.
        /// </summary>
        public event Action? AddFrameRequested;

        /// <summary>
        /// Raised when user clicks a frame to edit it.
        /// Provides the tile position (tileX, tileY) to edit.
        /// </summary>
        public event Action<int, int>? FrameEditRequested;

        /// <summary>
        /// Raised when the tile content has been modified via the editor.
        /// </summary>
        public event Action? TileModified;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public FrameEditPanel()
        {
            InitializeComponent();

            // Wire up editor canvas events
            FrameEditorCanvas.TileModified += OnTileModified;
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Binds the panel to animation state and document.
        /// </summary>
        public void Bind(TileAnimationState? state, CanvasDocument? document)
        {
            // Unbind previous
            if (_state != null)
            {
                _state.SelectedReelChanged -= OnSelectedReelChanged;
                _state.CurrentFrameChanged -= OnCurrentFrameChanged;

                // Unbind reel changed event
                if (_state.SelectedReel != null)
                {
                    _state.SelectedReel.Changed -= OnReelChanged;
                }
            }
            if (_document != null)
            {
                _document.DocumentModified -= OnDocumentModified;
            }

            _state = state;
            _document = document;

            // Bind new
            if (_state != null)
            {
                _state.SelectedReelChanged += OnSelectedReelChanged;
                _state.CurrentFrameChanged += OnCurrentFrameChanged;

                // Bind reel changed event for live updates
                if (_state.SelectedReel != null)
                {
                    _state.SelectedReel.Changed += OnReelChanged;
                }
            }
            if (_document != null)
            {
                _document.DocumentModified += OnDocumentModified;
            }

            // Bind the editor canvas
            FrameEditorCanvas.Bind(_state, _document);

            RefreshDisplay();
        }

        /// <summary>
        /// Binds the tool state and palette service for tool integration.
        /// </summary>
        public void BindToolState(ToolState? toolState, PaletteService? palette)
        {
            _toolState = toolState;
            _palette = palette;

            // Pass to the editor canvas for tool integration
            FrameEditorCanvas.BindToolState(_toolState, _palette);
        }

        /// <summary>
        /// Binds the canvas host for synchronization with the main canvas.
        /// </summary>
        public void BindCanvasHost(CanvasViewHost? canvasHost)
        {
            FrameEditorCanvas.BindCanvasHost(canvasHost);
        }

        /// <summary>
        /// Sets the foreground color for the frame editor.
        /// </summary>
        public void SetForegroundColor(uint bgra)
        {
            FrameEditorCanvas.SetForegroundColor(bgra);
        }

        /// <summary>
        /// Sets the background color for the frame editor.
        /// </summary>
        public void SetBackgroundColor(uint bgra)
        {
            FrameEditorCanvas.SetBackgroundColor(bgra);
        }

        /// <summary>
        /// Refreshes the display to reflect current state.
        /// </summary>
        public void RefreshDisplay()
        {
            RefreshFrameStrip();
            RefreshFrameInfo();
            RefreshEditorVisibility();
            FrameEditorCanvas.RefreshDisplay();
        }

        /// <summary>
        /// Refreshes frame thumbnails after a commit (like layer previews).
        /// Call this after stroke ends or document saves.
        /// </summary>
        public void RefreshThumbnailsOnCommit()
        {
            // Invalidate all thumbnail canvases to re-read pixels
            foreach (var child in FrameStrip.Children)
            {
                if (child is Button btn && btn.Content is CanvasControl canvas)
                {
                    canvas.Invalidate();
                }
            }
        }

        // ====================================================================
        // EVENT HANDLERS
        // ====================================================================

        private void OnSelectedReelChanged(TileAnimationReel? reel)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Bind to new reel's Changed event
                if (_state?.SelectedReel != null)
                {
                    _state.SelectedReel.Changed -= OnReelChanged; // Remove if already bound
                    _state.SelectedReel.Changed += OnReelChanged;
                }
                RefreshDisplay();
            });
        }

        private void OnReelChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshDisplay);
        }

        private void OnCurrentFrameChanged(int frameIndex)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshFrameInfo();
                UpdateFrameStripSelection();
                RefreshEditorVisibility();
                FrameEditorCanvas.RefreshDisplay();
            });
        }

        /// <summary>
        /// Handles document modifications from external sources (main canvas painting).
        /// Refreshes all frame thumbnails to show updated content.
        /// </summary>
        private void OnDocumentModified()
        {
            DispatcherQueue.TryEnqueue(RefreshThumbnailsOnCommit);
        }

        private void OnTileModified()
        {
            // Refresh the frame strip thumbnails on commit (stroke end)
            RefreshThumbnailsOnCommit();
            TileModified?.Invoke();
        }

        private void PrevFrameButton_Click(object sender, RoutedEventArgs e)
        {
            _state?.PreviousFrame();
        }

        private void NextFrameButton_Click(object sender, RoutedEventArgs e)
        {
            _state?.NextFrame();
        }

        private void AddFrameButton_Click(object sender, RoutedEventArgs e)
        {
            AddFrameRequested?.Invoke();
        }

        private void RemoveFrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (_state?.SelectedReel == null) return;

            int index = _state.CurrentFrameIndex;
            _state.SelectedReel.RemoveFrameAt(index);
            RefreshDisplay();
        }

        private void FrameTimeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressFrameTimeChange || _state?.SelectedReel == null) return;

            if (!double.IsNaN(args.NewValue))
            {
                int index = _state.CurrentFrameIndex;
                _state.SelectedReel.SetFrameDuration(index, (int)args.NewValue);
            }
        }

        private void FrameThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int frameIndex)
            {
                _state?.SetCurrentFrame(frameIndex);
            }
        }

        // ====================================================================
        // ONION SKINNING EVENT HANDLERS
        // ====================================================================

        private void OnionPrev2Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                FrameEditorCanvas.OnionSkinPrev2 = toggle.IsChecked == true;
            }
        }

        private void OnionPrev1Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                FrameEditorCanvas.OnionSkinPrev1 = toggle.IsChecked == true;
            }
        }

        private void OnionNext1Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                FrameEditorCanvas.OnionSkinNext1 = toggle.IsChecked == true;
            }
        }

        private void OnionNext2Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggle)
            {
                FrameEditorCanvas.OnionSkinNext2 = toggle.IsChecked == true;
            }
        }

        // ====================================================================
        // PRIVATE METHODS
        // ====================================================================

        private void RefreshFrameStrip()
        {
            FrameStrip.Children.Clear();

            if (_state?.SelectedReel == null)
            {
                FrameCountText.Text = "0 frames";
                return;
            }

            var reel = _state.SelectedReel;
            FrameCountText.Text = $"{reel.FrameCount} frames";

            // Create thumbnail buttons for each frame
            for (int i = 0; i < reel.FrameCount; i++)
            {
                var frame = reel.Frames[i];
                var thumbnail = CreateFrameThumbnail(i, frame.TileX, frame.TileY);
                FrameStrip.Children.Add(thumbnail);
            }

            UpdateFrameStripSelection();
        }

        private Button CreateFrameThumbnail(int frameIndex, int tileX, int tileY)
        {
            var btn = new Button
            {
                Width = 64,
                Height = 64,
                Padding = new Thickness(2),
                Tag = frameIndex
            };

            ToolTipService.SetToolTip(btn, $"Frame {frameIndex + 1} (Tile {tileX},{tileY})");
            btn.Click += FrameThumbnail_Click;

            // Create thumbnail canvas for crisp pixel rendering
            var canvas = CreateTilePositionCanvas(tileX, tileY, 56, 56);
            if (canvas != null)
            {
                btn.Content = canvas;
            }
            else
            {
                btn.Content = new TextBlock
                {
                    Text = $"{tileX},{tileY}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            return btn;
        }

        /// <summary>
        /// Creates a Win2D CanvasControl for crisp pixel-perfect tile rendering at a grid position.
        /// The canvas re-reads pixels on each draw, so it stays in sync when invalidated on commit.
        /// </summary>
        private CanvasControl? CreateTilePositionCanvas(int tileX, int tileY, double width, double height)
        {
            if (_document == null) return null;

            // Get tile dimensions
            int tileW = _document.TileSize.Width;
            int tileH = _document.TileSize.Height;

            var canvas = new CanvasControl
            {
                Width = width,
                Height = height
            };

            // Capture the document and tile position for the draw handler
            var doc = _document;
            int capturedTileX = tileX;
            int capturedTileY = tileY;

            canvas.Draw += (sender, args) =>
            {
                var ds = args.DrawingSession;
                ds.Antialiasing = CanvasAntialiasing.Aliased;

                float w = (float)sender.ActualWidth;
                float h = (float)sender.ActualHeight;

                // Re-read pixels fresh from the document on each draw
                byte[]? tilePixels = ReadTilePixelsFromCanvas(capturedTileX, capturedTileY, doc);
                if (tilePixels == null || tilePixels.Length < tileW * tileH * 4)
                    return;

                using var bitmap = CanvasBitmap.CreateFromBytes(
                    sender.Device,
                    tilePixels,
                    tileW,
                    tileH,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    96.0f);

                // Calculate destination rect (centered, uniform scale)
                float scale = Math.Min(w / tileW, h / tileH);
                float destW = tileW * scale;
                float destH = tileH * scale;
                float destX = (w - destW) / 2;
                float destY = (h - destH) / 2;

                ds.DrawImage(
                    bitmap,
                    new Rect(destX, destY, destW, destH),
                    new Rect(0, 0, tileW, tileH),
                    1.0f,
                    CanvasImageInterpolation.NearestNeighbor);
            };

            return canvas;
        }

        /// <summary>
        /// Reads pixels from the canvas at a tile grid position.
        /// </summary>
        private byte[]? ReadTilePixelsFromCanvas(int tileX, int tileY)
        {
            return ReadTilePixelsFromCanvas(tileX, tileY, _document);
        }

        /// <summary>
        /// Reads pixels from a document at a tile grid position.
        /// </summary>
        private static byte[]? ReadTilePixelsFromCanvas(int tileX, int tileY, CanvasDocument? document)
        {
            if (document == null) return null;

            int tileW = document.TileSize.Width;
            int tileH = document.TileSize.Height;

            // Calculate pixel coordinates
            int docX = tileX * tileW;
            int docY = tileY * tileH;

            // Ensure we're within bounds
            if (docX < 0 || docY < 0 ||
                docX + tileW > document.PixelWidth ||
                docY + tileH > document.PixelHeight)
            {
                return null;
            }

            // Read from composite surface
            var surface = document.Surface;
            if (surface == null) return null;

            byte[] tilePixels = new byte[tileW * tileH * 4];

            for (int y = 0; y < tileH; y++)
            {
                for (int x = 0; x < tileW; x++)
                {
                    int srcIdx = ((docY + y) * surface.Width + (docX + x)) * 4;
                    int dstIdx = (y * tileW + x) * 4;

                    if (srcIdx + 3 < surface.Pixels.Length && dstIdx + 3 < tilePixels.Length)
                    {
                        tilePixels[dstIdx + 0] = surface.Pixels[srcIdx + 0];
                        tilePixels[dstIdx + 1] = surface.Pixels[srcIdx + 1];
                        tilePixels[dstIdx + 2] = surface.Pixels[srcIdx + 2];
                        tilePixels[dstIdx + 3] = surface.Pixels[srcIdx + 3];
                    }
                }
            }

            return tilePixels;
        }

        private void UpdateFrameStripSelection()
        {
            if (_state == null) return;

            int currentIndex = _state.CurrentFrameIndex;

            for (int i = 0; i < FrameStrip.Children.Count; i++)
            {
                if (FrameStrip.Children[i] is Button btn)
                {
                    bool isSelected = i == currentIndex;
                    btn.BorderThickness = new Thickness(isSelected ? 2 : 1);
                    btn.BorderBrush = isSelected
                        ? new SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue)
                        : (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
                }
            }
        }

        private void RefreshFrameInfo()
        {
            if (_state?.SelectedReel == null)
            {
                CurrentFrameText.Text = "-";
                _suppressFrameTimeChange = true;
                FrameTimeBox.Value = 100;
                _suppressFrameTimeChange = false;
                return;
            }

            var reel = _state.SelectedReel;
            int current = _state.CurrentFrameIndex;
            int total = reel.FrameCount;

            CurrentFrameText.Text = total > 0
                ? $"{current + 1}/{total}"
                : "-";

            _suppressFrameTimeChange = true;
            FrameTimeBox.Value = reel.GetFrameDuration(current);
            _suppressFrameTimeChange = false;
        }

        private void RefreshEditorVisibility()
        {
            bool hasFrame = _state?.SelectedReel != null &&
                            _state.SelectedReel.FrameCount > 0 &&
                            _state.CurrentFrameIndex >= 0;

            EmptyStateText.Visibility = hasFrame ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
