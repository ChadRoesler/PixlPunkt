using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Graphics.DirectX;
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
        /// <param name="ds">The canvas drawing session.</param>
        /// <param name="device">The canvas device for bitmap creation.</param>
        /// <param name="dest">The destination rectangle in screen coordinates.</param>
        /// <remarks>
        /// <para>
        /// This method serves as the entry point for interleaved rendering. It determines whether
        /// complex interleaving is needed based on the current animation mode and active sub-routines.
        /// </para>
        /// <para>
        /// Call this instead of simple <c>CompositeTo + DrawDocumentSurface</c> when sub-routines
        /// need to respect layer Z-ordering.
        /// </para>
        /// </remarks>
        private void DrawDocumentWithInterleavedSubRoutines(CanvasDrawingSession ds, CanvasDevice device, Rect dest)
        {
            var animState = Document?.CanvasAnimationState;

            // If not in canvas animation mode, use simple rendering
            if (_animationMode != Animation.AnimationMode.Canvas || animState == null)
            {
                DrawSimpleCompositeInternal(ds, device, dest);
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
                DrawSimpleCompositeInternal(ds, device, dest);
                DrawSubRoutineOverlayInternal(ds, dest); // Still draw selection handles if any
                return;
            }

            // Complex path: interleave sub-routines with layers based on Z-order
            DrawInterleavedCompositeInternal(ds, device, dest, activeSubRoutines);
        }

        /// <summary>
        /// Simple composite rendering: all layers composited together, then drawn.
        /// </summary>
        /// <param name="ds">The canvas drawing session.</param>
        /// <param name="device">The canvas device for bitmap creation.</param>
        /// <param name="dest">The destination rectangle in screen coordinates.</param>
        /// <remarks>
        /// Used when no sub-routines are active or when not in canvas animation mode.
        /// This is the fast path that delegates to <see cref="Core.Document.CanvasDocument.CompositeTo"/>.
        /// </remarks>
        private void DrawSimpleCompositeInternal(CanvasDrawingSession ds, CanvasDevice device, Rect dest)
        {
            EnsureComposite();
            Document.CompositeTo(_composite!);
            FrameReady?.Invoke(_composite!.Pixels, _composite.Width, _composite.Height);
            DrawDocumentSurface(ds, device, dest);
        }

        /// <summary>
        /// Interleaved composite rendering: layers and sub-routines composited in Z-order.
        /// </summary>
        /// <param name="ds">The canvas drawing session.</param>
        /// <param name="device">The canvas device for bitmap creation.</param>
        /// <param name="dest">The destination rectangle in screen coordinates.</param>
        /// <param name="activeSubRoutines">List of active sub-routines with their render info.</param>
        /// <remarks>
        /// <para>
        /// This method implements the core Z-order interleaving algorithm. It builds a unified
        /// list of layers and sub-routines, sorts them by Z-order, and composites them in order.
        /// </para>
        /// <para><strong>Algorithm:</strong></para>
        /// <list type="number">
        /// <item>Collect all visible layers with their computed Z-orders (inverted track index).</item>
        /// <item>Collect all active sub-routines with their explicit Z-order values.</item>
        /// <item>Sort all items by Z-order ascending (lowest first = rendered behind).</item>
        /// <item>Apply tie-breaking: sub-routines before layers; among sub-routines, reverse collection order.</item>
        /// <item>Composite each item onto the output surface in sorted order.</item>
        /// </list>
        /// <para><strong>Consistency with UI:</strong></para>
        /// <para>
        /// This method uses the same Z-order logic as <c>AnimationPanel.BuildOrderedTrackList()</c>
        /// but sorts in ascending order for rendering (vs. descending for UI display). This ensures
        /// the rendered output matches what users see in the timeline panel.
        /// </para>
        /// </remarks>
        private void DrawInterleavedCompositeInternal(CanvasDrawingSession ds, CanvasDevice device, Rect dest,
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
            // Include an index for stable secondary sorting
            // Tuple: (zOrder, isLayer, insertionOrder, layer, subRoutine, name)
            // insertionOrder is used to break ties: higher = later in collection = lower in UI = render first
            var renderItems = new List<(int zOrder, bool isLayer, int insertionOrder, Core.Document.Layer.RasterLayer? layer, Core.Animation.SubRoutineRenderInfo? subRoutine, string name)>();

            int trackCount = animState.Tracks.Count;
            int insertionCounter = 0;

            // Add layers with inverted index as Z-order (same as BuildOrderedTrackList)
            // Track 0 (Foreground/top in UI) gets highest Z, track N-1 (Background) gets Z=0
            for (int i = 0; i < trackCount; i++)
            {
                var track = animState.Tracks[i];
                
                // Find this track's layer in the visible layers
                if (layerIdToLayer.TryGetValue(track.LayerId, out var rasterLayer))
                {
                    int zOrder = trackCount - 1 - i;
                    renderItems.Add((zOrder, true, insertionCounter++, rasterLayer, null, track.LayerName));
                }
            }

            // Add sub-routines with their ZOrder (same as BuildOrderedTrackList)
            // The collection order determines their relative position when Z values are equal
            int subRoutineIndex = 0;
            foreach (var subRoutine in activeSubRoutines)
            {
                // Use subRoutineIndex as the insertion order - this preserves their collection order
                renderItems.Add((subRoutine.ZOrder, false, subRoutineIndex++, null, subRoutine, subRoutine.SubRoutine.DisplayName));
            }

            // Sort for RENDERING (ascending Z = lowest first = behind):
            // 1. Primary: Z-order ascending (lowest Z renders first = behind)
            // 2. Secondary (same Z): sub-routines (false) before layers (true) 
            // 3. Tertiary (same Z, both sub-routines): HIGHER insertionOrder first (they appear lower in UI, should render behind)
            renderItems.Sort((a, b) =>
            {
                int cmp = a.zOrder.CompareTo(b.zOrder);
                if (cmp != 0) return cmp;
                
                // Same Z-order: sub-routines before layers
                int typeCmp = a.isLayer.CompareTo(b.isLayer);
                if (typeCmp != 0) return typeCmp;
                
                // Same Z, same type: for sub-routines, REVERSE the insertion order
                // Higher insertion order = later in collection = lower in UI = should render FIRST (behind)
                // So we compare b to a (descending)
                if (!a.isLayer && !b.isLayer)
                {
                    return b.insertionOrder.CompareTo(a.insertionOrder);
                }
                
                // For layers with same Z (shouldn't happen normally), use insertion order ascending
                return a.insertionOrder.CompareTo(b.insertionOrder);
            });

            // Render in Z-order (lowest first = behind, highest last = in front)
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

            // Draw the interleaved composite
            using var bmp = CanvasBitmap.CreateFromBytes(
                device,
                _composite.Pixels,
                _composite.Width,
                _composite.Height,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96.0f);

            ds.DrawImage(
                bmp,
                dest,
                new Rect(0, 0, _composite.Width, _composite.Height),
                1.0f,
                CanvasImageInterpolation.NearestNeighbor);

            // Draw mask overlay when editing a mask
            DrawMaskOverlay(ds, device, dest);

            // Draw selection handles for selected sub-routine (overlays, not composited)
            if (_selectedSubRoutine != null)
            {
                var selectedInfo = activeSubRoutines.FirstOrDefault(r => r.SubRoutine == _selectedSubRoutine);
                if (selectedInfo != null)
                {
                    DrawSubRoutineSelectionOverlayForInterleaved(ds, dest, scale, selectedInfo);
                }
            }
        }

        /// <summary>
        /// Draws sub-routine selection overlay when no interleaved rendering is needed.
        /// </summary>
        /// <param name="ds">The canvas drawing session.</param>
        /// <param name="dest">The destination rectangle in screen coordinates.</param>
        /// <remarks>
        /// Called after simple compositing to draw selection handles for any selected sub-routine,
        /// even when the sub-routine isn't currently active (e.g., current frame outside its range).
        /// </remarks>
        private void DrawSubRoutineOverlayInternal(CanvasDrawingSession ds, Rect dest)
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
                DrawSubRoutineSelectionOverlayForInterleaved(ds, dest, scale, selectedInfo);
            }
        }

        /// <summary>
        /// Composites a single layer onto the internal composite surface.
        /// </summary>
        /// <param name="layer">The raster layer to composite.</param>
        /// <remarks>
        /// Uses standard alpha compositing (Porter-Duff "over" operator) with the layer's opacity.
        /// The layer must have the same dimensions as the composite surface.
        /// </remarks>
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
        /// <param name="renderInfo">The sub-routine render info containing frame data and transform.</param>
        /// <remarks>
        /// <para>
        /// Applies the sub-routine's interpolated transform (position, scale, rotation) when compositing.
        /// Uses nearest-neighbor sampling to preserve pixel art sharpness.
        /// </para>
        /// <para>
        /// Position values are pixel-snapped (rounded) to avoid sub-pixel rendering artifacts.
        /// </para>
        /// </remarks>
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
        /// <param name="ds">The canvas drawing session.</param>
        /// <param name="dest">The destination rectangle in screen coordinates.</param>
        /// <param name="scale">The current zoom scale.</param>
        /// <param name="renderInfo">The sub-routine render info.</param>
        /// <remarks>
        /// Selection handles are drawn as screen-space overlays after compositing,
        /// so they always appear on top regardless of Z-order.
        /// </remarks>
        private void DrawSubRoutineSelectionOverlayForInterleaved(CanvasDrawingSession ds, Rect dest, float scale,
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

            DrawSubRoutineSelectionInternal(ds, screenX, screenY, screenW, screenH, renderInfo.Rotation);
        }

        /// <summary>
        /// Draws selection handles around a selected sub-routine.
        /// </summary>
        private void DrawSubRoutineSelectionInternal(CanvasDrawingSession ds, float x, float y, float w, float h, float rotation)
        {
            var outlineColor = SubRoutineOutlineSelectedColor;
            var handleColor = SubRoutineHandleColor;
            float outlineWidth = 2f;
            float handleSize = 8f;

            if (Math.Abs(rotation) < 0.01f)
            {
                // No rotation - draw simple rectangle
                ds.DrawRectangle(x, y, w, h, outlineColor, outlineWidth);

                // Draw corner handles
                DrawSubRoutineHandle(ds, x, y, handleSize, handleColor, outlineColor);
                DrawSubRoutineHandle(ds, x + w, y, handleSize, handleColor, outlineColor);
                DrawSubRoutineHandle(ds, x, y + h, handleSize, handleColor, outlineColor);
                DrawSubRoutineHandle(ds, x + w, y + h, handleSize, handleColor, outlineColor);

                // Draw center handle (for moving)
                float centerX = x + w / 2f;
                float centerY = y + h / 2f;
                ds.FillEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, handleColor);
                ds.DrawEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, outlineColor, 1f);
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

                // Draw corner handles at rotated positions
                foreach (var corner in corners)
                {
                    DrawSubRoutineHandle(ds, corner.X, corner.Y, handleSize, handleColor, outlineColor);
                }

                // Draw center handle
                ds.FillEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, handleColor);
                ds.DrawEllipse(centerX, centerY, handleSize / 2f, handleSize / 2f, outlineColor, 1f);
            }
        }

        /// <summary>
        /// Draws a single sub-routine selection handle (square).
        /// </summary>
        private static void DrawSubRoutineHandle(CanvasDrawingSession ds, float x, float y, float size, Color fill, Color stroke)
        {
            float half = size / 2f;
            ds.FillRectangle(x - half, y - half, size, size, fill);
            ds.DrawRectangle(x - half, y - half, size, size, stroke, 1f);
        }
    }
}
