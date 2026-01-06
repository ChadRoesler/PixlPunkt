using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Imaging;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.Animation
{
    /// <summary>
    /// Panel for previewing the stage/camera view during canvas animation.
    /// Shows what will be exported - either the stage region or full canvas.
    /// </summary>
    public sealed partial class StagePreviewPanel : UserControl
    {
        //////////////////////////////////////////////////////////////////
        // FIELDS
        //////////////////////////////////////////////////////////////////

        private CanvasDocument? _document;
        private CanvasAnimationState? _animationState;
        private SKXamlCanvas? _previewCanvas;
        private byte[]? _previewPixels;
        private int _previewWidth;
        private int _previewHeight;
        private bool _isExpanded;

        //////////////////////////////////////////////////////////////////
        // EVENTS
        //////////////////////////////////////////////////////////////////

        public event Action<bool>? ExpandRequested;

        //////////////////////////////////////////////////////////////////
        // CONSTRUCTOR
        //////////////////////////////////////////////////////////////////

        public StagePreviewPanel()
        {
            InitializeComponent();
            SetupPreviewCanvas();
        }

        private void SetupPreviewCanvas()
        {
            _previewCanvas = new SKXamlCanvas();
            _previewCanvas.PaintSurface += PreviewCanvas_PaintSurface;
            PreviewBorder.Child = _previewCanvas;
        }

        //////////////////////////////////////////////////////////////////
        // PUBLIC API
        //////////////////////////////////////////////////////////////////

        public void Bind(CanvasDocument? document, CanvasAnimationState? animationState)
        {
            if (_animationState != null)
            {
                _animationState.CurrentFrameChanged -= OnFrameChanged;
                _animationState.StageSettingsChanged -= OnStageSettingsChanged;
                _animationState.SubRoutinesChanged -= OnSubRoutinesChanged;
            }

            _document = document;
            _animationState = animationState;

            if (_animationState != null)
            {
                _animationState.CurrentFrameChanged += OnFrameChanged;
                _animationState.StageSettingsChanged += OnStageSettingsChanged;
                _animationState.SubRoutinesChanged += OnSubRoutinesChanged;

                LoadSubRoutineReels();
            }

            RefreshPreview();
        }

        private void LoadSubRoutineReels()
        {
            if (_animationState == null || _document == null)
                return;

            foreach (var subRoutine in _animationState.SubRoutines.SubRoutines)
            {
                if (subRoutine.HasReel && !subRoutine.IsLoaded)
                {
                    subRoutine.LoadReel(_document);
                }
            }
        }

        public void RefreshPreview()
        {
            if (_document == null)
            {
                _previewPixels = null;
                UpdateInfoText();
                _previewCanvas?.Invalidate();
                return;
            }

            RenderPreview();
            UpdateInfoText();
            _previewCanvas?.Invalidate();
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                ExpandIcon.Icon = value
                    ? FluentIcons.Common.Icon.ArrowMinimize
                    : FluentIcons.Common.Icon.ArrowMaximize;
                ToolTipService.SetToolTip(ExpandToggle,
                    value ? "Collapse preview" : "Expand preview");
            }
        }

        //////////////////////////////////////////////////////////////////
        // EVENT HANDLERS
        //////////////////////////////////////////////////////////////////

        private void OnFrameChanged(int frameIndex)
        {
            DispatcherQueue.TryEnqueue(RefreshPreview);
        }

        private void OnStageSettingsChanged()
        {
            DispatcherQueue.TryEnqueue(RefreshPreview);
        }

        private void OnSubRoutinesChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                LoadSubRoutineReels();
                RefreshPreview();
            });
        }

        private void ExpandToggle_Click(object sender, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
            ExpandRequested?.Invoke(IsExpanded);
        }

        private void PreviewCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(new SKColor(40, 40, 40, 255));

            if (_previewPixels == null || _previewPixels.Length < _previewWidth * _previewHeight * 4)
                return;

            float canvasW = e.Info.Width;
            float canvasH = e.Info.Height;

            if (canvasW <= 0 || canvasH <= 0) return;

            // Create bitmap from pixels
            var info = new SKImageInfo(_previewWidth, _previewHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(info);

            // Copy pixels to bitmap
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(_previewPixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                bitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);

                // Calculate destination rect (centered, uniform scale, nearest neighbor for pixel art)
                float scale = Math.Min(canvasW / _previewWidth, canvasH / _previewHeight);
                float destW = _previewWidth * scale;
                float destH = _previewHeight * scale;
                float destX = (canvasW - destW) / 2;
                float destY = (canvasH - destH) / 2;

                var destRect = new SKRect(destX, destY, destX + destW, destY + destH);
                var srcRect = new SKRect(0, 0, _previewWidth, _previewHeight);

                using var paint = new SKPaint
                {
                    FilterQuality = SKFilterQuality.None, // Nearest neighbor
                    IsAntialias = false
                };

                canvas.DrawBitmap(bitmap, srcRect, destRect, paint);
            }
            finally
            {
                handle.Free();
            }
        }

        //////////////////////////////////////////////////////////////////
        // PRIVATE METHODS
        //////////////////////////////////////////////////////////////////

        private void RenderPreview()
        {
            if (_document == null)
            {
                _previewPixels = null;
                return;
            }

            var composite = new PixelSurface(_document.PixelWidth, _document.PixelHeight);
            _document.CompositeTo(composite);

            bool useStage = _animationState?.Stage.Enabled == true;
            int currentFrame = _animationState?.CurrentFrameIndex ?? 0;

            if (useStage)
            {
                var stage = _animationState!.Stage;
                var interpolated = _animationState.StageTrack.GetInterpolatedStateAt(currentFrame);

                int stageX, stageY;
                int stageW, stageH;

                if (interpolated != null)
                {
                    float centerX = interpolated.PositionX;
                    float centerY = interpolated.PositionY;
                    
                    float scaleX = interpolated.ScaleX;
                    float scaleY = interpolated.ScaleY;
                    float captureW = stage.OutputWidth / scaleX;
                    float captureH = stage.OutputHeight / scaleY;
                    
                    stageX = (int)(centerX - captureW / 2);
                    stageY = (int)(centerY - captureH / 2);
                    stageW = (int)captureW;
                    stageH = (int)captureH;
                }
                else
                {
                    stageX = stage.StageX;
                    stageY = stage.StageY;
                    stageW = stage.StageWidth;
                    stageH = stage.StageHeight;
                }

                if (stageW <= 0 || stageH <= 0)
                {
                    _previewPixels = null;
                    return;
                }

                _previewWidth = stageW;
                _previewHeight = stageH;
                _previewPixels = new byte[stageW * stageH * 4];

                for (int y = 0; y < stageH; y++)
                {
                    for (int x = 0; x < stageW; x++)
                    {
                        int srcX = stageX + x;
                        int srcY = stageY + y;

                        int dstIdx = (y * stageW + x) * 4;

                        if (srcX >= 0 && srcX < composite.Width && srcY >= 0 && srcY < composite.Height)
                        {
                            int srcIdx = (srcY * composite.Width + srcX) * 4;

                            _previewPixels[dstIdx + 0] = composite.Pixels[srcIdx + 0];
                            _previewPixels[dstIdx + 1] = composite.Pixels[srcIdx + 1];
                            _previewPixels[dstIdx + 2] = composite.Pixels[srcIdx + 2];
                            _previewPixels[dstIdx + 3] = composite.Pixels[srcIdx + 3];
                        }
                        else
                        {
                            _previewPixels[dstIdx + 0] = 0;
                            _previewPixels[dstIdx + 1] = 0;
                            _previewPixels[dstIdx + 2] = 0;
                            _previewPixels[dstIdx + 3] = 0;
                        }
                    }
                }

                CompositeSubRoutines(_previewPixels, stageW, stageH, stageX, stageY, currentFrame);
            }
            else
            {
                _previewWidth = composite.Width;
                _previewHeight = composite.Height;
                _previewPixels = (byte[])composite.Pixels.Clone();

                CompositeSubRoutines(_previewPixels, _previewWidth, _previewHeight, 0, 0, currentFrame);
            }
        }

        private void CompositeSubRoutines(byte[] pixels, int destWidth, int destHeight, int offsetX, int offsetY, int frameIndex)
        {
            if (_animationState == null)
                return;

            _animationState.SubRoutineState.UpdateForFrame(frameIndex);

            foreach (var renderInfo in _animationState.SubRoutineState.GetRenderInfo(frameIndex))
            {
                double posX = renderInfo.PositionX - offsetX;
                double posY = renderInfo.PositionY - offsetY;

                int scaledWidth = (int)(renderInfo.FrameWidth * renderInfo.Scale);
                int scaledHeight = (int)(renderInfo.FrameHeight * renderInfo.Scale);

                if (scaledWidth <= 0 || scaledHeight <= 0)
                    continue;

                CompositeFrame(
                    pixels, destWidth, destHeight,
                    renderInfo.FramePixels, renderInfo.FrameWidth, renderInfo.FrameHeight,
                    (int)posX, (int)posY,
                    renderInfo.Scale);
            }
        }

        private static void CompositeFrame(
            byte[] dest, int destWidth, int destHeight,
            byte[] src, int srcWidth, int srcHeight,
            int destX, int destY,
            float scale)
        {
            int scaledWidth = (int)(srcWidth * scale);
            int scaledHeight = (int)(srcHeight * scale);

            for (int dy = 0; dy < scaledHeight; dy++)
            {
                int targetY = destY + dy;
                if (targetY < 0 || targetY >= destHeight)
                    continue;

                int srcY = (int)(dy / scale);
                if (srcY >= srcHeight) srcY = srcHeight - 1;

                for (int dx = 0; dx < scaledWidth; dx++)
                {
                    int targetX = destX + dx;
                    if (targetX < 0 || targetX >= destWidth)
                        continue;

                    int srcX = (int)(dx / scale);
                    if (srcX >= srcWidth) srcX = srcWidth - 1;

                    int srcIdx = (srcY * srcWidth + srcX) * 4;
                    int dstIdx = (targetY * destWidth + targetX) * 4;

                    byte srcB = src[srcIdx + 0];
                    byte srcG = src[srcIdx + 1];
                    byte srcR = src[srcIdx + 2];
                    byte srcA = src[srcIdx + 3];

                    if (srcA == 0)
                        continue;

                    if (srcA == 255)
                    {
                        dest[dstIdx + 0] = srcB;
                        dest[dstIdx + 1] = srcG;
                        dest[dstIdx + 2] = srcR;
                        dest[dstIdx + 3] = 255;
                    }
                    else
                    {
                        byte dstB = dest[dstIdx + 0];
                        byte dstG = dest[dstIdx + 1];
                        byte dstR = dest[dstIdx + 2];
                        byte dstA = dest[dstIdx + 3];

                        float srcAlpha = srcA / 255f;
                        float dstAlpha = dstA / 255f;
                        float outAlpha = srcAlpha + dstAlpha * (1 - srcAlpha);

                        if (outAlpha > 0)
                        {
                            dest[dstIdx + 0] = (byte)((srcB * srcAlpha + dstB * dstAlpha * (1 - srcAlpha)) / outAlpha);
                            dest[dstIdx + 1] = (byte)((srcG * srcAlpha + dstG * dstAlpha * (1 - srcAlpha)) / outAlpha);
                            dest[dstIdx + 2] = (byte)((srcR * srcAlpha + dstR * dstAlpha * (1 - srcAlpha)) / outAlpha);
                            dest[dstIdx + 3] = (byte)(outAlpha * 255);
                        }
                    }
                }
            }
        }

        private void UpdateInfoText()
        {
            if (_document == null)
            {
                StageInfoText.Text = "No document";
                OutputSizeText.Text = "";
                return;
            }

            bool useStage = _animationState?.Stage.Enabled == true;

            if (useStage)
            {
                var stage = _animationState!.Stage;
                StageInfoText.Text = $"Stage ({stage.StageX},{stage.StageY})";
                OutputSizeText.Text = $"{stage.OutputWidth}�{stage.OutputHeight}";
            }
            else
            {
                StageInfoText.Text = "Full Canvas";
                OutputSizeText.Text = $"{_document.PixelWidth}�{_document.PixelHeight}";
            }
        }
    }
}
