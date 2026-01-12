using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using PixlPunkt.Core.Rendering;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// Partial class for interleaved rendering of layers and sub-routines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This partial class extends <see cref="CanvasViewHost"/> with functionality to composite
    /// animation sub-routines at the correct Z-order relative to document layers. Sub-routines
    /// are tile animations that can be embedded within canvas animations with transform keyframes.
    /// </para>
    /// <para><strong>Z-Order System:</strong></para>
    /// <para>
    /// The Z-order system determines the visual stacking order of layers and sub-routines:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Lower Z = Behind:</strong> Items with lower Z-order values render first and appear behind items with higher Z values.</item>
    /// <item><strong>Higher Z = In Front:</strong> Items with higher Z-order values render last and appear in front of items with lower Z values.</item>
    /// </list>
    /// <para><strong>Layer Z-Order Calculation:</strong></para>
    /// <para>
    /// Layer Z-orders are derived from their track position in <see cref="Core.Animation.CanvasAnimationState.Tracks"/>:
    /// </para>
    /// <list type="bullet">
    /// <item>Track 0 (top of UI, e.g., "Foreground") ? Z = trackCount - 1 (highest)</item>
    /// <item>Track N-1 (bottom of UI, e.g., "Background") ? Z = 0 (lowest)</item>
    /// </list>
    /// <para><strong>Sub-Routine Z-Order:</strong></para>
    /// <para>
    /// Sub-routines have an explicit <see cref="Core.Animation.AnimationSubRoutine.ZOrder"/> property
    /// that can be modified via context menu (Move Up/Down, Bring to Front/Send to Back).
    /// </para>
    /// <para><strong>Tie-Breaking Rules (Same Z-Order):</strong></para>
    /// <para>
    /// When items share the same Z-order value:
    /// </para>
    /// <list type="number">
    /// <item>Sub-routines render BEFORE (behind) layers at the same Z level.</item>
    /// <item>Among multiple sub-routines at the same Z, items lower in the timeline UI render first (behind).</item>
    /// </list>
    /// <para>
    /// This ensures the visual timeline order matches the rendered output: items appearing lower
    /// in the timeline panel are rendered behind items appearing higher.
    /// </para>
    /// </remarks>
    /// <seealso cref="Core.Animation.AnimationSubRoutine"/>
    /// <seealso cref="Core.Animation.SubRoutineRenderInfo"/>
    /// <seealso cref="Animation.AnimationPanel"/>
    public sealed partial class CanvasViewHost
    {
        // Sub-routine overlay colors
        private static readonly Color SubRoutineOutlineColor = Color.FromArgb(255, 100, 200, 255); // Light blue
        private static readonly Color SubRoutineOutlineSelectedColor = Color.FromArgb(255, 255, 150, 50); // Orange when selected
        private static readonly Color SubRoutineHandleColor = Color.FromArgb(255, 255, 255, 255);

        /// <summary>
        /// Draws the document surface with sub-routines interleaved based on Z-order.
        /// </summary>
        /// <param name="renderer">The canvas renderer.</param>
        /// <param name="dest">The destination rectangle in screen coordinates.</param>
        private void DrawDocumentWithInterleavedSubRoutines(ICanvasRenderer renderer, Rect dest)
        {
            var animState = Document?.CanvasAnimationState;

            // If not in canvas animation mode, use simple rendering
            if (_animationMode != Animation.AnimationMode.Canvas || animState == null)
            {
                DrawSimpleCompositeInternal(renderer, dest);
                return;
            }

            // Update sub-routine state for current frame
            int currentFrame = animState.CurrentFrameIndex;
            animState.SubRoutineState.UpdateForFrame(currentFrame);

            // Get active sub-routines sorted by Z-order
            var activeSubRoutines = animState.SubRoutineState.GetRenderInfo(currentFrame)
                .OrderBy(r => r.ZOrder)
                .ToList();

            if (activeSubRoutines.Count == 0)
            {
                // No active sub-routines, use simple rendering
                DrawSimpleCompositeInternal(renderer, dest);
                DrawSubRoutineOverlayInternal(renderer, dest); // Still draw selection handles if any
                return;
            }

            // Complex path: interleave sub-routines with layers based on Z-order
            DrawInterleavedCompositeInternal(renderer, dest, activeSubRoutines);
        }

        /// <summary>
        /// Simple composite rendering: all layers composited together, then drawn.
        /// </summary>
        private void DrawSimpleCompositeInternal(ICanvasRenderer renderer, Rect dest)
        {
            EnsureComposite();
            Document.CompositeTo(_composite!);
            FrameReady?.Invoke(_composite!.Pixels, _composite.Width, _composite.Height);
            DrawDocumentSurface(renderer, dest);
        }

        /// <summary>
        /// Interleaved composite rendering: layers and sub-routines composited in Z-order.
        /// </summary>
        private void DrawInterleavedCompositeInternal(ICanvasRenderer renderer, Rect dest,
            List<Core.Animation.SubRoutineRenderInfo> activeSubRoutines)
        {
            var animState = Document.CanvasAnimationState;
            
            // Get all visible layers (we need these for actual rendering)
            var visibleLayers = Document.VisibleBottomToTop().ToList();

            // Build a lookup from layer ID to its RasterLayer
            var layerIdToLayer = new Dictionary<Guid, Core.Document.Layer.RasterLayer>();
            foreach (var layer in visibleLayers)
            {
                if (layer is Core.Document.Layer.RasterLayer rasterLayer)
                {
                    layerIdToLayer[layer.Id] = rasterLayer;
                }
            }

            float scale = (float)_zoom.Scale;
            EnsureComposite();
            _composite!.Clear(0x00000000);

            // Build ordered list using the SAME logic as AnimationPanel.BuildOrderedTrackList()
            var renderItems = new List<(int zOrder, bool isLayer, int insertionOrder, Core.Document.Layer.RasterLayer? layer, Core.Animation.SubRoutineRenderInfo? subRoutine, string name)>();

            int trackCount = animState.Tracks.Count;
            int insertionCounter = 0;

            // Add layers with inverted index as Z-order
            for (int i = 0; i < trackCount; i++)
            {
                var track = animState.Tracks[i];
                
                if (layerIdToLayer.TryGetValue(track.LayerId, out var rasterLayer))
                {
                    int zOrder = trackCount - 1 - i;
                    renderItems.Add((zOrder, true, insertionCounter++, rasterLayer, null, track.LayerName));
                }
            }

            // Add sub-routines with their ZOrder
            int subRoutineIndex = 0;
            foreach (var subRoutine in activeSubRoutines)
            {
                renderItems.Add((subRoutine.ZOrder, false, subRoutineIndex++, null, subRoutine, subRoutine.SubRoutine.DisplayName));
            }

            // Sort for RENDERING (ascending Z = lowest first = behind)
            renderItems.Sort((a, b) =>
            {
                int cmp = a.zOrder.CompareTo(b.zOrder);
                if (cmp != 0) return cmp;
                
                int typeCmp = a.isLayer.CompareTo(b.isLayer);
                if (typeCmp != 0) return typeCmp;
                
                if (!a.isLayer && !b.isLayer)
                {
                    return b.insertionOrder.CompareTo(a.insertionOrder);
                }
                
                return a.insertionOrder.CompareTo(b.insertionOrder);
            });

            // Render in Z-order
            foreach (var item in renderItems)
            {
                if (item.isLayer && item.layer != null)
                {
                    CompositeLayerToComposite(item.layer);
                }
                else if (item.subRoutine != null)
                {
                    CompositeSubRoutineToComposite(item.subRoutine);
                }
            }

            FrameReady?.Invoke(_composite.Pixels, _composite.Width, _composite.Height);

            // Draw the interleaved composite using the renderer
            var srcRect = new Rect(0, 0, _composite.Width, _composite.Height);
            renderer.DrawPixels(_composite.Pixels, _composite.Width, _composite.Height,
                dest, srcRect, 1.0f, ImageInterpolation.NearestNeighbor);

            // Draw mask overlay when editing a mask
            DrawMaskOverlay(renderer, dest);

            // Draw selection handles for selected sub-routine (overlays, not composited)
            if (_selectedSubRoutine != null)
            {
                var selectedInfo = activeSubRoutines.FirstOrDefault(r => r.SubRoutine == _selectedSubRoutine);
                if (selectedInfo != null)
                {
                    DrawSubRoutineSelectionOverlayForInterleaved(renderer, dest, scale, selectedInfo);
                }
            }
        }

        /// <summary>
        /// Draws sub-routine selection overlay when no interleaved rendering is needed.
        /// </summary>
        private void DrawSubRoutineOverlayInternal(ICanvasRenderer renderer, Rect dest)
        {
            if (_selectedSubRoutine == null || _animationMode != Animation.AnimationMode.Canvas)
                return;

            var animState = Document?.CanvasAnimationState;
            if (animState == null)
                return;

            int currentFrame = animState.CurrentFrameIndex;
            var activeSubRoutines = animState.SubRoutineState.GetRenderInfo(currentFrame).ToList();

            float scale = (float)_zoom.Scale;
            var selectedInfo = activeSubRoutines.FirstOrDefault(r => r.SubRoutine == _selectedSubRoutine);
            if (selectedInfo != null)
            {
                DrawSubRoutineSelectionOverlayForInterleaved(renderer, dest, scale, selectedInfo);
            }
        }

        /// <summary>
        /// Composites a single layer onto the internal composite surface.
        /// </summary>
        private void CompositeLayerToComposite(Core.Document.Layer.RasterLayer layer)
        {
            if (_composite == null) return;

            var srcPixels = layer.Surface.Pixels;
            var dstPixels = _composite.Pixels;
            int width = _composite.Width;
            int height = _composite.Height;

            if (layer.Surface.Width != width || layer.Surface.Height != height)
                return;

            float opacityF = layer.Opacity / 255f;

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

        /// <summary>
        /// Composites a sub-routine's current frame onto the composite surface.
        /// </summary>
        private void CompositeSubRoutineToComposite(Core.Animation.SubRoutineRenderInfo renderInfo)
        {
            if (_composite == null) return;
            if (renderInfo.FramePixels == null || renderInfo.FrameWidth <= 0 || renderInfo.FrameHeight <= 0)
                return;

            int srcW = renderInfo.FrameWidth;
            int srcH = renderInfo.FrameHeight;
            float spriteScale = renderInfo.Scale;
            float rotation = renderInfo.Rotation;

            int scaledW = (int)(srcW * spriteScale);
            int scaledH = (int)(srcH * spriteScale);

            if (scaledW <= 0 || scaledH <= 0)
                return;

            int destX = (int)renderInfo.PositionX;
            int destY = (int)renderInfo.PositionY;

            var srcPixels = renderInfo.FramePixels;
            var dstPixels = _composite.Pixels;
            int dstW = _composite.Width;
            int dstH = _composite.Height;

            // Handle rotation
            if (Math.Abs(rotation) > 0.01f)
            {
                CompositeSubRoutineRotated(srcPixels, srcW, srcH, spriteScale, rotation, destX, destY, scaledW, scaledH, dstPixels, dstW, dstH);
            }
            else
            {
                CompositeSubRoutineUnrotated(srcPixels, srcW, srcH, spriteScale, destX, destY, scaledW, scaledH, dstPixels, dstW, dstH);
            }
        }

        /// <summary>
        /// Composites an unrotated sub-routine sprite using nearest-neighbor sampling.
        /// </summary>
        private static void CompositeSubRoutineUnrotated(byte[] srcPixels, int srcW, int srcH, float spriteScale,
            int destX, int destY, int scaledW, int scaledH, byte[] dstPixels, int dstW, int dstH)
        {
            for (int dy = 0; dy < scaledH; dy++)
            {
                int finalY = destY + dy;
                if (finalY < 0 || finalY >= dstH) continue;

                int srcY = (int)(dy / spriteScale);
                if (srcY >= srcH) srcY = srcH - 1;

                for (int dx = 0; dx < scaledW; dx++)
                {
                    int finalX = destX + dx;
                    if (finalX < 0 || finalX >= dstW) continue;

                    int srcX = (int)(dx / spriteScale);
                    if (srcX >= srcW) srcX = srcW - 1;

                    int srcIdx = (srcY * srcW + srcX) * 4;
                    int dstIdx = (finalY * dstW + finalX) * 4;

                    byte srcB = srcPixels[srcIdx];
                    byte srcG = srcPixels[srcIdx + 1];
                    byte srcR = srcPixels[srcIdx + 2];
                    byte srcA = srcPixels[srcIdx + 3];

                    if (srcA == 0) continue;

                    // Alpha composite
                    byte dstB = dstPixels[dstIdx];
                    byte dstG = dstPixels[dstIdx + 1];
                    byte dstR = dstPixels[dstIdx + 2];
                    byte dstA = dstPixels[dstIdx + 3];

                    float sa = srcA / 255f;
                    float da = dstA / 255f;
                    float outA = sa + da * (1 - sa);

                    if (outA > 0)
                    {
                        dstPixels[dstIdx] = (byte)((srcB * sa + dstB * da * (1 - sa)) / outA);
                        dstPixels[dstIdx + 1] = (byte)((srcG * sa + dstG * da * (1 - sa)) / outA);
                        dstPixels[dstIdx + 2] = (byte)((srcR * sa + dstR * da * (1 - sa)) / outA);
                        dstPixels[dstIdx + 3] = (byte)(outA * 255);
                    }
                }
            }
        }

        /// <summary>
        /// Composites a rotated sub-routine sprite using nearest-neighbor sampling.
        /// </summary>
        private static void CompositeSubRoutineRotated(byte[] srcPixels, int srcW, int srcH, float spriteScale, float rotation,
            int destX, int destY, int scaledW, int scaledH, byte[] dstPixels, int dstW, int dstH)
        {
            float radians = rotation * MathF.PI / 180f;
            float cos = MathF.Cos(-radians);
            float sin = MathF.Sin(-radians);
            float centerX = destX + scaledW / 2f;
            float centerY = destY + scaledH / 2f;

            for (int dy = 0; dy < scaledH; dy++)
            {
                for (int dx = 0; dx < scaledW; dx++)
                {
                    // Calculate rotated position
                    float localX = dx - scaledW / 2f;
                    float localY = dy - scaledH / 2f;
                    float rotX = localX * cos - localY * sin;
                    float rotY = localX * sin + localY * cos;

                    int finalX = (int)(centerX + rotX);
                    int finalY = (int)(centerY + rotY);

                    if (finalX < 0 || finalX >= dstW || finalY < 0 || finalY >= dstH)
                        continue;

                    // Sample from source (nearest neighbor)
                    int srcX = (int)(dx / spriteScale);
                    int srcY = (int)(dy / spriteScale);
                    if (srcX >= srcW) srcX = srcW - 1;
                    if (srcY >= srcH) srcY = srcH - 1;

                    int srcIdx = (srcY * srcW + srcX) * 4;
                    int dstIdx = (finalY * dstW + finalX) * 4;

                    byte srcB = srcPixels[srcIdx];
                    byte srcG = srcPixels[srcIdx + 1];
                    byte srcR = srcPixels[srcIdx + 2];
                    byte srcA = srcPixels[srcIdx + 3];

                    if (srcA == 0) continue;

                    // Alpha composite
                    byte dstB = dstPixels[dstIdx];
                    byte dstG = dstPixels[dstIdx + 1];
                    byte dstR = dstPixels[dstIdx + 2];
                    byte dstA = dstPixels[dstIdx + 3];

                    float sa = srcA / 255f;
                    float da = dstA / 255f;
                    float outA = sa + da * (1 - sa);

                    if (outA > 0)
                    {
                        dstPixels[dstIdx] = (byte)((srcB * sa + dstB * da * (1 - sa)) / outA);
                        dstPixels[dstIdx + 1] = (byte)((srcG * sa + dstG * da * (1 - sa)) / outA);
                        dstPixels[dstIdx + 2] = (byte)((srcR * sa + dstR * da * (1 - sa)) / outA);
                        dstPixels[dstIdx + 3] = (byte)(outA * 255);
                    }
                }
            }
        }

        /// <summary>
        /// Draws selection handles for a sub-routine as an overlay (not composited).
        /// </summary>
        private void DrawSubRoutineSelectionOverlayForInterleaved(ICanvasRenderer renderer, Rect dest, float scale,
            Core.Animation.SubRoutineRenderInfo renderInfo)
        {
            double posX = renderInfo.PositionX;
            double posY = renderInfo.PositionY;
            float spriteScale = renderInfo.Scale;

            int scaledWidth = (int)(renderInfo.FrameWidth * spriteScale);
            int scaledHeight = (int)(renderInfo.FrameHeight * spriteScale);

            if (scaledWidth <= 0 || scaledHeight <= 0)
                return;

            float screenX = (float)(dest.X + posX * scale);
            float screenY = (float)(dest.Y + posY * scale);
            float screenW = scaledWidth * scale;
            float screenH = scaledHeight * scale;

            DrawSubRoutineSelectionInternal(renderer, screenX, screenY, screenW, screenH, renderInfo.Rotation);
        }

        /// <summary>
        /// Draws selection handles around a selected sub-routine.
        /// </summary>
        private void DrawSubRoutineSelectionInternal(ICanvasRenderer renderer, float x, float y, float w, float h, float rotation)
        {
            var outlineColor = SubRoutineOutlineSelectedColor;
            var handleColor = SubRoutineHandleColor;
            float outlineWidth = 2f;
            float handleSize = 8f;

            if (Math.Abs(rotation) < 0.01f)
            {
                // No rotation - draw simple rectangle
                renderer.DrawRectangle(x, y, w, h, outlineColor, outlineWidth);

                // Draw corner handles
                DrawSubRoutineHandle(renderer, x, y, handleSize, handleColor, outlineColor);
                DrawSubRoutineHandle(renderer, x + w, y, handleSize, handleColor, outlineColor);
                DrawSubRoutineHandle(renderer, x, y + h, handleSize, handleColor, outlineColor);
                DrawSubRoutineHandle(renderer, x + w, y + h, handleSize, handleColor, outlineColor);

                // Draw center handle (for moving)
                float centerX = x + w / 2f;
                float centerY = y + h / 2f;
                renderer.FillEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, handleColor);
                renderer.DrawEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, outlineColor, 1f);
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
                    renderer.DrawLine(corners[i], corners[next], outlineColor, outlineWidth);
                }

                // Draw corner handles at rotated positions
                foreach (var corner in corners)
                {
                    DrawSubRoutineHandle(renderer, corner.X, corner.Y, handleSize, handleColor, outlineColor);
                }

                // Draw center handle
                renderer.FillEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, handleColor);
                renderer.DrawEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, outlineColor, 1f);
            }
        }

        /// <summary>
        /// Draws a single sub-routine selection handle (square).
        /// </summary>
        private static void DrawSubRoutineHandle(ICanvasRenderer renderer, float x, float y, float size, Color fill, Color stroke)
        {
            float half = size / 2f;
            renderer.FillRectangle(x - half, y - half, size, size, fill);
            renderer.DrawRectangle(x - half, y - half, size, size, stroke, 1f);
        }
    }
}
