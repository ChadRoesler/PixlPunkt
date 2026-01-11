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

        // ════════════════════════════════════════════════════════════════════
        // CACHED RENDERING OBJECTS - Reused across frames to minimize GC
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Cached paint for tile mapping text rendering.</summary>
        private SKPaint? _tileMappingTextPaint;
        
        /// <summary>Cached paint for tile mapping background.</summary>
        private SKPaint? _tileMappingBgPaint;
        
        /// <summary>Cached paint for tile animation text rendering.</summary>
        private SKPaint? _tileAnimTextPaint;
        
        /// <summary>Cached paint for tile animation background.</summary>
        private SKPaint? _tileAnimBgPaint;
        
        /// <summary>Cached paint for checkerboard rendering - reused to avoid per-frame allocation.</summary>
        private SKPaint? _checkerboardPaint;
        
        /// <summary>Cached dictionary for tile animation frame positions.</summary>
        private Dictionary<(int, int), List<int>>? _cachedFramePositions;
        
        /// <summary>Cached list for reuse in frame position building.</summary>
        private List<int>? _cachedFrameIndexList;

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

        // ════════════════════════════════════════════════════════════════════
        // CACHED MASK OVERLAY BUFFER
        // ════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Cached buffer for mask overlay rendering to avoid per-frame allocation.
        /// </summary>
        private byte[]? _cachedMaskOverlayBuffer;
        private int _cachedMaskOverlayWidth;
        private int _cachedMaskOverlayHeight;

        private void DrawMaskOverlay(ICanvasRenderer renderer, Rect dest)
        {
            var activeLayer = Document.ActiveLayer;
            if (activeLayer == null || !activeLayer.IsEditingMask || activeLayer.Mask == null)
                return;

            var mask = activeLayer.Mask;
            var maskPixels = mask.Surface.Pixels;
            int maskWidth = mask.Width;
            int maskHeight = mask.Height;

            // Reuse cached buffer if dimensions match, otherwise allocate new one
            if (_cachedMaskOverlayBuffer == null ||
                _cachedMaskOverlayWidth != maskWidth ||
                _cachedMaskOverlayHeight != maskHeight)
            {
                _cachedMaskOverlayBuffer = new byte[maskPixels.Length];
                _cachedMaskOverlayWidth = maskWidth;
                _cachedMaskOverlayHeight = maskHeight;
            }

            var overlayPixels = _cachedMaskOverlayBuffer;

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

            var srcRect = new Rect(0, 0, maskWidth, maskHeight);
            renderer.DrawPixels(overlayPixels, maskWidth, maskHeight,
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

            // Ensure cached paint exists and has correct shader
            if (_checkerboardPaint == null)
            {
                _checkerboardPaint = new SKPaint
                {
                    IsAntialias = false
                };
            }
            _checkerboardPaint.Shader = _checkerboardShader;

            canvas.DrawRect(dest.ToSKRect(), _checkerboardPaint);
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
            _checkerboardPaint?.Dispose();
            _checkerboardPaint = null;
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
            if (Document?.ActiveLayer is not RasterLayer rl)
                return;

            var tileMapping = rl.TileMapping;
            if (tileMapping == null)
                return;

            // Get the SKCanvas from the renderer for text drawing
            if (renderer.Device is not SKCanvas canvas) return;

            int tileW = Document.TileSize.Width;
            int tileH = Document.TileSize.Height;
            float scale = (float)_zoom.Scale;

            // Only draw if tiles are large enough to see the text
            float minTileScreenSize = 24f;
            if (tileW * scale < minTileScreenSize || tileH * scale < minTileScreenSize)
                return;

            float fontSize = Math.Max(8f, Math.Min(14f, tileW * scale * 0.3f));

            // Initialize or update cached text paint
            if (_tileMappingTextPaint == null)
            {
                _tileMappingTextPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                };
            }
            _tileMappingTextPaint.TextSize = fontSize;

            // Initialize cached background paint
            _tileMappingBgPaint ??= new SKPaint
            {
                Color = new SKColor(0, 0, 0, 180),
                IsAntialias = false
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
                    float textW = _tileMappingTextPaint.MeasureText(text);
                    float textH = fontSize;

                    var bgRect = new SKRect(screenX + 2, screenY + 2, screenX + 2 + textW + 4, screenY + 2 + textH + 2);
                    canvas.DrawRect(bgRect, _tileMappingBgPaint);

                    // Draw text
                    canvas.DrawText(text, screenX + 4, screenY + 2 + textH, _tileMappingTextPaint);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // RENDERING - TILE ANIMATION FRAMES
        // ════════════════════════════════════════════════════════════════════

        private void DrawTileAnimationFrames(ICanvasRenderer renderer, Rect dest)
        {
            if (Document == null) return;

            var animState = Document.TileAnimationState;
            if (animState == null) return;

            var reel = animState.SelectedReel;
            if (reel == null || reel.FrameCount == 0) return;

            int tileW = Document.TileSize.Width;
            int tileH = Document.TileSize.Height;
            float scale = (float)_zoom.Scale;

            // Get the current tile position
            var (currentTileX, currentTileY) = animState.CurrentTilePosition;

            // Build a set of all frame positions and their frame indices
            if (_cachedFramePositions == null)
                _cachedFramePositions = new Dictionary<(int, int), List<int>>();

            _cachedFramePositions.Clear();

            for (int i = 0; i < reel.FrameCount; i++)
            {
                var frame = reel.Frames[i];
                var key = (frame.TileX, frame.TileY);
                if (!_cachedFramePositions.ContainsKey(key))
                    _cachedFramePositions[key] = new List<int>();
                _cachedFramePositions[key].Add(i);
            }

            // Draw all frame positions
            foreach (var kvp in _cachedFramePositions)
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
                renderer.FillRectangle(screenX, screenY, screenW, screenH, fillColor);

                // Draw outline
                var outlineColor = isCurrent ? TileAnimCurrentOutline : TileAnimFrameOutline;
                renderer.DrawRectangle(screenX, screenY, screenW, screenH, outlineColor, isCurrent ? 3f : 2f);

                // Draw frame number label(s) in the top-left
                DrawTileAnimationLabel(renderer, screenX, screenY, screenW, frameIndices, isCurrent);
            }
        }

        /// <summary>
        /// Draws frame number labels on a tile animation frame.
        /// </summary>
        private void DrawTileAnimationLabel(ICanvasRenderer renderer, float screenX, float screenY, float screenW,
            System.Collections.Generic.List<int> frameIndices, bool isCurrent)
        {
            // Only draw if tiles are large enough to see the text
            if (screenW < 24f)
                return;

            if (renderer.Device is not SKCanvas canvas) return;

            // Lazy-create text paint if needed
            if (_tileAnimTextPaint == null)
            {
                _tileAnimTextPaint = new SKPaint
                {
                    Color = isCurrent ? SKColors.White : new SKColor(200, 220, 255),
                    TextSize = 12f,
                    IsAntialias = true,
                    Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
                };
            }

            // Lazy-create background paint if needed
            if (_tileAnimBgPaint == null)
            {
                _tileAnimBgPaint = new SKPaint
                {
                    Color = new SKColor(0, 50, 100, 200),
                    IsAntialias = true
                };
            }

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

            float textW = _tileAnimTextPaint.MeasureText(label);
            float textH = _tileAnimTextPaint.TextSize;

            float labelX = screenX + 2;
            float labelY = screenY + 2;

            // Draw background
            var bgColor = isCurrent ? new SKColor(80, 60, 0, 220) : new SKColor(0, 50, 100, 200);
            using var bgPaint = new SKPaint
            {
                Color = bgColor,
                IsAntialias = true
            };

            var bgRect = new SKRect(labelX, labelY, labelX + textW + 4, labelY + textH + 2);
            canvas.DrawRoundRect(bgRect, 2, 2, bgPaint);

            // Draw text
            canvas.DrawText(label, labelX + 2, labelY + textH, _tileAnimTextPaint);
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
