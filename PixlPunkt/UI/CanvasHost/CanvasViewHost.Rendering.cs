using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Rendering;
using PixlPunkt.Core.Symmetry;
using SkiaSharp;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Rendering subsystem for CanvasViewHost (SkiaSharp implementation):
    /// - Main draw pipeline
    /// - Background/surface rendering
    /// - Grid overlays (pixel, tile)
    /// - Guide overlays
    /// - Checkerboard pattern management
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // Guide colors
        private static readonly Color GuideColor = Color.FromArgb(200, 0, 180, 255);
        private static readonly Color GuideColorSelected = Color.FromArgb(255, 255, 100, 100);

        // Stage overlay colors
        private static readonly Color StageOutlineColor = Color.FromArgb(255, 255, 165, 0);
        private static readonly Color StageOutlineSelectedColor = Color.FromArgb(255, 255, 80, 80);
        private static readonly Color StageDimColor = Color.FromArgb(120, 0, 0, 0);
        private static readonly Color StageCornerColor = Color.FromArgb(255, 255, 200, 100);
        private static readonly Color StageCornerSelectedColor = Color.FromArgb(255, 255, 120, 120);

        // Tile animation frame overlay colors
        private static readonly Color TileAnimFrameColor = Color.FromArgb(100, 100, 200, 255);
        private static readonly Color TileAnimCurrentFrameColor = Color.FromArgb(180, 255, 200, 100);
        private static readonly Color TileAnimFrameOutline = Color.FromArgb(200, 100, 150, 255);
        private static readonly Color TileAnimCurrentOutline = Color.FromArgb(255, 255, 180, 0);

        // Symmetry overlay colors
        private static readonly Color SymmetryAxisColor = Color.FromArgb(180, 255, 100, 200);
        private static readonly Color SymmetryAxisColorSecondary = Color.FromArgb(120, 200, 100, 255);
        private static readonly Color SymmetryCenterColor = Color.FromArgb(255, 255, 200, 100);

        private bool _showStageOverlay = true;

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
            var theme = ActualTheme;
            return theme == Microsoft.UI.Xaml.ElementTheme.Light
                ? Color.FromArgb(255, 249, 249, 249)
                : Color.FromArgb(255, 24, 24, 24);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - MAIN DRAW (SKIASHARP)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Main drawing pipeline using SkiaSharp renderer.
        /// </summary>
        private void CanvasView_Draw(ICanvasRenderer renderer)
        {
            var clearColor = GetThemeClearColor();
            renderer.Clear(clearColor);

            var dest = _zoom.GetDestRect();
            renderer.Antialiasing = false;

            // Draw checkerboard background
            DrawCheckerboardBackground(renderer, dest);

            // Draw reference layers below
            DrawReferenceLayers(renderer, dest, renderBelow: true);

            // Draw onion skin frames
            DrawOnionSkinFrames(renderer, dest);

            // Draw document with interleaved sub-routines
            DrawDocumentWithInterleavedSubRoutines(renderer, dest);

            // Draw document border
            DrawDocumentBorder(renderer, dest);

            // Draw reference layers above
            DrawReferenceLayers(renderer, dest, renderBelow: false);

            // Draw shape preview
            if (_shapeDrag)
                DrawShapePreview(renderer, dest);

            // Draw gradient fill preview
            if (_gradientDrag)
                DrawGradientFillPreview(renderer, dest);

            // Draw brush cursor overlay
            DrawBrushCursorOverlay(renderer, dest);

            // Draw grids
            if (_showPixelGrid) DrawPixelGrid(renderer, dest);
            if (_showTileGrid) DrawTileGrid(renderer, dest);
            if (_showTileMappings) DrawTileMappings(renderer, dest);
            if (_showTileAnimationMappings) DrawTileAnimationFrames(renderer, dest);

            // Draw symmetry overlay
            DrawSymmetryOverlay(renderer, dest);

            // Draw stage overlay
            if (_showStageOverlay) DrawStageOverlay(renderer, dest);

            // Draw guides
            DrawGuides(renderer, dest);

            // Draw selection
            Selection_Draw(renderer);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - REFERENCE LAYERS
        // ════════════════════════════════════════════════════════════════════

        private void DrawReferenceLayers(ICanvasRenderer renderer, Rect dest, bool renderBelow)
        {
            if (Document == null) return;

            float scale = (float)_zoom.Scale;
            var rootItems = Document.RootItems;

            int firstRasterIndex = -1;
            for (int i = 0; i < rootItems.Count; i++)
            {
                if (rootItems[i] is RasterLayer || rootItems[i] is LayerFolder)
                {
                    firstRasterIndex = i;
                    break;
                }
            }

            for (int i = 0; i < rootItems.Count; i++)
            {
                if (rootItems[i] is not ReferenceLayer refLayer)
                    continue;

                if (!refLayer.IsEffectivelyVisible())
                    continue;

                bool isBelow = firstRasterIndex < 0 || i < firstRasterIndex;
                if (isBelow != renderBelow)
                    continue;

                DrawSingleReferenceLayer(renderer, dest, refLayer, scale);
            }

            foreach (var item in rootItems)
            {
                if (item is LayerFolder folder)
                {
                    DrawReferencLayersInFolder(renderer, dest, folder, scale, renderBelow, firstRasterIndex);
                }
            }
        }

        private void DrawReferencLayersInFolder(ICanvasRenderer renderer, Rect dest,
            LayerFolder folder, float scale, bool renderBelow, int firstRasterIndex)
        {
            foreach (var child in folder.Children)
            {
                if (child is ReferenceLayer refLayer && refLayer.IsEffectivelyVisible())
                {
                    if (!renderBelow)
                    {
                        DrawSingleReferenceLayer(renderer, dest, refLayer, scale);
                    }
                }
                else if (child is LayerFolder subFolder)
                {
                    DrawReferencLayersInFolder(renderer, dest, subFolder, scale, renderBelow, firstRasterIndex);
                }
            }
        }

        private void DrawSingleReferenceLayer(ICanvasRenderer renderer, Rect dest, ReferenceLayer refLayer, float scale)
        {
            if (refLayer.Pixels == null || refLayer.ImageWidth <= 0 || refLayer.ImageHeight <= 0)
                return;

            try
            {
                float imgScale = refLayer.Scale * scale;
                float screenX = (float)(dest.X + refLayer.PositionX * scale);
                float screenY = (float)(dest.Y + refLayer.PositionY * scale);
                float screenW = refLayer.ImageWidth * imgScale;
                float screenH = refLayer.ImageHeight * imgScale;

                float opacity = refLayer.Opacity / 255f;

                var destRect = new Rect(screenX, screenY, screenW, screenH);
                var srcRect = new Rect(0, 0, refLayer.ImageWidth, refLayer.ImageHeight);

                if (Math.Abs(refLayer.Rotation) < 0.01f)
                {
                    renderer.DrawPixels(refLayer.Pixels, refLayer.ImageWidth, refLayer.ImageHeight,
                        destRect, srcRect, opacity, ImageInterpolation.HighQualityCubic);
                }
                else
                {
                    float centerX = screenX + screenW / 2f;
                    float centerY = screenY + screenH / 2f;
                    float radians = refLayer.Rotation * MathF.PI / 180f;

                    var oldTransform = renderer.Transform;
                    renderer.Transform = Matrix3x2.CreateRotation(radians, new Vector2(centerX, centerY));

                    renderer.DrawPixels(refLayer.Pixels, refLayer.ImageWidth, refLayer.ImageHeight,
                        destRect, srcRect, opacity, ImageInterpolation.HighQualityCubic);

                    renderer.Transform = oldTransform;
                }

                if (_selectedReferenceLayer == refLayer)
                {
                    DrawReferenceLayerSelection(renderer, screenX, screenY, screenW, screenH, refLayer.Rotation, refLayer.Locked);
                }
            }
            catch
            {
                // Skip layers that fail to render
            }
        }

        private void DrawReferenceLayerSelection(ICanvasRenderer renderer, float x, float y, float w, float h, float rotation, bool isLocked)
        {
            var outlineColor = isLocked ? Color.FromArgb(200, 128, 128, 128) : Color.FromArgb(255, 0, 200, 255);
            var handleColor = isLocked ? Color.FromArgb(200, 100, 100, 100) : Color.FromArgb(255, 255, 255, 255);
            float outlineWidth = 2f;
            float handleSize = 8f;

            if (Math.Abs(rotation) < 0.01f)
            {
                renderer.DrawRectangle(x, y, w, h, outlineColor, outlineWidth);

                if (!isLocked)
                {
                    DrawReferenceHandle(renderer, x, y, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(renderer, x + w, y, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(renderer, x, y + h, handleSize, handleColor, outlineColor);
                    DrawReferenceHandle(renderer, x + w, y + h, handleSize, handleColor, outlineColor);
                }
            }
            else
            {
                // TODO: Implement rotated selection drawing
            }
        }

        private static void DrawReferenceHandle(ICanvasRenderer renderer, float x, float y, float size, Color fill, Color stroke)
        {
            float half = size / 2f;
            renderer.FillRectangle(x - half, y - half, size, size, fill);
            renderer.DrawRectangle(x - half, y - half, size, size, stroke, 1f);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - ONION SKINNING (placeholder)
        // ════════════════════════════════════════════════════════════════════

        private void DrawOnionSkinFrames(ICanvasRenderer renderer, Rect dest)
        {
            // TODO: Implement onion skinning with SkiaSharp
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - DOCUMENT SURFACE
        // ════════════════════════════════════════════════════════════════════

        private void DrawDocumentSurface(ICanvasRenderer renderer, Rect dest)
        {
            var surf = _composite ?? Document.Surface;
            var srcRect = new Rect(0, 0, surf.Width, surf.Height);

            renderer.DrawPixels(surf.Pixels, surf.Width, surf.Height,
                dest, srcRect, 1.0f, ImageInterpolation.NearestNeighbor);

            // Draw mask overlay when editing a mask
            DrawMaskOverlay(renderer, dest);
        }

        private void DrawMaskOverlay(ICanvasRenderer renderer, Rect dest)
        {
            var activeLayer = Document.ActiveLayer;
            if (activeLayer == null || !activeLayer.IsEditingMask || activeLayer.Mask == null)
                return;

            var mask = activeLayer.Mask;
            var maskPixels = mask.Surface.Pixels;
            var overlayPixels = new byte[maskPixels.Length];

            for (int i = 0; i < maskPixels.Length; i += 4)
            {
                byte maskValue = maskPixels[i];

                if (maskValue >= 250)
                {
                    overlayPixels[i + 0] = 0;
                    overlayPixels[i + 1] = 0;
                    overlayPixels[i + 2] = 0;
                    overlayPixels[i + 3] = 0;
                }
                else
                {
                    byte alpha = (byte)((255 - maskValue) * 0.6f);
                    overlayPixels[i + 0] = 60;
                    overlayPixels[i + 1] = 40;
                    overlayPixels[i + 2] = 220;
                    overlayPixels[i + 3] = alpha;
                }
            }

            var srcRect = new Rect(0, 0, mask.Width, mask.Height);
            renderer.DrawPixels(overlayPixels, mask.Width, mask.Height,
                dest, srcRect, 1.0f, ImageInterpolation.NearestNeighbor);
        }

        private void DrawDocumentBorder(ICanvasRenderer renderer, Rect dest)
        {
            renderer.DrawRectangle(dest, Color.FromArgb(255, 0, 0, 0), 1f);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - CHECKERBOARD BACKGROUND
        // ════════════════════════════════════════════════════════════════════

        private void DrawCheckerboardBackground(ICanvasRenderer renderer, Rect dest)
        {
            // Get theme-aware colors from the pattern service
            _patternService.SyncWith(ActualTheme);
            var (lightColor, darkColor) = _patternService.CurrentScheme;

            // Calculate checkerboard square size (in screen pixels, consistent at all zoom levels)
            int squareSize = 8; // 8px squares

            // Get the underlying SKCanvas from the renderer
            if (renderer.Device is not SKCanvas canvas)
            {
                // Fallback: just fill with light color
                renderer.FillRectangle(dest, lightColor);
                return;
            }

            // Create or update cached checkerboard shader
            EnsureCheckerboardShader(squareSize, lightColor, darkColor);

            if (_checkerboardShader == null)
            {
                // Fallback: just fill with light color
                renderer.FillRectangle(dest, lightColor);
                return;
            }

            // Draw the checkerboard using the shader
            using var paint = new SKPaint
            {
                Shader = _checkerboardShader,
                IsAntialias = false
            };

            canvas.DrawRect(dest.ToSKRect(), paint);
        }

        /// <summary>
        /// Ensures the checkerboard shader is created and up to date.
        /// </summary>
        private void EnsureCheckerboardShader(int squareSize, Color lightColor, Color darkColor)
        {
            // Check if we need to rebuild the shader
            bool needsRebuild = _checkerboardBitmap == null ||
                                _checkerboardShader == null;

            if (!needsRebuild)
                return;

            // Dispose old resources
            _checkerboardShader?.Dispose();
            _checkerboardBitmap?.Dispose();

            // Create a small checkerboard bitmap (2x2 squares)
            int tileSize = squareSize * 2;
            _checkerboardBitmap = new SKBitmap(tileSize, tileSize, SKColorType.Bgra8888, SKAlphaType.Premul);

            var skLight = new SKColor(lightColor.R, lightColor.G, lightColor.B, lightColor.A);
            var skDark = new SKColor(darkColor.R, darkColor.G, darkColor.B, darkColor.A);

            // Fill the bitmap with checkerboard pattern
            for (int y = 0; y < tileSize; y++)
            {
                for (int x = 0; x < tileSize; x++)
                {
                    int cx = x / squareSize;
                    int cy = y / squareSize;
                    bool isLight = ((cx + cy) & 1) == 0;
                    _checkerboardBitmap.SetPixel(x, y, isLight ? skLight : skDark);
                }
            }

            // Create tiled shader from the bitmap
            using var image = SKImage.FromBitmap(_checkerboardBitmap);
            _checkerboardShader = image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        }

        /// <summary>
        /// Invalidates the checkerboard pattern cache (call when theme changes).
        /// </summary>
        private void InvalidateCheckerboardCache()
        {
            _checkerboardShader?.Dispose();
            _checkerboardShader = null;
            _checkerboardBitmap?.Dispose();
            _checkerboardBitmap = null;
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - GRID OVERLAYS
        // ════════════════════════════════════════════════════════════════════

        private void DrawPixelGrid(ICanvasRenderer renderer, Rect dest)
        {
            float s = (float)_zoom.Scale;
            if (s < 6f) return;

            var gridColor = Color.FromArgb(255, 105, 105, 105); // DimGray

            for (int x = 0; x <= Document.Surface.Width; x++)
            {
                float sx = (float)(dest.X + x * s);
                renderer.DrawLine(sx, (float)dest.Y, sx, (float)(dest.Y + dest.Height), gridColor, 1f);
            }

            for (int y = 0; y <= Document.Surface.Height; y++)
            {
                float sy = (float)(dest.Y + y * s);
                renderer.DrawLine((float)dest.X, sy, (float)(dest.X + dest.Width), sy, gridColor, 1f);
            }
        }

        private void DrawTileGrid(ICanvasRenderer renderer, Rect dest)
        {
            float s = (float)_zoom.Scale;
            var gridColor = Color.FromArgb(255, 105, 105, 105); // DimGray

            for (int x = Document.TileSize.Width; x <= Document.TileCounts.Width * Document.TileSize.Width; x += Document.TileSize.Width)
            {
                float sx = (float)(dest.X + x * s);
                renderer.DrawLine(sx, (float)dest.Y, sx, (float)(dest.Y + dest.Height), gridColor, 1f);
            }

            for (int y = Document.TileSize.Height; y <= Document.TileCounts.Height * Document.TileSize.Height; y += Document.TileSize.Height)
            {
                float sy = (float)(dest.Y + y * s);
                renderer.DrawLine((float)dest.X, sy, (float)(dest.X + dest.Width), sy, gridColor, 1f);
            }
        }

        private void DrawTileMappings(ICanvasRenderer renderer, Rect dest)
        {
            // TODO: Implement tile mappings with SkiaSharp text rendering
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - TILE ANIMATION FRAMES
        // ════════════════════════════════════════════════════════════════════

        private void DrawTileAnimationFrames(ICanvasRenderer renderer, Rect dest)
        {
            // TODO: Implement tile animation frame rendering
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - GUIDES
        // ════════════════════════════════════════════════════════════════════

        private void DrawGuides(ICanvasRenderer renderer, Rect dest)
        {
            if (_guideService == null || !_guideService.GuidesVisible)
                return;

            float scale = (float)_zoom.Scale;
            float renderWidth = renderer.Width;
            float renderHeight = renderer.Height;

            foreach (var guide in _guideService.HorizontalGuides)
            {
                float screenY = (float)(dest.Y + guide.Position * scale);
                var color = guide.IsSelected ? GuideColorSelected : GuideColor;
                renderer.DrawLine(0, screenY, renderWidth, screenY, color, 1f);
            }

            foreach (var guide in _guideService.VerticalGuides)
            {
                float screenX = (float)(dest.X + guide.Position * scale);
                var color = guide.IsSelected ? GuideColorSelected : GuideColor;
                renderer.DrawLine(screenX, 0, screenX, renderHeight, color, 1f);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - STAGE OVERLAY
        // ════════════════════════════════════════════════════════════════════

        private void DrawStageOverlay(ICanvasRenderer renderer, Rect dest)
        {
            if (_animationMode != Animation.AnimationMode.Canvas)
                return;

            var animState = Document.CanvasAnimationState;
            if (animState == null || !animState.Stage.Enabled)
                return;

            var stage = animState.Stage;
            float scale = (float)_zoom.Scale;

            var outlineColor = _stageSelected ? StageOutlineSelectedColor : StageOutlineColor;
            float outlineWidth = _stageSelected ? 3f : 2f;

            float stageX = stage.StageX;
            float stageY = stage.StageY;
            float stageW = stage.StageWidth;
            float stageH = stage.StageHeight;

            float screenStageX = (float)(dest.X + stageX * scale);
            float screenStageY = (float)(dest.Y + stageY * scale);
            float screenStageW = stageW * scale;
            float screenStageH = stageH * scale;

            renderer.DrawRectangle(screenStageX, screenStageY, screenStageW, screenStageH, outlineColor, outlineWidth);
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - SYMMETRY OVERLAY
        // ════════════════════════════════════════════════════════════════════

        private void DrawSymmetryOverlay(ICanvasRenderer renderer, Rect dest)
        {
            if (_symmetryService == null || !_symmetryService.IsActive)
                return;

            var settings = _toolState?.Symmetry;
            if (settings == null || !settings.ShowAxisLines)
                return;

            float scale = (float)_zoom.Scale;
            int docWidth = Document.PixelWidth;
            int docHeight = Document.PixelHeight;

            foreach (var (x1, y1, x2, y2) in _symmetryService.GetAxisLines(docWidth, docHeight))
            {
                float screenX1 = (float)(dest.X + x1 * scale);
                float screenY1 = (float)(dest.Y + y1 * scale);
                float screenX2 = (float)(dest.X + x2 * scale);
                float screenY2 = (float)(dest.Y + y2 * scale);

                renderer.DrawLine(screenX1, screenY1, screenX2, screenY2, SymmetryAxisColor, 2f);
            }

            if (settings.IsRadialMode)
            {
                var (centerX, centerY) = _symmetryService.GetAxisCenter(docWidth, docHeight);
                float screenCenterX = (float)(dest.X + centerX * scale);
                float screenCenterY = (float)(dest.Y + centerY * scale);

                float crossSize = 8f;
                renderer.DrawLine(screenCenterX - crossSize, screenCenterY, screenCenterX + crossSize, screenCenterY, SymmetryCenterColor, 2f);
                renderer.DrawLine(screenCenterX, screenCenterY - crossSize, screenCenterX, screenCenterY + crossSize, SymmetryCenterColor, 2f);

                renderer.DrawEllipse(screenCenterX, screenCenterY, 6f, 6f, SymmetryCenterColor, 2f);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CONTRAST INK HELPER
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

        private LayerBase? FindLayerByGuid(Guid layerId)
        {
            foreach (var layer in Document.GetFlattenedLayers())
            {
                if (layer.Id == layerId)
                    return layer;
            }
            return null;
        }
    }
}
