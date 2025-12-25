using System;
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
            }

            _document = document;
            _animationState = animationState;

            // Bind new
            if (_animationState != null)
            {
                _animationState.CurrentFrameChanged += OnFrameChanged;
                _animationState.StageSettingsChanged += OnStageSettingsChanged;
            }

            RefreshPreview();
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

            if (useStage)
            {
                var stage = _animationState!.Stage;

                // Apply any stage animation interpolation for current frame
                int currentFrame = _animationState.CurrentFrameIndex;
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
            }
            else
            {
                // Use full canvas
                _previewWidth = composite.Width;
                _previewHeight = composite.Height;
                _previewPixels = (byte[])composite.Pixels.Clone();
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
