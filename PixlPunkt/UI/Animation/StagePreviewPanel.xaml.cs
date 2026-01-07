using System;
using System.Collections.Generic;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Imaging;
using Windows.Foundation;
using Windows.Graphics.DirectX;

namespace PixlPunkt.UI.Animation
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
        private CanvasControl? _previewCanvas;
        private byte[]? _previewPixels;
        private int _previewWidth;
        private int _previewHeight;
        private bool _isExpanded;

        //////////////////////////////////////////////////////////////////
        // EVENTS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raised when the expand toggle is clicked.
        /// Parameter is true when expanding, false when collapsing.
        /// </summary>
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
            _previewCanvas = new CanvasControl();
            _previewCanvas.Draw += PreviewCanvas_Draw;
            PreviewBorder.Child = _previewCanvas;
        }

        //////////////////////////////////////////////////////////////////
        // PUBLIC API
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Binds the panel to a document and its canvas animation state.
        /// </summary>
        public void Bind(CanvasDocument? document, CanvasAnimationState? animationState)
        {
            // Unbind previous
            if (_animationState != null)
            {
                _animationState.CurrentFrameChanged -= OnFrameChanged;
                _animationState.StageSettingsChanged -= OnStageSettingsChanged;
                _animationState.SubRoutinesChanged -= OnSubRoutinesChanged;
            }

            _document = document;
            _animationState = animationState;

            // Bind new
            if (_animationState != null)
            {
                _animationState.CurrentFrameChanged += OnFrameChanged;
                _animationState.StageSettingsChanged += OnStageSettingsChanged;
                _animationState.SubRoutinesChanged += OnSubRoutinesChanged;

                // Load all sub-routine reels
                LoadSubRoutineReels();
            }

            RefreshPreview();
        }

        /// <summary>
        /// Loads all sub-routine reels from their file paths.
        /// </summary>
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

        /// <summary>
        /// Refreshes the preview to reflect current document state.
        /// Call this when the canvas content changes.
        /// </summary>
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

        /// <summary>
        /// Gets or sets whether the panel is in expanded mode.
        /// </summary>
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
            // Reload sub-routine reels when they change
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

        private void PreviewCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            ds.Antialiasing = CanvasAntialiasing.Aliased;

            // Clear with transparency grid pattern
            ds.Clear(Windows.UI.Color.FromArgb(255, 40, 40, 40));

            if (_previewPixels == null || _previewPixels.Length < _previewWidth * _previewHeight * 4)
                return;

            float canvasW = (float)sender.ActualWidth;
            float canvasH = (float)sender.ActualHeight;

            if (canvasW <= 0 || canvasH <= 0) return;

            using var bitmap = CanvasBitmap.CreateFromBytes(
                sender.Device,
                _previewPixels,
                _previewWidth,
                _previewHeight,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            // Calculate destination rect (centered, uniform scale, nearest neighbor for pixel art)
            float scale = Math.Min(canvasW / _previewWidth, canvasH / _previewHeight);
            float destW = _previewWidth * scale;
            float destH = _previewHeight * scale;
            float destX = (canvasW - destW) / 2;
            float destY = (canvasH - destH) / 2;

            ds.DrawImage(
                bitmap,
                new Rect(destX, destY, destW, destH),
                new Rect(0, 0, _previewWidth, _previewHeight),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);
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

            // Composite the document
            var composite = new PixelSurface(_document.PixelWidth, _document.PixelHeight);
            _document.CompositeTo(composite);

            // Check if stage is enabled
            bool useStage = _animationState?.Stage.Enabled == true;
            int currentFrame = _animationState?.CurrentFrameIndex ?? 0;

            if (useStage)
            {
                var stage = _animationState!.Stage;

                // Apply any stage animation interpolation for current frame
                var interpolated = _animationState.StageTrack.GetInterpolatedStateAt(currentFrame);

                // Calculate effective stage position and size
                int stageX, stageY;
                int stageW, stageH;

                if (interpolated != null)
                {
                    // The keyframe stores the CENTER position of the stage (not an offset)
                    // Calculate top-left corner from the center
                    float centerX = interpolated.PositionX;
                    float centerY = interpolated.PositionY;
                    
                    // Apply scale to determine the capture area
                    // Scale > 1 = zooming in (smaller capture area)
                    // Scale < 1 = zooming out (larger capture area)
                    // The capture area is OutputSize / Scale
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
                    // No keyframes - use default stage settings (top-left position)
                    stageX = stage.StageX;
                    stageY = stage.StageY;
                    stageW = stage.StageWidth;
                    stageH = stage.StageHeight;
                }

                // Ensure we have valid dimensions
                if (stageW <= 0 || stageH <= 0)
                {
                    _previewPixels = null;
                    return;
                }

                // Extract stage region
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
                            // Outside canvas bounds - transparent
                            _previewPixels[dstIdx + 0] = 0;
                            _previewPixels[dstIdx + 1] = 0;
                            _previewPixels[dstIdx + 2] = 0;
                            _previewPixels[dstIdx + 3] = 0;
                        }
                    }
                }

                // Composite sub-routines on top (in stage space)
                CompositeSubRoutines(_previewPixels, stageW, stageH, stageX, stageY, currentFrame);
            }
            else
            {
                // Use full canvas
                _previewWidth = composite.Width;
                _previewHeight = composite.Height;
                _previewPixels = (byte[])composite.Pixels.Clone();

                // Composite sub-routines on top (in canvas space)
                CompositeSubRoutines(_previewPixels, _previewWidth, _previewHeight, 0, 0, currentFrame);
            }
        }

        /// <summary>
        /// Composites all active sub-routines onto the preview buffer.
        /// </summary>
        /// <param name="pixels">The destination pixel buffer (BGRA).</param>
        /// <param name="destWidth">The width of the destination buffer.</param>
        /// <param name="destHeight">The height of the destination buffer.</param>
        /// <param name="offsetX">The X offset (stage position) to subtract from sub-routine positions.</param>
        /// <param name="offsetY">The Y offset (stage position) to subtract from sub-routine positions.</param>
        /// <param name="frameIndex">The current animation frame index.</param>
        private void CompositeSubRoutines(byte[] pixels, int destWidth, int destHeight, int offsetX, int offsetY, int frameIndex)
        {
            if (_animationState == null)
                return;

            // Update sub-routine state for current frame
            _animationState.SubRoutineState.UpdateForFrame(frameIndex);

            // Get render info for all active sub-routines
            foreach (var renderInfo in _animationState.SubRoutineState.GetRenderInfo(frameIndex))
            {
                // Calculate position in destination buffer
                // The sub-routine position is in canvas coordinates, we need to convert to dest buffer coordinates
                double posX = renderInfo.PositionX - offsetX;
                double posY = renderInfo.PositionY - offsetY;

                // Apply scale
                int scaledWidth = (int)(renderInfo.FrameWidth * renderInfo.Scale);
                int scaledHeight = (int)(renderInfo.FrameHeight * renderInfo.Scale);

                if (scaledWidth <= 0 || scaledHeight <= 0)
                    continue;

                // For now, ignore rotation and just do position + scale
                // TODO: Add rotation support with matrix transformation

                // Composite the sub-routine frame onto the destination
                CompositeFrame(
                    pixels, destWidth, destHeight,
                    renderInfo.FramePixels, renderInfo.FrameWidth, renderInfo.FrameHeight,
                    (int)posX, (int)posY,
                    renderInfo.Scale);
            }
        }

        /// <summary>
        /// Composites a single frame onto the destination buffer with alpha blending.
        /// </summary>
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

                // Source Y with nearest-neighbor scaling
                int srcY = (int)(dy / scale);
                if (srcY >= srcHeight) srcY = srcHeight - 1;

                for (int dx = 0; dx < scaledWidth; dx++)
                {
                    int targetX = destX + dx;
                    if (targetX < 0 || targetX >= destWidth)
                        continue;

                    // Source X with nearest-neighbor scaling
                    int srcX = (int)(dx / scale);
                    if (srcX >= srcWidth) srcX = srcWidth - 1;

                    int srcIdx = (srcY * srcWidth + srcX) * 4;
                    int dstIdx = (targetY * destWidth + targetX) * 4;

                    byte srcB = src[srcIdx + 0];
                    byte srcG = src[srcIdx + 1];
                    byte srcR = src[srcIdx + 2];
                    byte srcA = src[srcIdx + 3];

                    if (srcA == 0)
                        continue; // Fully transparent, skip

                    if (srcA == 255)
                    {
                        // Fully opaque, just copy
                        dest[dstIdx + 0] = srcB;
                        dest[dstIdx + 1] = srcG;
                        dest[dstIdx + 2] = srcR;
                        dest[dstIdx + 3] = 255;
                    }
                    else
                    {
                        // Alpha blend
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
                OutputSizeText.Text = $"{stage.OutputWidth}×{stage.OutputHeight}";
            }
            else
            {
                StageInfoText.Text = "Full Canvas";
                OutputSizeText.Text = $"{_document.PixelWidth}×{_document.PixelHeight}";
            }
        }
    }
}
