using System;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Rendering subsystem for CanvasViewHost:
    /// - Main draw pipeline
    /// - Background/surface rendering
    /// - Grid overlays (pixel, tile)
    /// - Guide overlays
    /// - Stripe brush management
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // Guide colors
        private static readonly Color GuideColor = Color.FromArgb(200, 0, 180, 255);
        private static readonly Color GuideColorSelected = Color.FromArgb(255, 255, 100, 100);

        // Stage overlay colors
        private static readonly Color StageOutlineColor = Color.FromArgb(255, 255, 165, 0); // Orange
        private static readonly Color StageOutlineSelectedColor = Color.FromArgb(255, 255, 80, 80); // Red when selected
        private static readonly Color StageDimColor = Color.FromArgb(120, 0, 0, 0); // Semi-transparent black for dimming
        private static readonly Color StageCornerColor = Color.FromArgb(255, 255, 200, 100); // Light orange for corners
        private static readonly Color StageCornerSelectedColor = Color.FromArgb(255, 255, 120, 120); // Light red when selected

        // Tile animation frame overlay colors
        private static readonly Color TileAnimFrameColor = Color.FromArgb(100, 100, 200, 255);      // Blue fill for frames in reel
        private static readonly Color TileAnimCurrentFrameColor = Color.FromArgb(180, 255, 200, 100); // Yellow fill for current frame
        private static readonly Color TileAnimFrameOutline = Color.FromArgb(200, 100, 150, 255);    // Blue outline
        private static readonly Color TileAnimCurrentOutline = Color.FromArgb(255, 255, 180, 0);    // Orange outline for current

        // Stage visibility
        private bool _showStageOverlay = true;

        /// <summary>
        /// Gets or sets whether the stage overlay is visible.
        /// </summary>
        public bool ShowStageOverlay
        {
            get => _showStageOverlay;
            set
            {
                if (_showStageOverlay != value)
                {
                    _showStageOverlay = value;
                    InvalidateCanvas();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether tile animation frame mappings are shown.
        /// Highlights tiles that are part of the current animation reel.
        /// </summary>
        public bool ShowTileAnimationMappings
        {
            get => _showTileAnimationMappings;
            set
            {
                if (_showTileAnimationMappings != value)
                {
                    _showTileAnimationMappings = value;
                    InvalidateCanvas();
                }
            }
        }

        private Color GetThemeClearColor()
        {
            // Check the actual theme of the control
            var theme = ActualTheme;
            return theme == Microsoft.UI.Xaml.ElementTheme.Light
                ? Color.FromArgb(255, 249, 249, 249)  // Light theme background
                : Color.FromArgb(255, 24, 24, 24);     // Dark theme background
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - MAIN DRAW
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Per-frame drawing pipeline: background → reference layers (below) → composite → reference layers (above) → overlays.</summary>
        private void CanvasView_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var ds = args.DrawingSession;
            var clearColor = GetThemeClearColor();
            ds.Clear(clearColor);

            var dest = _zoom.GetDestRect();
            ds.Antialiasing = CanvasAntialiasing.Aliased;

            var dpiScale = CanvasView.XamlRoot?.RasterizationScale ?? 1.0;
            var bg = _patternService.GetSizedImage(sender.Device, dpiScale, (float)dest.Width, (float)dest.Height);

            float tx = (float)Math.Floor(dest.X);
            float ty = (float)Math.Floor(dest.Y);
            var tgt = new Rect(tx, ty, dest.Width, dest.Height);
            var src = new Rect(0, 0, bg.SizeInPixels.Width, bg.SizeInPixels.Height);
            ds.DrawImage(bg, tgt, src);

            // Draw reference layers that are below the current composite position
            DrawReferenceLayers(ds, sender.Device, dest, renderBelow: true);

            // Draw onion skin frames (before current frame)
            DrawOnionSkinFrames(ds, sender.Device, dest);

            EnsureComposite();
            Document.CompositeTo(_composite!);

            FrameReady?.Invoke(_composite!.Pixels, _composite.Width, _composite.Height);

            DrawDocumentSurface(ds, sender.Device, dest);
            DrawDocumentBorder(ds, dest);

            // Draw reference layers that are above the current composite position
            DrawReferenceLayers(ds, sender.Device, dest, renderBelow: false);

            if (_shapeDrag)
                DrawShapePreview(ds, dest);

            if (_gradientDrag)
                DrawGradientFillPreview(ds, dest);

            DrawBrushCursorOverlay(ds, dest);

            if (_showPixelGrid) DrawPixelGrid(ds, dest);
            if (_showTileGrid) DrawTileGrid(ds, dest);
            if (_showTileMappings) DrawTileMappings(ds, dest);
            if (_showTileAnimationMappings) DrawTileAnimationFrames(ds, dest);

            // Draw stage overlay (camera viewport)
            if (_showStageOverlay) DrawStageOverlay(ds, dest);

            // Draw guides
            DrawGuides(ds, dest);

            Selection_Draw(sender, args);

            // Invalidate rulers when canvas redraws (for cursor position sync)
            HorizontalRulerCanvas?.Invalidate();
            VerticalRulerCanvas?.Invalidate();
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - REFERENCE LAYERS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws reference layers from the layer stack.
        /// </summary>
        /// <param name="ds">The drawing session.</param>
        /// <param name="device">The canvas device.</param>
        /// <param name="dest">The destination rectangle.</param>
        /// <param name="renderBelow">If true, draws reference layers below the first raster layer; otherwise draws those above.</param>
        private void DrawReferenceLayers(CanvasDrawingSession ds, CanvasDevice device, Rect dest, bool renderBelow)
        {
            if (Document == null) return;

            float scale = (float)_zoom.Scale;
            var rootItems = Document.RootItems;

            // Find the position of the first (bottom-most) raster layer
            int firstRasterIndex = -1;
            for (int i = 0; i < rootItems.Count; i++)
            {
                if (rootItems[i] is RasterLayer || rootItems[i] is LayerFolder)
                {
                    firstRasterIndex = i;
                    break;
                }
            }

            // Draw reference layers based on their position in the stack
            for (int i = 0; i < rootItems.Count; i++)
            {
                if (rootItems[i] is not ReferenceLayer refLayer)
                    continue;

                // Skip hidden layers
                if (!refLayer.IsEffectivelyVisible())
                    continue;

                // Determine if this reference layer is below or above the raster content
                bool isBelow = firstRasterIndex < 0 || i < firstRasterIndex;

                if (isBelow != renderBelow)
                    continue;

                DrawSingleReferenceLayer(ds, device, dest, refLayer, scale);
            }

            // Also draw reference layers inside folders
            foreach (var item in rootItems)
            {
                if (item is LayerFolder folder)
                {
                    DrawReferencLayersInFolder(ds, device, dest, folder, scale, renderBelow, firstRasterIndex);
                }
            }
        }

        private void DrawReferencLayersInFolder(CanvasDrawingSession ds, CanvasDevice device, Rect dest, 
            LayerFolder folder, float scale, bool renderBelow, int firstRasterIndex)
        {
            foreach (var child in folder.Children)
            {
                if (child is ReferenceLayer refLayer && refLayer.IsEffectivelyVisible())
                {
                    // Reference layers in folders render in front of canvas content
                    if (!renderBelow)
                    {
                        DrawSingleReferenceLayer(ds, device, dest, refLayer, scale);
                    }
                }
                else if (child is LayerFolder subFolder)
                {
                    DrawReferencLayersInFolder(ds, device, dest, subFolder, scale, renderBelow, firstRasterIndex);
                }
            }
        }

        /// <summary>
        /// Draws a single reference layer.
        /// </summary>
        private void DrawSingleReferenceLayer(CanvasDrawingSession ds, CanvasDevice device, Rect dest, ReferenceLayer refLayer, float scale)
        {
            if (refLayer.Pixels == null || refLayer.ImageWidth <= 0 || refLayer.ImageHeight <= 0)
                return;

            try
            {
                using var bmp = CanvasBitmap.CreateFromBytes(
                    device,
                    refLayer.Pixels,
                    refLayer.ImageWidth,
                    refLayer.ImageHeight,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    96.0f);

                float imgScale = refLayer.Scale * scale;
                float screenX = (float)(dest.X + refLayer.PositionX * scale);
                float screenY = (float)(dest.Y + refLayer.PositionY * scale);
                float screenW = refLayer.ImageWidth * imgScale;
                float screenH = refLayer.ImageHeight * imgScale;

                var interpolation = CanvasImageInterpolation.HighQualityCubic;
                float opacity = refLayer.Opacity / 255f;

                if (Math.Abs(refLayer.Rotation) < 0.01f)
                {
                    ds.DrawImage(
                        bmp,
                        new Rect(screenX, screenY, screenW, screenH),
                        new Rect(0, 0, refLayer.ImageWidth, refLayer.ImageHeight),
                        opacity,
                        interpolation);
                }
                else
                {
                    float centerX = screenX + screenW / 2f;
                    float centerY = screenY + screenH / 2f;
                    float radians = refLayer.Rotation * MathF.PI / 180f;

                    var oldTransform = ds.Transform;
                    ds.Transform = Matrix3x2.CreateRotation(radians, new Vector2(centerX, centerY));

                    ds.DrawImage(
                        bmp,
                        new Rect(screenX, screenY, screenW, screenH),
                        new Rect(0, 0, refLayer.ImageWidth, refLayer.ImageHeight),
                        opacity,
                        interpolation);

                    ds.Transform = oldTransform;
                }

                // Draw selection border if this is the selected reference layer
                if (_selectedReferenceLayer == refLayer)
                {
                    DrawReferenceLayerSelection(ds, screenX, screenY, screenW, screenH, refLayer.Rotation, refLayer.Locked);
                }
            }
            catch
            {
                // Skip layers that fail to render
            }
        }

        /// <summary>
        /// Draws selection handles around a selected reference layer.
        /// </summary>
        private void DrawReferenceLayerSelection(CanvasDrawingSession ds, float x, float y, float w, float h, float rotation, bool isLocked)
        {
            var outlineColor = isLocked ? Color.FromArgb(200, 128, 128, 128) : Color.FromArgb(255, 0, 200, 255);
            var handleColor = isLocked ? Color.FromArgb(200, 100, 100, 100) : Color.FromArgb(255, 255, 255, 255);
            var edgeHandleColor = isLocked ? Color.FromArgb(200, 100, 100, 100) : Color.FromArgb(255, 200, 255, 200); // Light green for edge handles
            var rotationHandleColor = isLocked ? Color.FromArgb(200, 100, 100, 100) : Color.FromArgb(255, 255, 200, 100);
            float outlineWidth = 2f;
            float handleSize = 8f;
            float edgeHandleSize = 6f; // Slightly smaller for edge handles
            float rotationHandleOffset = 20f; // Distance from corner for rotation handle

            if (Math.Abs(rotation) < 0.01f)
            {
                ds.DrawRectangle(x, y, w, h, outlineColor, outlineWidth);

                if (!isLocked)
                {
                    // Draw corner handles (for resize)
                    DrawReferenceHandle(ds, x, y, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(ds, x + w, y, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(ds, x, y + h, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(ds, x + w, y + h, handleSize, handleColor, outlineColor);

                    // Draw edge midpoint handles (for when corners are off-screen)
                    float midX = x + w / 2f;
                    float midY = y + h / 2f;
                    DrawReferenceHandle(ds, midX, y, edgeHandleSize, edgeHandleColor, outlineColor);        // Top
                    DrawReferenceHandle(ds, x + w, midY, edgeHandleSize, edgeHandleColor, outlineColor);    // Right
                    DrawReferenceHandle(ds, midX, y + h, edgeHandleSize, edgeHandleColor, outlineColor);    // Bottom
                    DrawReferenceHandle(ds, x, midY, edgeHandleSize, edgeHandleColor, outlineColor);        // Left

                    // Draw rotation handles (outside corners)
                    float cx = x + w / 2f;
                    float cy = y + h / 2f;
                    DrawRotationHandle(ds, x, y, cx, cy, rotationHandleOffset, rotationHandleColor, outlineColor);
                    DrawRotationHandle(ds, x + w, y, cx, cy, rotationHandleOffset, rotationHandleColor, outlineColor);
                    DrawRotationHandle(ds, x, y + h, cx, cy, rotationHandleOffset, rotationHandleColor, outlineColor);
                    DrawRotationHandle(ds, x + w, y + h, cx, cy, rotationHandleOffset, rotationHandleColor, outlineColor);
                }
            }
            else
            {
                float centerX = x + w / 2f;
                float centerY = y + h / 2f;
                float radians = rotation * MathF.PI / 180f;
                float cos = MathF.Cos(radians);
                float sin = MathF.Sin(radians);

                var corners = new Vector2[4];
                var localCorners = new Vector2[]
                {
                    new(x - centerX, y - centerY),
                    new(x + w - centerX, y - centerY),
                    new(x + w - centerX, y + h - centerY),
                    new(x - centerX, y + h - centerY)
                };

                for (int i = 0; i < 4; i++)
                {
                    float rx = localCorners[i].X * cos - localCorners[i].Y * sin;
                    float ry = localCorners[i].X * sin + localCorners[i].Y * cos;
                    corners[i] = new Vector2(centerX + rx, centerY + ry);
                }

                for (int i = 0; i < 4; i++)
                {
                    int next = (i + 1) % 4;
                    ds.DrawLine(corners[i], corners[next], outlineColor, outlineWidth);
                }

                if (!isLocked)
                {
                    // Draw corner handles (for resize)
                    foreach (var corner in corners)
                    {
                        DrawReferenceHandle(ds, corner.X, corner.Y, handleSize, handleColor, outlineColor);
                    }

                    // Draw edge midpoint handles (for when corners are off-screen)
                    // Calculate rotated edge midpoints
                    var localMidpoints = new Vector2[]
                    {
                        new(0, -h / 2f),      // Top
                        new(w / 2f, 0),       // Right
                        new(0, h / 2f),       // Bottom
                        new(-w / 2f, 0)       // Left
                    };

                    foreach (var localMid in localMidpoints)
                    {
                        float rx = localMid.X * cos - localMid.Y * sin;
                        float ry = localMid.X * sin + localMid.Y * cos;
                        DrawReferenceHandle(ds, centerX + rx, centerY + ry, edgeHandleSize, edgeHandleColor, outlineColor);
                    }

                    // Draw rotation handles (outside corners)
                    foreach (var corner in corners)
                    {
                        DrawRotationHandle(ds, corner.X, corner.Y, centerX, centerY, rotationHandleOffset, rotationHandleColor, outlineColor);
                    }
                }
            }
        }

        /// <summary>
        /// Draws a rotation handle at a position extended outward from the center.
        /// </summary>
        private static void DrawRotationHandle(CanvasDrawingSession ds, float cornerX, float cornerY, float centerX, float centerY, float offset, Color fill, Color stroke)
        {
            // Calculate direction from center to corner
            float dirX = cornerX - centerX;
            float dirY = cornerY - centerY;
            float len = MathF.Sqrt(dirX * dirX + dirY * dirY);
            if (len < 0.001f) return;

            // Normalize and extend
            dirX /= len;
            dirY /= len;
            float handleX = cornerX + dirX * offset;
            float handleY = cornerY + dirY * offset;

            // Draw line from corner to rotation handle
            ds.DrawLine(cornerX, cornerY, handleX, handleY, stroke, 1f);

            // Draw circular rotation handle
            float radius = 5f;
            ds.FillEllipse(handleX, handleY, radius, radius, fill);
            ds.DrawEllipse(handleX, handleY, radius, radius, stroke, 1f);
        }

        // Keep original DrawReferenceImages for backward compatibility with ReferenceImageService
        /// <summary>
        /// Draws reference image overlays on the canvas (from ReferenceImageService - legacy).
        /// </summary>
        /// <param name="ds">The drawing session.</param>
        /// <param name="device">The canvas device.</param>
        /// <param name="dest">The destination rectangle.</param>
        /// <param name="renderBehind">If true, draws images with RenderBehind=true; otherwise draws those with RenderBehind=false.</param>
        private void DrawReferenceImages(CanvasDrawingSession ds, CanvasDevice device, Rect dest, bool renderBehind)
        {
            // ...existing code... (keep for backward compat with ReferenceImageService if needed)
            var refService = Document?.ReferenceImages;
            if (refService == null || !refService.OverlaysVisible)
                return;

            float scale = (float)_zoom.Scale;

            foreach (var refImage in refService.GetVisibleImages())
            {
                // Filter by render order
                if (refImage.RenderBehind != renderBehind)
                    continue;

                if (refImage.Pixels == null || refImage.Width <= 0 || refImage.Height <= 0)
                    continue;

                try
                {
                    // Create bitmap from reference image pixels
                    using var bmp = CanvasBitmap.CreateFromBytes(
                        device,
                        refImage.Pixels,
                        refImage.Width,
                        refImage.Height,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        96.0f);

                    // Calculate screen position and size
                    // Note: Reference images are NOT clipped to canvas bounds
                    float imgScale = refImage.Scale * scale;
                    float screenX = (float)(dest.X + refImage.PositionX * scale);
                    float screenY = (float)(dest.Y + refImage.PositionY * scale);
                    float screenW = refImage.Width * imgScale;
                    float screenH = refImage.Height * imgScale;

                    // Use smooth interpolation for reference images (not pixel-snapped)
                    var interpolation = CanvasImageInterpolation.HighQualityCubic;

                    // Draw with opacity and optional rotation
                    if (Math.Abs(refImage.Rotation) < 0.01f)
                    {
                        // No rotation - simple draw
                        ds.DrawImage(
                            bmp,
                            new Rect(screenX, screenY, screenW, screenH),
                            new Rect(0, 0, refImage.Width, refImage.Height),
                            refImage.Opacity,
                            interpolation);
                    }
                    else
                    {
                        // With rotation - use transform
                        float centerX = screenX + screenW / 2f;
                        float centerY = screenY + screenH / 2f;
                        float radians = refImage.Rotation * MathF.PI / 180f;

                        var oldTransform = ds.Transform;
                        ds.Transform = Matrix3x2.CreateRotation(radians, new Vector2(centerX, centerY));

                        ds.DrawImage(
                            bmp,
                            new Rect(screenX, screenY, screenW, screenH),
                            new Rect(0, 0, refImage.Width, refImage.Height),
                            refImage.Opacity,
                            interpolation);

                        ds.Transform = oldTransform;
                    }

                    // Draw selection border if this is the selected reference image
                    if (refService.SelectedImage == refImage)
                    {
                        DrawReferenceImageSelection(ds, screenX, screenY, screenW, screenH, refImage.Rotation, refImage.IsLocked);
                    }
                }
                catch
                {
                    // Skip images that fail to render
                }
            }
        }

        /// <summary>
        /// Draws selection handles around a selected reference image.
        /// </summary>
        private void DrawReferenceImageSelection(CanvasDrawingSession ds, float x, float y, float w, float h, float rotation, bool isLocked)
        {
            var outlineColor = isLocked ? Color.FromArgb(200, 128, 128, 128) : Color.FromArgb(255, 0, 200, 255);
            var handleColor = isLocked ? Color.FromArgb(200, 100, 100, 100) : Color.FromArgb(255, 255, 255, 255);
            float outlineWidth = 2f;
            float handleSize = 8f;

            if (Math.Abs(rotation) < 0.01f)
            {
                // No rotation - draw simple rectangle
                ds.DrawRectangle(x, y, w, h, outlineColor, outlineWidth);

                if (!isLocked)
                {
                    // Draw corner handles
                    DrawReferenceHandle(ds, x, y, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(ds, x + w, y, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(ds, x, y + h, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(ds, x + w, y + h, handleSize, handleColor, outlineColor);
                }
            }
            else
            {
                // With rotation - draw rotated border
                float centerX = x + w / 2f;
                float centerY = y + h / 2f;
                float radians = rotation * MathF.PI / 180f;
                float cos = MathF.Cos(radians);
                float sin = MathF.Sin(radians);

                var corners = new Vector2[4];
                var localCorners = new Vector2[]
                {
                    new(x - centerX, y - centerY),
                    new(x + w - centerX, y - centerY),
                    new(x + w - centerX, y + h - centerY),
                    new(x - centerX, y + h - centerY)
                };

                for (int i = 0; i < 4; i++)
                {
                    float rx = localCorners[i].X * cos - localCorners[i].Y * sin;
                    float ry = localCorners[i].X * sin + localCorners[i].Y * cos;
                    corners[i] = new Vector2(centerX + rx, centerY + ry);
                }

                for (int i = 0; i < 4; i++)
                {
                    int next = (i + 1) % 4;
                    ds.DrawLine(corners[i], corners[next], outlineColor, outlineWidth);
                }

                if (!isLocked)
                {
                    foreach (var corner in corners)
                    {
                        DrawReferenceHandle(ds, corner.X, corner.Y, handleSize, handleColor, outlineColor);
                    }
                }
            }
        }

        /// <summary>
        /// Draws a single reference image handle.
        /// </summary>
        private static void DrawReferenceHandle(CanvasDrawingSession ds, float x, float y, float size, Color fill, Color stroke)
        {
            float half = size / 2f;
            ds.FillRectangle(x - half, y - half, size, size, fill);
            ds.DrawRectangle(x - half, y - half, size, size, stroke, 1f);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - ONION SKINNING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws onion skin frames (semi-transparent previous/next frames) for canvas animation.
        /// </summary>
        private void DrawOnionSkinFrames(CanvasDrawingSession ds, CanvasDevice device, Rect dest)
        {
            var animState = Document.CanvasAnimationState;
            if (animState == null || !animState.OnionSkinEnabled)
                return;

            var onionFrames = animState.GetOnionSkinFrames();
            if (onionFrames.Count == 0)
                return;

            int currentFrame = animState.CurrentFrameIndex;

            foreach (var (frameIndex, opacity) in onionFrames)
            {
                // Create a temporary composite for this frame
                var tempComposite = new Core.Imaging.PixelSurface(Document.PixelWidth, Document.PixelHeight);

                // Apply the frame state to get the pixel data
                // We need to temporarily apply the frame and then restore
                foreach (var track in animState.Tracks)
                {
                    var state = track.GetEffectiveStateAt(frameIndex);
                    if (state == null) continue;

                    // Find the layer in the document
                    var layer = FindLayerByGuid(track.LayerId);
                    if (layer is not RasterLayer raster) continue;

                    // If the keyframe has pixel data, use it; otherwise skip
                    if (!state.HasPixelData) continue;

                    var pixelData = animState.GetPixelData(state.PixelDataId);
                    if (pixelData == null || pixelData.Length != raster.Surface.Pixels.Length) continue;

                    if (!state.Visible) continue;

                    // Composite this layer's frame data onto tempComposite
                    CompositeLayerToSurface(tempComposite, pixelData, raster.Surface.Width, raster.Surface.Height,
                        state.Opacity, state.BlendMode);
                }

                // Draw the temp composite with tint based on whether it's before or after current frame
                using var bmp = CanvasBitmap.CreateFromBytes(
                    device,
                    tempComposite.Pixels,
                    tempComposite.Width,
                    tempComposite.Height,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    96.0f);

                // Tint color: blue for previous frames, green for future frames
                bool isPrevious = frameIndex < currentFrame;

                // Draw with reduced opacity and tint
                using (ds.CreateLayer(opacity))
                {
                    ds.DrawImage(
                        bmp,
                        dest,
                        new Rect(0, 0, tempComposite.Width, tempComposite.Height),
                        1.0f,
                        CanvasImageInterpolation.NearestNeighbor);

                    // Draw a tint overlay
                    var tintColor = isPrevious
                        ? Color.FromArgb((byte)(50 * opacity), 100, 100, 255)  // Blue tint for previous
                        : Color.FromArgb((byte)(50 * opacity), 100, 255, 100); // Green tint for next
                    ds.FillRectangle(dest, tintColor);
                }
            }
        }

        /// <summary>
        /// Finds a layer in the document by its GUID.
        /// </summary>
        private LayerBase? FindLayerByGuid(Guid layerId)
        {
            foreach (var layer in Document.GetFlattenedLayers())
            {
                if (layer.Id == layerId)
                    return layer;
            }
            return null;
        }

        /// <summary>
        /// Composites layer pixel data onto a surface with the given opacity and blend mode.
        /// </summary>
        private static void CompositeLayerToSurface(Core.Imaging.PixelSurface dest, byte[] srcPixels, int width, int height, byte opacity, Core.Enums.BlendMode blend)
        {
            if (width != dest.Width || height != dest.Height)
                return;

            var dstPixels = dest.Pixels;
            float opacityF = opacity / 255f;

            for (int i = 0; i < srcPixels.Length; i += 4)
            {
                byte srcB = srcPixels[i];
                byte srcG = srcPixels[i + 1];
                byte srcR = srcPixels[i + 2];
                byte srcA = (byte)(srcPixels[i + 3] * opacityF);

                if (srcA == 0) continue;

                byte dstB = dstPixels[i];
                byte dstG = dstPixels[i + 1];
                byte dstR = dstPixels[i + 2];
                byte dstA = dstPixels[i + 3];

                // Simple alpha compositing (Normal blend mode)
                // For simplicity, we just do normal blending here
                float sa = srcA / 255f;
                float da = dstA / 255f;
                float outA = sa + da * (1 - sa);

                if (outA > 0)
                {
                    dstPixels[i] = (byte)((srcB * sa + dstB * da * (1 - sa)) / outA);
                    dstPixels[i + 1] = (byte)((srcG * sa + dstG * da * (1 - sa)) / outA);
                    dstPixels[i + 2] = (byte)((srcR * sa + dstR * da * (1 - sa)) / outA);
                    dstPixels[i + 3] = (byte)(outA * 255);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - STAGE OVERLAY
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws the stage (camera viewport) overlay on the canvas.
        /// Shows the visible area that will be rendered in the final animation.
        /// Only shown in Canvas Animation mode.
        /// </summary>
        private void DrawStageOverlay(CanvasDrawingSession ds, Rect dest)
        {
            // Only show stage in Canvas Animation mode
            if (_animationMode != Animation.AnimationMode.Canvas)
                return;

            var animState = Document.CanvasAnimationState;
            if (animState == null || !animState.Stage.Enabled)
                return;

            var stage = animState.Stage;
            float scale = (float)_zoom.Scale;

            // Get current colors based on selection state
            var outlineColor = _stageSelected ? StageOutlineSelectedColor : StageOutlineColor;
            var cornerColor = _stageSelected ? StageCornerSelectedColor : StageCornerColor;
            float outlineWidth = _stageSelected ? 3f : 2f;

            // Calculate stage rect in screen coordinates
            // Behavior:
            // - Actively dragging/resizing: Use direct StageSettings (see your edits in real-time)
            // - Pending unsaved edits at current frame: Use direct StageSettings (keep showing your edits)
            // - Otherwise: Use interpolated position (scrubbing/playback preview)
            float stageX, stageY, stageW, stageH;
            float rotation = 0f;

            // Check if we have pending (unsaved) edits at the current frame
            bool hasPendingEditsAtCurrentFrame = _stagePendingEdits && 
                                                  _stagePendingEditsFrame == animState.CurrentFrameIndex;
            
            // Use direct settings when:
            // - Actively dragging or resizing, OR
            // - We have pending unsaved edits at this frame
            bool useDirectSettings = _stageDragging || _stageResizing || hasPendingEditsAtCurrentFrame;

            if (useDirectSettings)
            {
                // Active manipulation or pending edits - use direct stage settings
                stageX = stage.StageX;
                stageY = stage.StageY;
                stageW = stage.StageWidth;
                stageH = stage.StageHeight;
            }
            else
            {
                // Preview mode - use interpolated transform for scrubbing/playback
                var stageTransform = animState.GetStageTransformAt(animState.CurrentFrameIndex);

                if (stageTransform != null)
                {
                    // Use keyframed transform
                    float centerX = stageTransform.PositionX;
                    float centerY = stageTransform.PositionY;
                    float scaleX = stageTransform.ScaleX;
                    float scaleY = stageTransform.ScaleY;
                    rotation = stageTransform.Rotation;

                    // Calculate stage capture area based on scale
                    stageW = stage.OutputWidth / scaleX;
                    stageH = stage.OutputHeight / scaleY;
                    stageX = centerX - stageW / 2;
                    stageY = centerY - stageH / 2;
                }
                else
                {
                    // No keyframes - use default stage settings
                    stageX = stage.StageX;
                    stageY = stage.StageY;
                    stageW = stage.StageWidth;
                    stageH = stage.StageHeight;
                }
            }

            // Convert to screen coordinates
            float screenStageX = (float)(dest.X + stageX * scale);
            float screenStageY = (float)(dest.Y + stageY * scale);
            float screenStageW = stageW * scale;
            float screenStageH = stageH * scale;

            // Draw dimmed areas outside the stage (letterboxing effect)
            DrawStageDimOverlay(ds, dest, screenStageX, screenStageY, screenStageW, screenStageH, rotation);

            // Draw stage outline
            if (Math.Abs(rotation) < 0.01f)
            {
                // No rotation - draw simple rectangle
                ds.DrawRectangle(screenStageX, screenStageY, screenStageW, screenStageH, outlineColor, outlineWidth);

                // Draw corner handles (only when selected for editing)
                if (_stageSelected)
                {
                    DrawStageCornerHandles(ds, screenStageX, screenStageY, screenStageW, screenStageH, cornerColor, outlineColor);
                }
            }
            else
            {
                // With rotation - draw rotated rectangle
                DrawRotatedStageOutline(ds, screenStageX, screenStageY, screenStageW, screenStageH, rotation, outlineColor, cornerColor, outlineWidth);
            }

            // Draw stage label
            DrawStageLabel(ds, screenStageX, screenStageY, stage, outlineColor);
        }

        /// <summary>
        /// Draws the dimmed overlay outside the stage area.
        /// </summary>
        private void DrawStageDimOverlay(CanvasDrawingSession ds, Rect dest,
            float stageX, float stageY, float stageW, float stageH, float rotation)
        {
            if (Math.Abs(rotation) > 0.01f)
            {
                // For rotated stages, we'd need a more complex masking approach
                // For now, skip dimming on rotated stages
                return;
            }

            // Top region
            if (stageY > dest.Y)
            {
                ds.FillRectangle((float)dest.X, (float)dest.Y,
                    (float)dest.Width, stageY - (float)dest.Y, StageDimColor);
            }

            // Bottom region
            float stageBottom = stageY + stageH;
            float destBottom = (float)(dest.Y + dest.Height);
            if (stageBottom < destBottom)
            {
                ds.FillRectangle((float)dest.X, stageBottom,
                    (float)dest.Width, destBottom - stageBottom, StageDimColor);
            }

            // Left region (between top and bottom dimmed areas)
            float dimTop = Math.Max((float)dest.Y, stageY);
            float dimBottom = Math.Min(destBottom, stageBottom);
            float dimHeight = dimBottom - dimTop;

            if (stageX > dest.X && dimHeight > 0)
            {
                ds.FillRectangle((float)dest.X, dimTop,
                    stageX - (float)dest.X, dimHeight, StageDimColor);
            }

            // Right region
            float stageRight = stageX + stageW;
            float destRight = (float)(dest.X + dest.Width);
            if (stageRight < destRight && dimHeight > 0)
            {
                ds.FillRectangle(stageRight, dimTop,
                    destRight - stageRight, dimHeight, StageDimColor);
            }
        }

        /// <summary>
        /// Draws corner handles for resizing the stage.
        /// </summary>
        private void DrawStageCornerHandles(CanvasDrawingSession ds,
            float x, float y, float w, float h, Color cornerColor, Color outlineColor)
        {
            const float handleSize = 8f;
            const float handleOffset = handleSize / 2f;

            // Top-left
            ds.FillRectangle(x - handleOffset, y - handleOffset, handleSize, handleSize, cornerColor);
            ds.DrawRectangle(x - handleOffset, y - handleOffset, handleSize, handleSize, outlineColor, 1f);

            // Top-right
            ds.FillRectangle(x + w - handleOffset, y - handleOffset, handleSize, handleSize, cornerColor);
            ds.DrawRectangle(x + w - handleOffset, y - handleOffset, handleSize, handleSize, outlineColor, 1f);

            // Bottom-left
            ds.FillRectangle(x - handleOffset, y + h - handleOffset, handleSize, handleSize, cornerColor);
            ds.DrawRectangle(x - handleOffset, y + h - handleOffset, handleSize, handleSize, outlineColor, 1f);

            // Bottom-right
            ds.FillRectangle(x + w - handleOffset, y + h - handleOffset, handleSize, handleSize, cornerColor);
            ds.DrawRectangle(x + w - handleOffset, y + h - handleOffset, handleSize, handleSize, outlineColor, 1f);

            // Center (for moving)
            float centerX = x + w / 2f;
            float centerY = y + h / 2f;
            ds.FillEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, cornerColor);
            ds.DrawEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, outlineColor, 1f);
        }

        /// <summary>
        /// Draws a rotated stage outline.
        /// </summary>
        private void DrawRotatedStageOutline(CanvasDrawingSession ds,
            float x, float y, float w, float h, float rotationDegrees, Color outlineColor, Color cornerColor, float outlineWidth)
        {
            float centerX = x + w / 2f;
            float centerY = y + h / 2f;
            float radians = rotationDegrees * MathF.PI / 180f;

            // Calculate corner points
            var corners = new Vector2[4];
            var halfW = w / 2f;
            var halfH = h / 2f;

            // Local corner positions (before rotation)
            var localCorners = new Vector2[]
            {
                new(-halfW, -halfH), // Top-left
                new(halfW, -halfH),  // Top-right
                new(halfW, halfH),   // Bottom-right
                new(-halfW, halfH)   // Bottom-left
            };

            // Rotate and translate corners
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);

            for (int i = 0; i < 4; i++)
            {
                float rx = localCorners[i].X * cos - localCorners[i].Y * sin;
                float ry = localCorners[i].X * sin + localCorners[i].Y * cos;
                corners[i] = new Vector2(centerX + rx, centerY + ry);
            }

            // Draw the rotated rectangle
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                ds.DrawLine(corners[i], corners[next], outlineColor, outlineWidth);
            }

            // Draw corner handles at rotated positions (only when selected)
            if (_stageSelected)
            {
                const float handleSize = 8f;
                foreach (var corner in corners)
                {
                    ds.FillRectangle(corner.X - handleSize / 2f, corner.Y - handleSize / 2f,
                        handleSize, handleSize, cornerColor);
                    ds.DrawRectangle(corner.X - handleSize / 2f, corner.Y - handleSize / 2f,
                        handleSize, handleSize, outlineColor, 1f);
                }

                // Draw center handle
                ds.FillEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, cornerColor);
                ds.DrawEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, outlineColor, 1f);
            }
        }

        /// <summary>
        /// Draws the stage label showing dimensions and output info.
        /// </summary>
        private void DrawStageLabel(CanvasDrawingSession ds, float x, float y,
            Core.Animation.StageSettings stage, Color outlineColor)
        {
            string label = _stageSelected
                ? $"Stage: {stage.StageWidth}×{stage.StageHeight} → {stage.OutputWidth}×{stage.OutputHeight} (Selected)"
                : $"Stage: {stage.StageWidth}×{stage.StageHeight} → {stage.OutputWidth}×{stage.OutputHeight}";

            using var format = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontSize = 11,
                FontFamily = "Segoe UI",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            using var textLayout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(
                ds.Device, label, format, 400, 20);

            float textWidth = (float)textLayout.LayoutBounds.Width;
            float textHeight = (float)textLayout.LayoutBounds.Height;
            float padding = 4f;

            float labelX = x;
            float labelY = y - textHeight - padding * 2 - 4f;

            // Draw background
            var bgRect = new Rect(labelX - padding, labelY - padding / 2,
                textWidth + padding * 2, textHeight + padding);
            ds.FillRoundedRectangle(bgRect, 3, 3, Color.FromArgb(220, 0, 0, 0));
            ds.DrawRoundedRectangle(bgRect, 3, 3, outlineColor, 1f);

            // Draw text
            ds.DrawText(label, labelX, labelY, outlineColor, format);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - GUIDES
        // ════════════════════════════════════════════════════════════════════

        private void DrawGuides(CanvasDrawingSession ds, Rect dest)
        {
            if (_guideService == null || !_guideService.GuidesVisible)
                return;

            float scale = (float)_zoom.Scale;

            // Get the full render area (the entire canvas control bounds)
            float renderWidth = (float)CanvasView.ActualWidth;
            float renderHeight = (float)CanvasView.ActualHeight;

            // Draw horizontal guides
            foreach (var guide in _guideService.HorizontalGuides)
            {
                float screenY = (float)(dest.Y + guide.Position * scale);
                var color = guide.IsSelected ? GuideColorSelected : GuideColor;

                // Draw guide line extending across the entire render area
                ds.DrawLine(0, screenY, renderWidth, screenY, color, 1f);
            }

            // Draw vertical guides
            foreach (var guide in _guideService.VerticalGuides)
            {
                float screenX = (float)(dest.X + guide.Position * scale);
                var color = guide.IsSelected ? GuideColorSelected : GuideColor;

                // Draw guide line extending across the entire render area
                ds.DrawLine(screenX, 0, screenX, renderHeight, color, 1f);
            }

            // Draw position indicator for guide being dragged
            if (_dragGuide != null)
            {
                DrawGuidePositionIndicator(ds, dest, scale);
            }
        }

        /// <summary>
        /// Draws a position indicator showing the current guide position in pixels.
        /// </summary>
        private void DrawGuidePositionIndicator(CanvasDrawingSession ds, Rect dest, float scale)
        {
            if (_dragGuide == null) return;

            string positionText = $"{_dragGuide.Position} px";

            using var textFormat = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontSize = 11,
                FontFamily = "Segoe UI",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            // Measure text to create background
            using var textLayout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(
                ds.Device, positionText, textFormat, 100, 20);

            float textWidth = (float)textLayout.LayoutBounds.Width;
            float textHeight = (float)textLayout.LayoutBounds.Height;
            float padding = 4f;

            if (_dragGuide.IsHorizontal)
            {
                // Horizontal guide - show position label near the left edge
                float screenY = (float)(dest.Y + _dragGuide.Position * scale);
                float labelX = 8f;
                float labelY = screenY + 4f;

                // Draw background
                var bgRect = new Rect(labelX - padding, labelY - padding / 2,
                    textWidth + padding * 2, textHeight + padding);
                ds.FillRoundedRectangle(bgRect, 3, 3,
                    Windows.UI.Color.FromArgb(220, 0, 0, 0));
                ds.DrawRoundedRectangle(bgRect, 3, 3, GuideColor, 1f);

                // Draw text
                ds.DrawText(positionText, labelX, labelY,
                    Windows.UI.Color.FromArgb(255, 100, 220, 255), textFormat);
            }
            else
            {
                // Vertical guide - show position label near the top edge  
                float screenX = (float)(dest.X + _dragGuide.Position * scale);
                float labelX = screenX + 4f;
                float labelY = 8f;

                // Draw background
                var bgRect = new Rect(labelX - padding, labelY - padding / 2,
                    textWidth + padding * 2, textHeight + padding);
                ds.FillRoundedRectangle(bgRect, 3, 3,
                    Windows.UI.Color.FromArgb(220, 0, 0, 0));
                ds.DrawRoundedRectangle(bgRect, 3, 3, GuideColor, 1f);

                // Draw text
                ds.DrawText(positionText, labelX, labelY,
                    Windows.UI.Color.FromArgb(255, 100, 220, 255), textFormat);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - BACKGROUND & SURFACE
        // ════════════════════════════════════════════════════════════════════

        private void DrawCheckerboardBackground(CanvasDrawingSession ds, Rect dest)
        {
            float tx = (float)Math.Floor(dest.X);
            float ty = (float)Math.Floor(dest.Y);

            using (ds.CreateLayer(1.0f, dest))
            {
                _stripeBrush!.Transform = Matrix3x2.CreateTranslation(tx, ty);
                ds.FillRectangle(dest, _stripeBrush);
            }
        }

        private void DrawDocumentSurface(CanvasDrawingSession ds, CanvasDevice device, Rect dest)
        {
            var surf = _composite ?? Document.Surface;
            using var bmp = CanvasBitmap.CreateFromBytes(
                device,
                surf.Pixels,
                surf.Width,
                surf.Height,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            ds.DrawImage(
                bmp,
                dest,
                new Rect(0, 0, surf.Width, surf.Height),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);

            // Draw mask overlay when editing a mask
            DrawMaskOverlay(ds, device, dest);
        }

        /// <summary>
        /// Draws the mask overlay when the user is editing a layer mask.
        /// Shows the mask as a semi-transparent red overlay so users can see what they're painting.
        /// White areas (visible) = completely clear, Black areas (hidden) = red tint
        /// </summary>
        private void DrawMaskOverlay(CanvasDrawingSession ds, CanvasDevice device, Rect dest)
        {
            // Check if active layer is editing a mask
            var activeLayer = Document.ActiveLayer;
            if (activeLayer == null || !activeLayer.IsEditingMask || activeLayer.Mask == null)
                return;

            var mask = activeLayer.Mask;

            // Create a colored version of the mask for visualization
            // We'll show white areas as completely clear, black/gray areas with red tint
            var maskPixels = mask.Surface.Pixels;
            var overlayPixels = new byte[maskPixels.Length];

            for (int i = 0; i < maskPixels.Length; i += 4)
            {
                // Get the grayscale mask value (stored in B, G, R channels equally)
                byte maskValue = maskPixels[i]; // B channel = grayscale value

                // Invert: where mask is dark (hidden), show red overlay
                // Where mask is white (visible), show nothing at all

                // Use a threshold so that near-white values are completely clear
                // This makes it obvious when an area is "fully revealed"
                if (maskValue >= 250)
                {
                    // Fully white or near-white = completely transparent (no overlay)
                    overlayPixels[i + 0] = 0;   // B
                    overlayPixels[i + 1] = 0;   // G  
                    overlayPixels[i + 2] = 0;   // R
                    overlayPixels[i + 3] = 0;   // A - fully transparent
                }
                else
                {
                    // Calculate alpha based on how dark the mask is
                    // 0 (black/hidden) = max red overlay, 249 (almost white) = minimal overlay
                    byte alpha = (byte)((255 - maskValue) * 0.6f); // 60% max opacity for better visibility

                    // Ruby red color for mask visualization
                    overlayPixels[i + 0] = 60;   // B
                    overlayPixels[i + 1] = 40;   // G  
                    overlayPixels[i + 2] = 220;  // R
                    overlayPixels[i + 3] = alpha; // A
                }
            }

            using var overlayBmp = CanvasBitmap.CreateFromBytes(
                device,
                overlayPixels,
                mask.Width,
                mask.Height,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            ds.DrawImage(
                overlayBmp,
                dest,
                new Rect(0, 0, mask.Width, mask.Height),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);
        }

        private void DrawDocumentBorder(CanvasDrawingSession ds, Rect dest)
        {
            ds.DrawRectangle(
                (float)dest.X,
                (float)dest.Y,
                (float)dest.Width,
                (float)dest.Height,
                Colors.Black,
                1f);
        }

        private void EnsureStripeBrush(CanvasDevice device)
        {
            if (_stripeBrush != null) return;

            const int BAND = 4;
            const int TILE = 64;
            const int PERIOD = BAND * 2;

            var rt = new CanvasRenderTarget(device, TILE, TILE, 96);

            var cA = Color.FromArgb(255, 255, 255, 255);
            var cB = Color.FromArgb(255, 232, 232, 232);

            var pixels = new Color[TILE * TILE];

            for (int y = 0; y < TILE; y++)
            {
                int yBase = y;
                for (int x = 0; x < TILE; x++)
                {
                    bool light = ((x + yBase) % PERIOD) < BAND;
                    pixels[y * TILE + x] = light ? cA : cB;
                }
            }

            rt.SetPixelColors(pixels);

            _stripeBrush = new CanvasImageBrush(device, rt)
            {
                ExtendX = CanvasEdgeBehavior.Wrap,
                ExtendY = CanvasEdgeBehavior.Wrap,
                Interpolation = CanvasImageInterpolation.NearestNeighbor
            };

            rt.Dispose();
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - GRID OVERLAYS
        // ════════════════════════════════════════════════════════════════════

        private void DrawPixelGrid(CanvasDrawingSession ds, Rect dest)
        {
            float s = (float)_zoom.Scale;
            if (s < 6f) return;

            for (int x = 0; x <= Document.Surface.Width; x++)
            {
                float sx = (float)(dest.X + x * s);
                ds.DrawLine(sx, (float)dest.Y, sx, (float)(dest.Y + dest.Height), Colors.DimGray, 1f);
            }

            for (int y = 0; y <= Document.Surface.Height; y++)
            {
                float sy = (float)(dest.Y + y * s);
                ds.DrawLine((float)dest.X, sy, (float)(dest.X + dest.Width), sy, Colors.DimGray, 1f);
            }
        }

        private void DrawTileGrid(CanvasDrawingSession ds, Rect dest)
        {
            float s = (float)_zoom.Scale;

            for (int x = Document.TileSize.Width; x <= Document.TileCounts.Width * Document.TileSize.Width; x += Document.TileSize.Width)
            {
                float sx = (float)(dest.X + x * s);
                ds.DrawLine(sx, (float)dest.Y, sx, (float)(dest.Y + dest.Height), Colors.DimGray, 1f);
            }

            for (int y = Document.TileSize.Height; y <= Document.TileCounts.Height * Document.TileSize.Height; y += Document.TileSize.Height)
            {
                float sy = (float)(dest.Y + y * s);
                ds.DrawLine((float)dest.X, sy, (float)(dest.X + dest.Width), sy, Colors.DimGray, 1f);
            }
        }

        /// <summary>
        /// Draws tile mapping numbers in the top-left corner of each tile on the active layer.
        /// </summary>
        private void DrawTileMappings(CanvasDrawingSession ds, Rect dest)
        {
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var tileMapping = rl.TileMapping;
            if (tileMapping == null)
                return;

            int tileW = Document.TileSize.Width;
            int tileH = Document.TileSize.Height;
            float scale = (float)_zoom.Scale;

            // Only draw if tiles are large enough to see the text
            float minTileScreenSize = 24f;
            if (tileW * scale < minTileScreenSize || tileH * scale < minTileScreenSize)
                return;

            using var format = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontSize = Math.Max(8f, Math.Min(14f, tileW * scale * 0.3f)),
                HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left,
                VerticalAlignment = Microsoft.Graphics.Canvas.Text.CanvasVerticalAlignment.Top,
                FontFamily = "Segoe UI",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };

            int tilesX = (Document.PixelWidth + tileW - 1) / tileW;
            int tilesY = (Document.PixelHeight + tileH - 1) / tileH;

            for (int ty = 0; ty < tilesY; ty++)
            {
                for (int tx = 0; tx < tilesX; tx++)
                {
                    int tileId = tileMapping.GetTileId(tx, ty);
                    if (tileId < 0) continue; // No tile mapped at this position

                    // Calculate screen position
                    float screenX = (float)(dest.X + tx * tileW * scale);
                    float screenY = (float)(dest.Y + ty * tileH * scale);

                    // Draw background for readability
                    string text = tileId.ToString();
                    var textLayout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(ds.Device, text, format, 100, 50);
                    float textW = (float)textLayout.LayoutBounds.Width;
                    float textH = (float)textLayout.LayoutBounds.Height;

                    var bgRect = new Rect(screenX + 2, screenY + 2, textW + 4, textH + 2);
                    ds.FillRectangle(bgRect, Color.FromArgb(180, 0, 0, 0));

                    // Draw text
                    ds.DrawText(text, screenX + 4, screenY + 2, Colors.White, format);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - CONTRAST INK HELPER
        // ════════════════════════════════════════════════════════════════════

        private Color SampleInkAtDoc(int docX, int docY)
        {
            if ((uint)docX >= (uint)Document.Surface.Width ||
                (uint)docY >= (uint)Document.Surface.Height)
                return ColorUtil.HighContrastInk(0xFFFFFFFFu);

            uint px = ReadCompositeBGRA(docX, docY);
            uint onWhite = ColorUtil.CompositeOverWhite(px);
            return ColorUtil.HighContrastInk(onWhite);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - TILE ANIMATION FRAMES
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws tile animation frame indicators on the canvas.
        /// Highlights tiles that are part of the current animation reel.
        /// </summary>
        private void DrawTileAnimationFrames(CanvasDrawingSession ds, Rect dest)
        {
            var animState = Document.TileAnimationState;
            if (animState == null)
                return;

            var reel = animState.SelectedReel;
            if (reel == null || reel.FrameCount == 0)
                return;

            int tileW = Document.TileSize.Width;
            int tileH = Document.TileSize.Height;
            float scale = (float)_zoom.Scale;

            // Get the current tile position
            var (currentTileX, currentTileY) = animState.CurrentTilePosition;

            // Build a set of all frame positions and their frame indices
            var framePositions = new System.Collections.Generic.Dictionary<(int, int), System.Collections.Generic.List<int>>();
            for (int i = 0; i < reel.FrameCount; i++)
            {
                var frame = reel.Frames[i];
                var key = (frame.TileX, frame.TileY);
                if (!framePositions.ContainsKey(key))
                    framePositions[key] = [];
                framePositions[key].Add(i);
            }

            // Draw all frame positions
            foreach (var kvp in framePositions)
            {
                int tx = kvp.Key.Item1;
                int ty = kvp.Key.Item2;
                var frameIndices = kvp.Value;

                bool isCurrent = tx == currentTileX && ty == currentTileY;

                // Calculate screen position
                float screenX = (float)(dest.X + tx * tileW * scale);
                float screenY = (float)(dest.Y + ty * tileH * scale);
                float screenW = tileW * scale;
                float screenH = tileH * scale;

                // Draw fill
                var fillColor = isCurrent ? TileAnimCurrentFrameColor : TileAnimFrameColor;
                ds.FillRectangle(screenX, screenY, screenW, screenH, fillColor);

                // Draw outline
                var outlineColor = isCurrent ? TileAnimCurrentOutline : TileAnimFrameOutline;
                ds.DrawRectangle(screenX, screenY, screenW, screenH, outlineColor, isCurrent ? 3f : 2f);

                // Draw frame number label(s) in the top-left
                DrawTileAnimationLabel(ds, screenX, screenY, screenW, scale, frameIndices, isCurrent);
            }
        }

        /// <summary>
        /// Draws frame number labels on a tile animation frame.
        /// </summary>
        private void DrawTileAnimationLabel(CanvasDrawingSession ds, float screenX, float screenY, float screenW, float scale,
            System.Collections.Generic.List<int> frameIndices, bool isCurrent)
        {
            // Only draw if tiles are large enough to see the text
            if (screenW < 24f)
                return;

            using var format = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
            {
                FontSize = Math.Max(8f, Math.Min(12f, screenW * 0.15f)),
                HorizontalAlignment = Microsoft.Graphics.Canvas.Text.CanvasHorizontalAlignment.Left,
                VerticalAlignment = Microsoft.Graphics.Canvas.Text.CanvasVerticalAlignment.Top,
                FontFamily = "Segoe UI",
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };

            // Create label text (show all frame indices for this tile)
            string label;
            if (frameIndices.Count == 1)
            {
                label = $"F{frameIndices[0] + 1}";
            }
            else if (frameIndices.Count <= 3)
            {
                label = string.Join(",", frameIndices.ConvertAll(i => $"F{i + 1}"));
            }
            else
            {
                // Too many to show, just show count
                label = $"×{frameIndices.Count}";
            }

            using var textLayout = new Microsoft.Graphics.Canvas.Text.CanvasTextLayout(ds.Device, label, format, 100, 50);
            float textW = (float)textLayout.LayoutBounds.Width;
            float textH = (float)textLayout.LayoutBounds.Height;

            float labelX = screenX + 2;
            float labelY = screenY + 2;

            // Draw background
            var bgColor = isCurrent ? Color.FromArgb(220, 80, 60, 0) : Color.FromArgb(200, 0, 50, 100);
            var bgRect = new Rect(labelX, labelY, textW + 4, textH + 2);
            ds.FillRoundedRectangle(bgRect, 2, 2, bgColor);

            // Draw text
            var textColor = isCurrent ? Colors.White : Color.FromArgb(255, 200, 220, 255);
            ds.DrawText(label, labelX + 2, labelY + 1, textColor, format);
        }
    }
}
