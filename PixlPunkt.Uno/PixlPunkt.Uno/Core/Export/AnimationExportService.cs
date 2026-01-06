using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PixlPunkt.Uno.Core.Animation;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.Export
{
    /// <summary>
    /// Provides animation frame rendering for export operations.
    /// Supports both tile-based and canvas-based animations.
    /// </summary>
    public sealed class AnimationExportService
    {
        //////////////////////////////////////////////////////////////////
        // EXPORT OPTIONS
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Options for animation export.
        /// </summary>
        public class ExportOptions
        {
            /// <summary>Output pixel scale (1 = original size).</summary>
            public int Scale { get; set; } = 1;

            /// <summary>Whether to use stage bounds (if enabled) for canvas animation.</summary>
            public bool UseStage { get; set; } = true;

            /// <summary>Whether to export each layer separately (image sequence only).</summary>
            public bool SeparateLayers { get; set; } = false;

            /// <summary>Background color for non-alpha formats (BGRA).</summary>
            public uint BackgroundColor { get; set; } = 0xFFFFFFFF;

            /// <summary>Frame delay override in milliseconds (0 = use animation settings).</summary>
            public int FrameDelayMs { get; set; } = 0;
        }

        //////////////////////////////////////////////////////////////////
        // FRAME DATA
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Represents a single rendered animation frame.
        /// </summary>
        public class RenderedFrame
        {
            /// <summary>Pixel data in BGRA format.</summary>
            public required byte[] Pixels { get; init; }

            /// <summary>Frame width in pixels.</summary>
            public int Width { get; init; }

            /// <summary>Frame height in pixels.</summary>
            public int Height { get; init; }

            /// <summary>Frame duration in milliseconds.</summary>
            public int DurationMs { get; init; }

            /// <summary>Frame index (0-based).</summary>
            public int Index { get; init; }

            /// <summary>Optional layer name (for separate layer export).</summary>
            public string? LayerName { get; init; }
        }

        //////////////////////////////////////////////////////////////////
        // TILE ANIMATION EXPORT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Renders all frames from a tile animation reel.
        /// </summary>
        /// <param name="document">The source document.</param>
        /// <param name="reel">The animation reel to export.</param>
        /// <param name="options">Export options.</param>
        /// <param name="progress">Optional progress callback (0-1).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of rendered frames.</returns>
        public async Task<List<RenderedFrame>> RenderTileAnimationAsync(
            CanvasDocument document,
            TileAnimationReel reel,
            ExportOptions options,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var frames = new List<RenderedFrame>();
            int frameCount = reel.FrameCount;

            if (frameCount == 0)
            {
                LoggingService.Warning("Tile animation reel '{ReelName}' has no frames", reel.Name);
                return frames;
            }

            int tileW = document.TileSize.Width;
            int tileH = document.TileSize.Height;
            int outputW = tileW * options.Scale;
            int outputH = tileH * options.Scale;

            // Composite the document once
            var composite = new PixelSurface(document.PixelWidth, document.PixelHeight);
            document.CompositeTo(composite);

            for (int i = 0; i < frameCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var reelFrame = reel.Frames[i];
                int tileX = reelFrame.TileX;
                int tileY = reelFrame.TileY;

                // Extract tile pixels from composite
                byte[] tilePixels = ExtractTilePixels(composite, tileX, tileY, tileW, tileH);

                // Scale if needed
                byte[] outputPixels = options.Scale == 1
                    ? tilePixels
                    : ScalePixels(tilePixels, tileW, tileH, outputW, outputH);

                // Calculate frame duration
                int durationMs = options.FrameDelayMs > 0
                    ? options.FrameDelayMs
                    : reel.GetFrameDuration(i);

                frames.Add(new RenderedFrame
                {
                    Pixels = outputPixels,
                    Width = outputW,
                    Height = outputH,
                    DurationMs = durationMs,
                    Index = i
                });

                progress?.Report((double)(i + 1) / frameCount);
            }

            LoggingService.Info("Rendered {FrameCount} frames from tile animation '{ReelName}'",
                frameCount, reel.Name);

            return frames;
        }

        //////////////////////////////////////////////////////////////////
        // CANVAS ANIMATION EXPORT
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Renders all frames from a canvas animation.
        /// </summary>
        /// <param name="document">The source document.</param>
        /// <param name="options">Export options.</param>
        /// <param name="progress">Optional progress callback (0-1).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of rendered frames.</returns>
        public async Task<List<RenderedFrame>> RenderCanvasAnimationAsync(
            CanvasDocument document,
            ExportOptions options,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var frames = new List<RenderedFrame>();
            var animState = document.CanvasAnimationState;
            int frameCount = animState.FrameCount;

            if (frameCount == 0)
            {
                LoggingService.Warning("Canvas animation has no frames");
                return frames;
            }

            // Determine output dimensions
            int outputW, outputH;
            bool useStage = options.UseStage && animState.Stage.Enabled;

            if (useStage)
            {
                outputW = animState.Stage.OutputWidth * options.Scale;
                outputH = animState.Stage.OutputHeight * options.Scale;
            }
            else
            {
                outputW = document.PixelWidth * options.Scale;
                outputH = document.PixelHeight * options.Scale;
            }

            // Calculate frame duration from FPS
            int fps = animState.FramesPerSecond;
            int baseDurationMs = options.FrameDelayMs > 0
                ? options.FrameDelayMs
                : (int)(1000.0 / fps);

            // Store original layer states to restore later
            var originalStates = SaveLayerStates(document);

            try
            {
                for (int i = 0; i < frameCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Apply animation state for this frame
                    animState.ApplyFrameToDocument(document, i);

                    // Render the frame
                    byte[] framePixels = RenderCanvasFrame(document, animState, useStage, options);

                    frames.Add(new RenderedFrame
                    {
                        Pixels = framePixels,
                        Width = outputW,
                        Height = outputH,
                        DurationMs = baseDurationMs,
                        Index = i
                    });

                    progress?.Report((double)(i + 1) / frameCount);
                }
            }
            finally
            {
                // Restore original layer states
                RestoreLayerStates(document, originalStates);
            }

            LoggingService.Info("Rendered {FrameCount} frames from canvas animation ({OutputW}x{OutputH})",
                frameCount, outputW, outputH);

            return frames;
        }

        /// <summary>
        /// Renders all frames with layers separated (for image sequence export).
        /// </summary>
        public async Task<Dictionary<string, List<RenderedFrame>>> RenderCanvasAnimationByLayerAsync(
            CanvasDocument document,
            ExportOptions options,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, List<RenderedFrame>>();
            var animState = document.CanvasAnimationState;
            int frameCount = animState.FrameCount;

            if (frameCount == 0) return result;

            var layers = document.Layers;
            int totalWork = frameCount * layers.Count;
            int workDone = 0;

            // Determine output dimensions
            int outputW, outputH;
            bool useStage = options.UseStage && animState.Stage.Enabled;

            if (useStage)
            {
                outputW = animState.Stage.OutputWidth * options.Scale;
                outputH = animState.Stage.OutputHeight * options.Scale;
            }
            else
            {
                outputW = document.PixelWidth * options.Scale;
                outputH = document.PixelHeight * options.Scale;
            }

            int fps = animState.FramesPerSecond;
            int baseDurationMs = options.FrameDelayMs > 0
                ? options.FrameDelayMs
                : (int)(1000.0 / fps);

            // Store original layer states
            var originalStates = SaveLayerStates(document);

            try
            {
                foreach (var layer in layers)
                {
                    var layerFrames = new List<RenderedFrame>();
                    string layerName = SanitizeLayerName(layer.Name ?? $"Layer{layer.Id}");

                    for (int i = 0; i < frameCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Apply animation state
                        animState.ApplyFrameToDocument(document, i);

                        // Render only this layer
                        byte[] framePixels = RenderSingleLayerFrame(document, layer, animState, useStage, options);

                        layerFrames.Add(new RenderedFrame
                        {
                            Pixels = framePixels,
                            Width = outputW,
                            Height = outputH,
                            DurationMs = baseDurationMs,
                            Index = i,
                            LayerName = layerName
                        });

                        workDone++;
                        progress?.Report((double)workDone / totalWork);
                    }

                    result[layerName] = layerFrames;
                }
            }
            finally
            {
                RestoreLayerStates(document, originalStates);
            }

            return result;
        }

        //////////////////////////////////////////////////////////////////
        // PRIVATE HELPERS
        //////////////////////////////////////////////////////////////////

        private byte[] ExtractTilePixels(PixelSurface composite, int tileX, int tileY, int tileW, int tileH)
        {
            int docX = tileX * tileW;
            int docY = tileY * tileH;
            byte[] pixels = new byte[tileW * tileH * 4];

            for (int y = 0; y < tileH; y++)
            {
                for (int x = 0; x < tileW; x++)
                {
                    int srcIdx = ((docY + y) * composite.Width + (docX + x)) * 4;
                    int dstIdx = (y * tileW + x) * 4;

                    if (srcIdx + 3 < composite.Pixels.Length)
                    {
                        pixels[dstIdx + 0] = composite.Pixels[srcIdx + 0];
                        pixels[dstIdx + 1] = composite.Pixels[srcIdx + 1];
                        pixels[dstIdx + 2] = composite.Pixels[srcIdx + 2];
                        pixels[dstIdx + 3] = composite.Pixels[srcIdx + 3];
                    }
                }
            }

            return pixels;
        }

        private byte[] RenderCanvasFrame(
            CanvasDocument document,
            CanvasAnimationState animState,
            bool useStage,
            ExportOptions options)
        {
            // Composite all visible layers
            var composite = new PixelSurface(document.PixelWidth, document.PixelHeight);
            document.CompositeTo(composite);

            if (useStage)
            {
                // Extract stage region
                var stage = animState.Stage;
                int stageW = stage.StageWidth;
                int stageH = stage.StageHeight;
                int stageX = stage.StageX;
                int stageY = stage.StageY;

                byte[] stagePixels = new byte[stageW * stageH * 4];

                for (int y = 0; y < stageH; y++)
                {
                    for (int x = 0; x < stageW; x++)
                    {
                        int srcX = stageX + x;
                        int srcY = stageY + y;

                        if (srcX >= 0 && srcX < composite.Width && srcY >= 0 && srcY < composite.Height)
                        {
                            int srcIdx = (srcY * composite.Width + srcX) * 4;
                            int dstIdx = (y * stageW + x) * 4;

                            stagePixels[dstIdx + 0] = composite.Pixels[srcIdx + 0];
                            stagePixels[dstIdx + 1] = composite.Pixels[srcIdx + 1];
                            stagePixels[dstIdx + 2] = composite.Pixels[srcIdx + 2];
                            stagePixels[dstIdx + 3] = composite.Pixels[srcIdx + 3];
                        }
                    }
                }

                // Scale if needed
                int outputW = stage.OutputWidth * options.Scale;
                int outputH = stage.OutputHeight * options.Scale;

                if (outputW != stageW || outputH != stageH)
                {
                    return ScalePixels(stagePixels, stageW, stageH, outputW, outputH);
                }

                return options.Scale == 1 ? stagePixels : ScalePixels(stagePixels, stageW, stageH, outputW, outputH);
            }
            else
            {
                // Use full canvas
                if (options.Scale == 1)
                {
                    return (byte[])composite.Pixels.Clone();
                }

                int outputW = document.PixelWidth * options.Scale;
                int outputH = document.PixelHeight * options.Scale;
                return ScalePixels(composite.Pixels, composite.Width, composite.Height, outputW, outputH);
            }
        }

        private byte[] RenderSingleLayerFrame(
            CanvasDocument document,
            RasterLayer layer,
            CanvasAnimationState animState,
            bool useStage,
            ExportOptions options)
        {
            // Create a surface with just this layer
            int w = document.PixelWidth;
            int h = document.PixelHeight;
            var layerSurface = new PixelSurface(w, h);

            // Copy layer pixels with opacity applied
            if (layer.Visible)
            {
                var srcPixels = layer.Surface.Pixels;
                var dstPixels = layerSurface.Pixels;
                float opacityF = layer.Opacity / 255f;

                for (int i = 0; i < srcPixels.Length; i += 4)
                {
                    dstPixels[i + 0] = srcPixels[i + 0];
                    dstPixels[i + 1] = srcPixels[i + 1];
                    dstPixels[i + 2] = srcPixels[i + 2];
                    dstPixels[i + 3] = (byte)(srcPixels[i + 3] * opacityF);
                }
            }

            if (useStage)
            {
                var stage = animState.Stage;
                int stageW = stage.StageWidth;
                int stageH = stage.StageHeight;
                int stageX = stage.StageX;
                int stageY = stage.StageY;

                byte[] stagePixels = new byte[stageW * stageH * 4];

                for (int y = 0; y < stageH; y++)
                {
                    for (int x = 0; x < stageW; x++)
                    {
                        int srcX = stageX + x;
                        int srcY = stageY + y;

                        if (srcX >= 0 && srcX < w && srcY >= 0 && srcY < h)
                        {
                            int srcIdx = (srcY * w + srcX) * 4;
                            int dstIdx = (y * stageW + x) * 4;

                            stagePixels[dstIdx + 0] = layerSurface.Pixels[srcIdx + 0];
                            stagePixels[dstIdx + 1] = layerSurface.Pixels[srcIdx + 1];
                            stagePixels[dstIdx + 2] = layerSurface.Pixels[srcIdx + 2];
                            stagePixels[dstIdx + 3] = layerSurface.Pixels[srcIdx + 3];
                        }
                    }
                }

                int outputW = stage.OutputWidth * options.Scale;
                int outputH = stage.OutputHeight * options.Scale;

                return options.Scale == 1 && outputW == stageW && outputH == stageH
                    ? stagePixels
                    : ScalePixels(stagePixels, stageW, stageH, outputW, outputH);
            }
            else
            {
                if (options.Scale == 1)
                {
                    return (byte[])layerSurface.Pixels.Clone();
                }

                int outputW = w * options.Scale;
                int outputH = h * options.Scale;
                return ScalePixels(layerSurface.Pixels, w, h, outputW, outputH);
            }
        }

        private static byte[] ScalePixels(byte[] src, int srcW, int srcH, int dstW, int dstH)
        {
            byte[] dst = new byte[dstW * dstH * 4];

            for (int y = 0; y < dstH; y++)
            {
                int sy = Math.Min(srcH - 1, y * srcH / dstH);
                for (int x = 0; x < dstW; x++)
                {
                    int sx = Math.Min(srcW - 1, x * srcW / dstW);
                    int srcIdx = (sy * srcW + sx) * 4;
                    int dstIdx = (y * dstW + x) * 4;

                    dst[dstIdx + 0] = src[srcIdx + 0];
                    dst[dstIdx + 1] = src[srcIdx + 1];
                    dst[dstIdx + 2] = src[srcIdx + 2];
                    dst[dstIdx + 3] = src[srcIdx + 3];
                }
            }

            return dst;
        }

        private Dictionary<Guid, LayerState> SaveLayerStates(CanvasDocument document)
        {
            var states = new Dictionary<Guid, LayerState>();

            foreach (var layer in document.Layers)
            {
                states[layer.Id] = new LayerState
                {
                    Visible = layer.Visible,
                    Opacity = layer.Opacity,
                    BlendMode = layer.Blend
                };
            }

            return states;
        }

        private void RestoreLayerStates(CanvasDocument document, Dictionary<Guid, LayerState> states)
        {
            foreach (var layer in document.Layers)
            {
                if (states.TryGetValue(layer.Id, out var state))
                {
                    layer.Visible = state.Visible;
                    layer.Opacity = state.Opacity;
                    layer.Blend = state.BlendMode;
                }
            }
        }

        private static string SanitizeLayerName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        private class LayerState
        {
            public bool Visible { get; set; }
            public byte Opacity { get; set; }
            public Enums.BlendMode BlendMode { get; set; }
        }
    }
}
