using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.UI.CanvasHost;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace PixlPunkt.Uno.UI.Animation
{
    /// <summary>
    /// Panel for editing animation frames.
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

        public event Action? AddFrameRequested;
        public event Action<int, int>? FrameEditRequested;
        public event Action? TileModified;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public FrameEditPanel()
        {
            InitializeComponent();
            FrameEditorCanvas.TileModified += OnTileModified;
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        public void Bind(TileAnimationState? state, CanvasDocument? document)
        {
            if (_state != null)
            {
                _state.SelectedReelChanged -= OnSelectedReelChanged;
                _state.CurrentFrameChanged -= OnCurrentFrameChanged;

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

            if (_state != null)
            {
                _state.SelectedReelChanged += OnSelectedReelChanged;
                _state.CurrentFrameChanged += OnCurrentFrameChanged;

                if (_state.SelectedReel != null)
                {
                    _state.SelectedReel.Changed += OnReelChanged;
                }
            }
            if (_document != null)
            {
                _document.DocumentModified += OnDocumentModified;
            }

            FrameEditorCanvas.Bind(_state, _document);
            RefreshDisplay();
        }

        public void BindToolState(ToolState? toolState, PaletteService? palette)
        {
            _toolState = toolState;
            _palette = palette;
            FrameEditorCanvas.BindToolState(_toolState, _palette);
        }

        public void BindCanvasHost(CanvasViewHost? canvasHost)
        {
            FrameEditorCanvas.BindCanvasHost(canvasHost);
        }

        public void SetForegroundColor(uint bgra)
        {
            FrameEditorCanvas.SetForegroundColor(bgra);
        }

        public void SetBackgroundColor(uint bgra)
        {
            FrameEditorCanvas.SetBackgroundColor(bgra);
        }

        public void RefreshDisplay()
        {
            RefreshFrameStrip();
            RefreshFrameInfo();
            RefreshEditorVisibility();
            FrameEditorCanvas.RefreshDisplay();
        }

        public void RefreshThumbnailsOnCommit()
        {
            foreach (var child in FrameStrip.Children)
            {
                if (child is Button btn && btn.Content is SKXamlCanvas canvas)
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
                if (_state?.SelectedReel != null)
                {
                    _state.SelectedReel.Changed -= OnReelChanged;
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

        private void OnDocumentModified()
        {
            DispatcherQueue.TryEnqueue(RefreshThumbnailsOnCommit);
        }

        private void OnTileModified()
        {
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
        /// Creates a SkiaSharp canvas for crisp pixel-perfect tile rendering.
        /// </summary>
        private SKXamlCanvas? CreateTilePositionCanvas(int tileX, int tileY, double width, double height)
        {
            if (_document == null) return null;

            int tileW = _document.TileSize.Width;
            int tileH = _document.TileSize.Height;

            var canvas = new SKXamlCanvas
            {
                Width = width,
                Height = height
            };

            var doc = _document;
            int capturedTileX = tileX;
            int capturedTileY = tileY;
            int capturedTileW = tileW;
            int capturedTileH = tileH;

            canvas.PaintSurface += (sender, e) =>
            {
                var skCanvas = e.Surface.Canvas;
                skCanvas.Clear(SKColors.Transparent);

                float w = e.Info.Width;
                float h = e.Info.Height;

                byte[]? tilePixels = ReadTilePixelsFromCanvas(capturedTileX, capturedTileY, doc);
                if (tilePixels == null || tilePixels.Length < capturedTileW * capturedTileH * 4)
                    return;

                var info = new SKImageInfo(capturedTileW, capturedTileH, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var bitmap = new SKBitmap(info);

                var handle = System.Runtime.InteropServices.GCHandle.Alloc(tilePixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);

                    float scale = Math.Min(w / capturedTileW, h / capturedTileH);
                    float destW = capturedTileW * scale;
                    float destH = capturedTileH * scale;
                    float destX = (w - destW) / 2;
                    float destY = (h - destH) / 2;

                    var destRect = new SKRect(destX, destY, destX + destW, destY + destH);
                    var srcRect = new SKRect(0, 0, capturedTileW, capturedTileH);

                    using var paint = new SKPaint
                    {
                        FilterQuality = SKFilterQuality.None,
                        IsAntialias = false
                    };

                    skCanvas.DrawBitmap(bitmap, srcRect, destRect, paint);
                }
                finally
                {
                    handle.Free();
                }
            };

            return canvas;
        }

        private byte[]? ReadTilePixelsFromCanvas(int tileX, int tileY)
        {
            return ReadTilePixelsFromCanvas(tileX, tileY, _document);
        }

        private static byte[]? ReadTilePixelsFromCanvas(int tileX, int tileY, CanvasDocument? document)
        {
            if (document == null) return null;

            int tileW = document.TileSize.Width;
            int tileH = document.TileSize.Height;

            int docX = tileX * tileW;
            int docY = tileY * tileH;

            if (docX < 0 || docY < 0 ||
                docX + tileW > document.PixelWidth ||
                docY + tileH > document.PixelHeight)
            {
                return null;
            }

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
