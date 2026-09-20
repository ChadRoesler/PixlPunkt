using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tile;
using PixlPunkt.Core.Voxel;
using PixlPunkt.Core.Voxel.Editing;
using PixlPunkt.Core.Voxel.Tools;
using PixlPunkt.PluginSdk.Voxel;
using PixlPunkt.UI.Controls;
using PixlPunkt.UI.Helpers;
using PixlPunkt.UI.Voxel.Tools;
using Windows.System;
using Windows.Storage.Pickers;
using Windows.UI.Core;

namespace PixlPunkt.UI.Voxel
{
    public sealed partial class VoxelWorkspaceControl
    {
        // ════════════════════════════════════════════════════════════════════
        // VIEWPORT RENDERING
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Renders the current mesh to the viewport Image control.
        /// </summary>
        /// <remarks>
        /// In normal mode, renders at <see cref="_viewportWidth"/> × <see cref="_viewportHeight"/>.
        /// In pixel preview mode, renders at the volume's native resolution (e.g. 16×16)
        /// then nearest-neighbor upscales to the viewport size, preserving the crisp
        /// pixel-art aesthetic at any rotation.
        /// </remarks>
        private void RenderViewport(
            ViewportRenderOverrides? renderOverrides = null,
            bool presentOnViewport = true,
            Action<byte[], int, int>? renderedBufferSink = null)
        {
            try
            {
                if (ViewportImage == null)
                    return;

                UpdateViewportSizeFromControl();

                bool pixelMode = PixelPreviewCheckBox?.IsChecked == true && _lastVolume != null;
                bool pixelPreviewAntialias = pixelMode && PixelPreviewAntialiasCheckBox?.IsChecked == true;
                float pixelPreviewAaStrength = Math.Clamp((float)(PixelPreviewAaStrengthSlider?.Value ?? 0.35d), 0f, 1f);
                bool drawOutline = renderOverrides?.DrawOutline ?? (OutlineCheckBox?.IsChecked == true);
                bool drawBackdropGrid = renderOverrides?.DrawBackdropGrid ?? (BackdropGridCheckBox?.IsChecked != false);
                bool drawBackdropProjectionTiles = renderOverrides?.DrawBackdropProjectionTiles ?? (BackdropProjectionTilesCheckBox?.IsChecked == true);
                bool drawSurfaceVoxelGridUi = renderOverrides?.DrawSurfaceVoxelGrid ?? (SurfaceVoxelGridCheckBox?.IsChecked == true);
                bool includeSelectionOverlay = renderOverrides?.IncludeSelectionOverlay ?? true;
                uint clearColor = renderOverrides?.ClearColor ?? ClearColor;
                int renderW, renderH;
                int displayW = _viewportWidth;
                int displayH = _viewportHeight;
                int screenPixelSize = 1;
                int usedDisplayW = displayW;
                int usedDisplayH = displayH;

                if (pixelMode)
                {
                    // ── Pixel-perfect layout ──────────────────────────────
                    // Compute integer screen pixel size from configured base size and zoom
                    int pixelBaseSize = GetPixelPreviewBaseSize();
                    screenPixelSize = ComputePixelPreviewScreenPixelSize(pixelBaseSize, _camera.ZoomPercent);

                    // Snap viewport to multiples of screenPixelSize for clean integer scaling
                    int snappedW = Math.Max(screenPixelSize, (_viewportWidth / screenPixelSize) * screenPixelSize);
                    int snappedH = Math.Max(screenPixelSize, (_viewportHeight / screenPixelSize) * screenPixelSize);

                    // Render target = how many "voxel pixels" fit in the snapped area
                    renderW = Math.Max(1, snappedW / screenPixelSize);
                    renderH = Math.Max(1, snappedH / screenPixelSize);

                    // Odd dimensions for stable center alignment
                    if ((renderW & 1) == 0 && renderW > 1) renderW--;
                    if ((renderH & 1) == 0 && renderH > 1) renderH--;

                    // Recompute used area after odd-snap
                    int usedW = renderW * screenPixelSize;
                    int usedH = renderH * screenPixelSize;
                    usedDisplayW = usedW;
                    usedDisplayH = usedH;

                    // Tell the camera about the pixel-perfect frustum
                    _camera.EnablePixelPerfectFrustum(renderW, renderH);
                    _camera.ResizeViewport(renderW, renderH);
                }
                else
                {
                    renderW = _viewportWidth;
                    renderH = _viewportHeight;
                    _camera.DisablePixelPerfectFrustum();
                    _camera.ResizeViewport(renderW, renderH);
                }

                // Snap the displayed Image size to device pixels so XAML doesn't introduce
                // fractional layout scaling on top of the integer pixel upscale.
                int usedDisplayPhysicalW = Math.Max(1, (int)Math.Round(usedDisplayW * _viewportRasterScale));
                int usedDisplayPhysicalH = Math.Max(1, (int)Math.Round(usedDisplayH * _viewportRasterScale));
                ConfigureViewportImagePresentation(pixelMode, pixelPreviewAntialias, usedDisplayPhysicalW, usedDisplayPhysicalH);

                // Ensure render buffers
                int pixelCount = renderW * renderH;
                if (_renderBuffer == null || _renderBuffer.Length != pixelCount * 4)
                {
                    _renderBuffer = new byte[pixelCount * 4];
                }

                bool renderedExactCardinal = false;
                bool drawSurfaceVoxelGrid = !pixelMode && (SurfaceVoxelGridCheckBox?.IsChecked == true);
                if (_lastVolume != null && _lastVolume.OccupiedCount > 0)
                {
                    EnsureRecommendedLightingDefaultsForCurrentVolume();
                    drawSurfaceVoxelGrid = !pixelMode && drawSurfaceVoxelGridUi;

                    int outlineVoxelSize = Math.Max(1, (int)Math.Round(OutlineSizeBox?.Value ?? 1d));
                    int outlineRenderSize = outlineVoxelSize;
                    if (!pixelMode)
                    {
                        var fr = _camera.GetFrustum();
                        float pixelsPerVoxel = renderH / MathF.Max(1e-6f, fr.Height); // 1 voxel = 1 world unit
                        outlineRenderSize = Math.Max(1, (int)MathF.Round(outlineVoxelSize * pixelsPerVoxel));
                        outlineRenderSize = Math.Min(Math.Max(renderW, renderH), outlineRenderSize);
                    }

                    var ws = _document.VoxelWorkspace;
                    var opts = new VoxelRenderer.RenderOptions
                    {
                        // With z-buffer rendering, disabling backface cull avoids
                        // edge-angle face loss in the preview.
                        BackfaceCull = false,
                        // Phase 4: default to flat/unlit unless the lighting utility enables preview lighting.
                        LightingEnabled = ws?.LightingEnabled == true,
                        LightPosition = ws != null
                            ? new Vector3(ws.LightPosX, ws.LightPosY, ws.LightPosZ)
                            : new Vector3(32f, 48f, 32f),
                        LightColor = ws?.LightColorBgra ?? 0xFFFFFFFF,
                        ShadowColor = ws?.ShadowColorBgra ?? 0xC0000000,
                        ShadowStrength = ws?.LightShadowStrength ?? 1f,
                        LightIntensity = ws?.LightIntensity ?? 1f,
                        // Standard ambient fill for pixel workflows so lit previews are readable
                        // without forcing users to position a perfect key light.
                        AmbientIntensity = 0.22f,
                        LightFalloff = ws?.LightFalloff ?? 0.05f,
                        LightCastShadows = ws?.LightCastShadows ?? false,
                        // In pixel preview we draw the backing grid as a separate 2D pass
                        // so it does not change the voxel rasterization path.
                        DrawBackdropGrid = !pixelMode && drawBackdropGrid,
                        DrawBackdropProjectionTiles = !pixelMode && drawBackdropProjectionTiles,
                        BackdropCageScale = Math.Clamp((float)(BackdropCageScaleBox?.Value ?? ws?.BackdropCageScale ?? 1.6d), 1.05f, 4f),
                        BackdropFrontProjection = _backdropFrontProjectionImage,
                        BackdropBackProjection = _backdropBackProjectionImage,
                        BackdropLeftProjection = _backdropLeftProjectionImage,
                        BackdropRightProjection = _backdropRightProjectionImage,
                        BackdropTopProjection = _backdropTopProjectionImage,
                        BackdropBottomProjection = _backdropBottomProjectionImage,
                        DrawSurfaceVoxelGrid = drawSurfaceVoxelGrid,
                        DrawOutline = drawOutline,
                        OutlineColor = OutlineColorSwatch.Color,
                        OutlineSize = outlineRenderSize,
                    };

                    bool renderedFromPixelSpriteCache = false;
                    if (pixelMode)
                    {
                        renderedExactCardinal = TryRenderExactCardinalPixelPreview(
                            _lastVolume, renderW, renderH, _renderBuffer, clearColor, opts);

                        if (!renderedExactCardinal)
                        {
                            renderedFromPixelSpriteCache = TryRenderCachedPixelPreviewSprite(
                                _lastVolume, renderW, renderH, _renderBuffer, clearColor, opts);
                        }
                    }

                    if (!renderedExactCardinal && !renderedFromPixelSpriteCache)
                    {
                        VoxelRenderer.Render(
                            _lastVolume, _camera,
                            renderW, renderH,
                            _renderBuffer,
                            clearColor, opts);
                    }
                }
                else
                {
                    FillClear(_renderBuffer, pixelCount, clearColor);
                }

                byte[] renderSource = _renderBuffer;
                if (pixelMode && pixelPreviewAntialias)
                {
                    EnsurePixelPreviewAaBuffer(pixelCount * 4);
                    ApplyPixelPreviewEdgeAa(_renderBuffer, renderW, renderH, _pixelPreviewAaBuffer!, pixelPreviewAaStrength);
                    renderSource = _pixelPreviewAaBuffer!;
                }

                // Build the display buffer
                byte[] displayBuffer;

                if (pixelMode && screenPixelSize > 1)
                {
                    // Integer-scale upscale: each render pixel becomes exactly
                    // screenPixelSize × screenPixelSize screen pixels — no distortion
                    displayW = renderW * screenPixelSize;
                    displayH = renderH * screenPixelSize;
                    EnsureDisplayBuffer(displayW * displayH * 4);
                    displayBuffer = _displayBuffer!;
                    FillClear(displayBuffer, displayW * displayH, clearColor);

                    if (drawBackdropGrid && _lastVolume != null)
                    {
                        float cageScale = Math.Clamp((float)(BackdropCageScaleBox?.Value ?? _document.VoxelWorkspace.BackdropCageScale), 1.05f, 4f);
                        DrawPixelPreviewBackdropCage3D(
                            displayBuffer, displayW, displayH,
                            renderW, renderH, screenPixelSize,
                            _lastVolume.Size,
                            minorColor: 0xFF2A2F35,
                            majorColor: 0xFF39424B,
                            majorEvery: 4,
                            cageScale: cageScale);
                    }

                    VoxelImageExporter.UpscaleNearestBgra(renderSource, renderW, renderH, displayBuffer, displayW, displayH, screenPixelSize);

                    if (renderedExactCardinal &&
                        screenPixelSize > 1 &&
                        drawSurfaceVoxelGrid)
                    {
                        DrawExactCardinalPixelPreviewSurfaceGrid(
                            _renderBuffer, renderW, renderH,
                            displayBuffer, displayW, displayH,
                            screenPixelSize,
                            0xB0000000);
                    }
                }
                else
                {
                    displayW = renderW;
                    displayH = renderH;
                    displayBuffer = renderSource;
                }

                if (includeSelectionOverlay)
                {
                    OverlaySelectionHighlight(
                        displayBuffer,
                        displayW,
                        displayH,
                        renderW,
                        renderH,
                        pixelMode,
                        screenPixelSize);
                }

                renderedBufferSink?.Invoke(displayBuffer, displayW, displayH);

                if (!presentOnViewport)
                    return;

                // Push to WriteableBitmap
                if (_viewportBitmap == null ||
                    _viewportBitmap.PixelWidth != displayW ||
                    _viewportBitmap.PixelHeight != displayH)
                {
                    _viewportBitmap = new WriteableBitmap(displayW, displayH);
                }

                using var stream = _viewportBitmap.PixelBuffer.AsStream();
                stream.Seek(0, SeekOrigin.Begin);
                stream.Write(displayBuffer, 0, displayW * displayH * 4);

                ViewportImage.Source = _viewportBitmap;
                UpdateCameraStatsText(pixelMode, screenPixelSize, renderW, renderH);
                UpdateAxisGizmo();
                UpdateLightingQuickActionsState();
                UpdateLightHandleOverlay();
                PersistVoxelPreviewStateToDocument();
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Viewport render failed: {Error}", ex.Message);
            }
        }

        private void EnsurePixelPreviewAaBuffer(int byteLength)
        {
            if (_pixelPreviewAaBuffer == null || _pixelPreviewAaBuffer.Length != byteLength)
            {
                _pixelPreviewAaBuffer = new byte[byteLength];
            }
        }

        private void EnsureDisplayBuffer(int byteLength)
        {
            if (_displayBuffer == null || _displayBuffer.Length != byteLength)
            {
                _displayBuffer = new byte[byteLength];
            }
        }

        private static void ApplyPixelPreviewEdgeAa(
            byte[] source,
            int width,
            int height,
            byte[] destination,
            float strengthMultiplier)
        {
            if (source == null || destination == null)
                return;
            if (width <= 0 || height <= 0)
                return;
            strengthMultiplier = Math.Clamp(strengthMultiplier, 0f, 1f);
            if (strengthMultiplier <= 0f)
            {
                Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
                return;
            }
            int byteLength = width * height * 4;
            if (source.Length < byteLength || destination.Length < byteLength)
                return;

            static int Luma(byte b, byte g, byte r) => (r * 77 + g * 150 + b * 29) >> 8;
            static byte Blend(byte from, int to, float t)
                => (byte)Math.Clamp((int)MathF.Round(from + ((to - from) * t)), 0, 255);

            const int edgeThreshold = 22;
            const int orientationSlack = 6;

            for (int y = 0; y < height; y++)
            {
                int yUp = y > 0 ? y - 1 : y;
                int yDn = y < height - 1 ? y + 1 : y;

                for (int x = 0; x < width; x++)
                {
                    int xLt = x > 0 ? x - 1 : x;
                    int xRt = x < width - 1 ? x + 1 : x;

                    int iC = (y * width + x) * 4;
                    int iL = (y * width + xLt) * 4;
                    int iR = (y * width + xRt) * 4;
                    int iU = (yUp * width + x) * 4;
                    int iD = (yDn * width + x) * 4;

                    byte cb = source[iC];
                    byte cg = source[iC + 1];
                    byte cr = source[iC + 2];
                    byte ca = source[iC + 3];

                    int lumC = Luma(cb, cg, cr);
                    int lumL = Luma(source[iL], source[iL + 1], source[iL + 2]);
                    int lumR = Luma(source[iR], source[iR + 1], source[iR + 2]);
                    int lumU = Luma(source[iU], source[iU + 1], source[iU + 2]);
                    int lumD = Luma(source[iD], source[iD + 1], source[iD + 2]);

                    int dL = Math.Abs(lumC - lumL);
                    int dR = Math.Abs(lumC - lumR);
                    int dU = Math.Abs(lumC - lumU);
                    int dD = Math.Abs(lumC - lumD);
                    int maxDiff = Math.Max(Math.Max(dL, dR), Math.Max(dU, dD));

                    if (maxDiff < edgeThreshold)
                    {
                        destination[iC] = cb;
                        destination[iC + 1] = cg;
                        destination[iC + 2] = cr;
                        destination[iC + 3] = ca;
                        continue;
                    }

                    int gradX = dL + dR;
                    int gradY = dU + dD;
                    int avgB, avgG, avgR;
                    if (gradX > gradY + orientationSlack)
                    {
                        avgB = (source[iL] + source[iR]) >> 1;
                        avgG = (source[iL + 1] + source[iR + 1]) >> 1;
                        avgR = (source[iL + 2] + source[iR + 2]) >> 1;
                    }
                    else if (gradY > gradX + orientationSlack)
                    {
                        avgB = (source[iU] + source[iD]) >> 1;
                        avgG = (source[iU + 1] + source[iD + 1]) >> 1;
                        avgR = (source[iU + 2] + source[iD + 2]) >> 1;
                    }
                    else
                    {
                        avgB = (source[iL] + source[iR] + source[iU] + source[iD]) >> 2;
                        avgG = (source[iL + 1] + source[iR + 1] + source[iU + 1] + source[iD + 1]) >> 2;
                        avgR = (source[iL + 2] + source[iR + 2] + source[iU + 2] + source[iD + 2]) >> 2;
                    }

                    float strength = Math.Clamp((maxDiff - edgeThreshold) / 140f, 0f, 1f) * 0.45f * strengthMultiplier;
                    destination[iC] = Blend(cb, avgB, strength);
                    destination[iC + 1] = Blend(cg, avgG, strength);
                    destination[iC + 2] = Blend(cr, avgR, strength);
                    destination[iC + 3] = ca;
                }
            }
        }

        private bool UpdateViewportSizeFromControl()
        {
            FrameworkElement? sizingElement = (FrameworkElement?)ViewportHost ?? ViewportImage;
            if (sizingElement == null)
                return false;

            int w = Math.Max(1, (int)Math.Round(sizingElement.ActualWidth));
            int h = Math.Max(1, (int)Math.Round(sizingElement.ActualHeight));
            float rasterScale = GetViewportRasterScale();

            bool sameSize = (w == _viewportWidth && h == _viewportHeight);
            bool sameScale = MathF.Abs(rasterScale - _viewportRasterScale) < 0.001f;
            if (sameSize && sameScale)
                return false;

            _viewportWidth = w;
            _viewportHeight = h;
            _viewportRasterScale = rasterScale;
            _renderBuffer = null;
            return true;
        }

        private float GetViewportRasterScale()
        {
            try
            {
                double scale = ViewportHost?.XamlRoot?.RasterizationScale
                    ?? ViewportImage?.XamlRoot?.RasterizationScale
                    ?? 1.0;
                return MathF.Max(1f, (float)scale);
            }
            catch
            {
                return 1f;
            }
        }

        private void ConfigureViewportImagePresentation(
            bool pixelMode,
            bool pixelPreviewAntialias,
            int usedPhysicalPixelWidth,
            int usedPhysicalPixelHeight)
        {
            if (ViewportImage == null) return;

            // In pixel-preview mode, present the already-upscaled bitmap at an explicit
            // centered size so XAML is not doing an extra Uniform fit pass for us.
            if (pixelMode)
            {
                _ = pixelPreviewAntialias;
                ViewportImage.Stretch = Stretch.None;
                ViewportImage.HorizontalAlignment = HorizontalAlignment.Center;
                ViewportImage.VerticalAlignment = VerticalAlignment.Center;

                double scale = Math.Max(1e-6, _viewportRasterScale);
                ViewportImage.Width = Math.Max(1, usedPhysicalPixelWidth) / scale;
                ViewportImage.Height = Math.Max(1, usedPhysicalPixelHeight) / scale;
                // Keep viewport presentation nearest-neighbor even when AA is enabled.
                // Pixel-preview AA is applied in render-space per pixel, not as a post-scale blur.
                TrySetViewportImageInterpolationMode(nearest: true);
            }
            else
            {
                ViewportImage.Stretch = Stretch.Uniform;
                ViewportImage.HorizontalAlignment = HorizontalAlignment.Stretch;
                ViewportImage.VerticalAlignment = VerticalAlignment.Stretch;
                ViewportImage.Width = double.NaN;
                ViewportImage.Height = double.NaN;
                TrySetViewportImageInterpolationMode(nearest: false);
            }
        }

        private void TrySetViewportImageInterpolationMode(bool nearest)
        {
            if (ViewportImage == null) return;

            try
            {
                // WinUI/Uno support varies by platform. Use reflection so desktop builds
                // can take advantage of nearest-neighbor presentation when available
                // without hard-failing on targets that omit the API.
                var imageType = ViewportImage.GetType();
                var prop = imageType.GetProperty("BitmapInterpolationMode");
                if (prop != null && prop.PropertyType.IsEnum)
                {
                    string enumName = nearest ? "NearestNeighbor" : "Linear";
                    object value = Enum.Parse(prop.PropertyType, enumName);
                    prop.SetValue(ViewportImage, value);
                    return;
                }

                Type? renderOptionsType =
                    Type.GetType("Microsoft.UI.Xaml.Media.RenderOptions, Microsoft.WinUI")
                    ?? Type.GetType("Windows.UI.Xaml.Media.RenderOptions, Windows");
                if (renderOptionsType == null) return;

                var setMethod = renderOptionsType.GetMethod(
                    "SetBitmapInterpolationMode",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (setMethod == null) return;

                var paramTypes = setMethod.GetParameters();
                if (paramTypes.Length != 2 || !paramTypes[1].ParameterType.IsEnum) return;

                string attachedEnumName = nearest ? "NearestNeighbor" : "Linear";
                object enumValue = Enum.Parse(paramTypes[1].ParameterType, attachedEnumName);
                setMethod.Invoke(null, new object?[] { ViewportImage, enumValue });
            }
            catch
            {
                // Some platforms/backends may not expose this property; pixel mode still
                // benefits from explicit sizing and integer upscaling.
            }
        }

        private void PixelPreview_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();

            PreserveVisualScaleOnPixelPreviewToggle();

            // Force buffer reallocation on mode change
            _renderBuffer = null;
            RenderViewport();
        }

        private void PixelPreviewAntialias_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            UpdatePixelPreviewAaStrengthLabel();
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void PixelPreviewAaStrength_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            UpdatePixelPreviewAaStrengthLabel();
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void UpdatePixelPreviewAaStrengthLabel()
        {
            if (PixelPreviewAaStrengthText == null)
                return;

            float strength = Math.Clamp((float)(PixelPreviewAaStrengthSlider?.Value ?? 0.35d), 0f, 1f);
            PixelPreviewAaStrengthText.Text = $"{MathF.Round(strength * 100f):0}%";
        }

        private int GetPixelPreviewBaseSize()
        {
            return Math.Max(1, (int)Math.Round(PixelBaseSizeBox?.Value ?? 16d));
        }

        private void PreserveVisualScaleOnPixelPreviewToggle()
        {
            if (_lastVolume == null) return;

            UpdateViewportSizeFromControl();

            bool pixelModeNow = PixelPreviewCheckBox?.IsChecked == true;
            float viewportH = MathF.Max(1f, _viewportHeight);
            float gridSize = MathF.Max(1f, _camera.GridSize);

            if (pixelModeNow)
            {
                // Converting from normal ortho zoom -> pixel-preview integer voxel scale
                float normalPixelsPerVoxel = viewportH * (_camera.ZoomPercent / 100f) / gridSize;
                int targetScreenPixelSize = Math.Max(1, (int)MathF.Round(normalPixelsPerVoxel));
                int basePixelSize = GetPixelPreviewBaseSize();
                float targetZoomPercent = (targetScreenPixelSize * 100f) / basePixelSize;
                _camera.SetZoomPercent(targetZoomPercent);
            }
            else
            {
                // Converting from pixel-preview integer voxel scale -> normal ortho zoom
                int basePixelSize = GetPixelPreviewBaseSize();
                int screenPixelSize = ComputePixelPreviewScreenPixelSize(basePixelSize, _camera.ZoomPercent);
                float targetZoomPercent = (screenPixelSize * gridSize * 100f) / viewportH;
                _camera.SetZoomPercent(targetZoomPercent);
            }
        }

        /// <summary>
        /// Fills the buffer with the clear color.
        /// </summary>
        private static void FillClear(byte[] buffer, int pixelCount, uint clearColor)
        {
            byte b = (byte)(clearColor & 0xFF);
            byte g = (byte)((clearColor >> 8) & 0xFF);
            byte r = (byte)((clearColor >> 16) & 0xFF);
            byte a = (byte)((clearColor >> 24) & 0xFF);

            for (int i = 0; i < pixelCount; i++)
            {
                int bi = i * 4;
                buffer[bi] = b;
                buffer[bi + 1] = g;
                buffer[bi + 2] = r;
                buffer[bi + 3] = a;
            }
        }

        private void OverlaySelectionHighlight(
            byte[]? displayBuffer,
            int displayW,
            int displayH,
            int renderW,
            int renderH,
            bool pixelMode,
            int screenPixelSize)
        {
            if (displayBuffer == null || _lastVolume == null)
                return;
            if (_editEngine.Selection.Count == 0)
                return;
            if (displayBuffer.Length < displayW * displayH * 4)
                return;

            // Visual color intentionally distinct from outline/grid.
            uint lineColor = pixelMode ? 0xFF3CFBFFu : 0xE03CFBFFu;
            int thickness = pixelMode ? Math.Max(1, screenPixelSize / 6) : 1;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();
            float vw = MathF.Max(1f, _camera.ViewportWidth);
            float vh = MathF.Max(1f, _camera.ViewportHeight);
            float frWidth = MathF.Max(1e-6f, fr.Width);
            float frHeight = MathF.Max(1e-6f, fr.Height);
            float frCenterX = (fr.Left + fr.Right) * 0.5f;
            float frCenterY = (fr.Top + fr.Bottom) * 0.5f;

            int size = _lastVolume.Size;
            float half = size * 0.5f;
            Span<Vector3> corners = stackalloc Vector3[8];
            var proj = new (float X, float Y, bool Valid)[8];

            int drawn = 0;
            foreach (var sel in _editEngine.Selection.Enumerate())
            {
                if ((uint)sel.X >= (uint)size || (uint)sel.Y >= (uint)size || (uint)sel.Z >= (uint)size)
                    continue;
                if (!_lastVolume.IsOccupied(sel.X, sel.Y, sel.Z))
                    continue;

                if (++drawn > 256)
                    break;

                var basePos = new Vector3(sel.X - half, sel.Y - half, sel.Z - half);
                corners[0] = basePos + new Vector3(0, 0, 0);
                corners[1] = basePos + new Vector3(1, 0, 0);
                corners[2] = basePos + new Vector3(1, 1, 0);
                corners[3] = basePos + new Vector3(0, 1, 0);
                corners[4] = basePos + new Vector3(0, 0, 1);
                corners[5] = basePos + new Vector3(1, 0, 1);
                corners[6] = basePos + new Vector3(1, 1, 1);
                corners[7] = basePos + new Vector3(0, 1, 1);
                for (int i = 0; i < 8; i++)
                {
                    var rel = corners[i] - pose.Position;
                    float cx = Vector3.Dot(rel, basis.Right);
                    float cy = Vector3.Dot(rel, basis.Up);
                    float cz = Vector3.Dot(rel, basis.Forward);
                    if (cz <= 0f)
                    {
                        proj[i] = (0f, 0f, false);
                        continue;
                    }

                    float xNdc = (2f * cx - 2f * frCenterX) / frWidth;
                    float yNdc = (2f * cy - 2f * frCenterY) / frHeight;
                    float sx = (xNdc * 0.5f + 0.5f) * vw;
                    float sy = (1f - (yNdc * 0.5f + 0.5f)) * vh;

                    if (pixelMode)
                    {
                        sx *= screenPixelSize;
                        sy *= screenPixelSize;
                    }

                    proj[i] = (sx, sy, true);
                }

                DrawEdge(0, 1); DrawEdge(1, 2); DrawEdge(2, 3); DrawEdge(3, 0);
                DrawEdge(4, 5); DrawEdge(5, 6); DrawEdge(6, 7); DrawEdge(7, 4);
                DrawEdge(0, 4); DrawEdge(1, 5); DrawEdge(2, 6); DrawEdge(3, 7);

                void DrawEdge(int a, int b)
                {
                    if (!proj[a].Valid || !proj[b].Valid)
                        return;

                    DrawLineBgra(
                        displayBuffer, displayW, displayH,
                        proj[a].X, proj[a].Y,
                        proj[b].X, proj[b].Y,
                        lineColor, thickness);
                }
            }
        }

        private static void DrawLineBgra(
            byte[] buffer,
            int width,
            int height,
            float x0f,
            float y0f,
            float x1f,
            float y1f,
            uint color,
            int thickness)
        {
            int x0 = (int)MathF.Round(x0f);
            int y0 = (int)MathF.Round(y0f);
            int x1 = (int)MathF.Round(x1f);
            int y1 = (int)MathF.Round(y1f);

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            thickness = Math.Max(1, thickness);
            int radius = thickness / 2;

            while (true)
            {
                for (int oy = -radius; oy <= radius; oy++)
                {
                    for (int ox = -radius; ox <= radius; ox++)
                    {
                        int px = x0 + ox;
                        int py = y0 + oy;
                        if ((uint)px >= (uint)width || (uint)py >= (uint)height)
                            continue;
                        AlphaBlendPackedBgra(buffer, (py * width + px) * 4, color);
                    }
                }

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = err * 2;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }
                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private bool IsKeyDown(VirtualKey key)
        {
            var st = InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return (st & CoreVirtualKeyStates.Down) != 0;
        }

        private bool IsFaceToolActive(string? toolId)
            => toolId == VoxelToolIds.FacePaint ||
               toolId == VoxelToolIds.FaceDropper ||
               toolId == VoxelToolIds.FaceEraseOverride;

        private static bool IsBuiltInVoxelTool(string? toolId)
            => toolId == VoxelToolIds.FacePaint ||
               toolId == VoxelToolIds.FaceDropper ||
               toolId == VoxelToolIds.FaceEraseOverride ||
               toolId == VoxelToolIds.VoxelCreate ||
               toolId == VoxelToolIds.VoxelDelete ||
               toolId == VoxelToolIds.VoxelSelect ||
               toolId == VoxelToolIds.VoxelMove ||
               toolId == VoxelToolIds.Lighting;

        private bool IsVoxelClickToolActive(string? toolId)
        {
            if (string.IsNullOrWhiteSpace(toolId))
                return false;

            var behavior = _voxelToolState.ActiveRegistration?.Behavior;
            if (behavior == null)
            {
                return IsBuiltInVoxelTool(toolId);
            }

            return behavior.InputPattern != VoxelToolInputPattern.Utility;
        }

        private bool TryApplyActiveVoxelToolAtHostPoint(
            Windows.Foundation.Point hostPoint,
            Microsoft.UI.Input.PointerPointProperties? pointerProps,
            ToolPointerPhase phase)
        {
            bool continuousStroke = phase != ToolPointerPhase.Pressed;
            string? toolId = _voxelToolState.ActiveToolId;
            if (string.IsNullOrWhiteSpace(toolId))
                return false;

            EnsureActiveVoxelToolHandler();

            if (!IsBuiltInVoxelTool(toolId))
            {
                return TryDispatchActiveVoxelToolHandler(hostPoint, pointerProps, phase);
            }

            if (IsFaceToolActive(toolId))
            {
                ApplyFacePainterActionAtHostPoint(hostPoint, continuousStroke);
                return true;
            }

            if (_lastVolume == null || _lastVolume.OccupiedCount == 0)
            {
                UpdateVoxelSelectionStatusText("No voxel model loaded. Build a voxel model first.");
                return false;
            }

            if (!TryPickVoxelFaceAtHostPoint(hostPoint, out var picked))
            {
                if (!continuousStroke && (toolId == VoxelToolIds.VoxelSelect || toolId == VoxelToolIds.VoxelMove))
                {
                    if (!IsKeyDown(VirtualKey.Control) && !IsKeyDown(VirtualKey.Shift) && !IsKeyDown(VirtualKey.Menu))
                    {
                        _editEngine.ClearSelection();
                        UpdateVoxelSelectionStatusText("Selection cleared.");
                    }
                }
                return false;
            }

            switch (toolId)
            {
                case VoxelToolIds.VoxelCreate:
                {
                    var delta = FaceToOffset(picked.Face);
                    int tx = picked.X + delta.X;
                    int ty = picked.Y + delta.Y;
                    int tz = picked.Z + delta.Z;
                    uint color = _palette?.Foreground ?? 0xFF000000;
                    if (_editEngine.CreateVoxel(tx, ty, tz, color))
                    {
                        UpdateVoxelSelectionStatusText($"Created voxel @ ({tx},{ty},{tz}).");
                    }
                    else if (!continuousStroke)
                    {
                        UpdateVoxelSelectionStatusText($"Create blocked @ ({tx},{ty},{tz}).");
                    }
                    return true;
                }

                case VoxelToolIds.VoxelDelete:
                {
                    if (_editEngine.DeleteVoxel(picked.X, picked.Y, picked.Z))
                    {
                        UpdateVoxelSelectionStatusText($"Deleted voxel @ ({picked.X},{picked.Y},{picked.Z}).");
                    }
                    else if (!continuousStroke)
                    {
                        UpdateVoxelSelectionStatusText($"Delete blocked @ ({picked.X},{picked.Y},{picked.Z}).");
                    }
                    return true;
                }

                case VoxelToolIds.VoxelSelect:
                case VoxelToolIds.VoxelMove:
                {
                    var mode = GetSelectionModeFromModifiers();
                    if (_editEngine.SetSelection(new[] { new Int3(picked.X, picked.Y, picked.Z) }, mode))
                    {
                        UpdateVoxelSelectionStatusText(
                            $"Selected voxel @ ({picked.X},{picked.Y},{picked.Z}) [{mode}].");
                    }
                    else if (!continuousStroke)
                    {
                        UpdateVoxelSelectionStatusText();
                    }
                    return true;
                }
            }

            return false;
        }

        private bool TryDispatchActiveVoxelToolHandler(
            Windows.Foundation.Point hostPoint,
            Microsoft.UI.Input.PointerPointProperties? pointerProps,
            ToolPointerPhase phase)
        {
            EnsureActiveVoxelToolHandler();
            if (_activeVoxelToolHandler == null)
                return false;

            var evt = BuildVoxelPointerEvent(hostPoint, pointerProps);
            try
            {
                return phase switch
                {
                    ToolPointerPhase.Pressed => _activeVoxelToolHandler.PointerPressed(evt),
                    ToolPointerPhase.Moved => _activeVoxelToolHandler.PointerMoved(evt),
                    ToolPointerPhase.Released => _activeVoxelToolHandler.PointerReleased(evt),
                    _ => false,
                };
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Voxel tool handler pointer event failed id={ToolId}: {Error}",
                    _activeVoxelToolHandlerToolId ?? "(unknown)", ex.Message);
                return false;
            }
        }

        private VoxelPointerEvent BuildVoxelPointerEvent(
            Windows.Foundation.Point hostPoint,
            Microsoft.UI.Input.PointerPointProperties? pointerProps)
        {
            bool shift = IsKeyDown(VirtualKey.Shift);
            bool ctrl = IsKeyDown(VirtualKey.Control);
            bool alt = IsKeyDown(VirtualKey.Menu);

            return new VoxelPointerEvent(
                (float)hostPoint.X,
                (float)hostPoint.Y,
                pointerProps?.IsLeftButtonPressed == true,
                pointerProps?.IsRightButtonPressed == true,
                pointerProps?.IsMiddleButtonPressed == true,
                shift,
                ctrl,
                alt);
        }

        private static Int3 FaceToOffset(Face face)
        {
            return face switch
            {
                Face.Front => new Int3(0, 0, -1),
                Face.Back => new Int3(0, 0, 1),
                Face.Left => new Int3(-1, 0, 0),
                Face.Right => new Int3(1, 0, 0),
                Face.Top => new Int3(0, 1, 0),
                Face.Bottom => new Int3(0, -1, 0),
                _ => new Int3(0, 0, 0),
            };
        }

        private VoxelSelectionMode GetSelectionModeFromModifiers()
        {
            bool shift = IsKeyDown(VirtualKey.Shift);
            bool ctrl = IsKeyDown(VirtualKey.Control);
            bool alt = IsKeyDown(VirtualKey.Menu);

            if (alt) return VoxelSelectionMode.Remove;
            if (ctrl && shift) return VoxelSelectionMode.Toggle;
            if (ctrl) return VoxelSelectionMode.Toggle;
            if (shift) return VoxelSelectionMode.Add;
            return VoxelSelectionMode.Replace;
        }

        private void ApplyFacePainterActionAtHostPoint(Windows.Foundation.Point hostPoint, bool continuousStroke = false)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0)
                return;

            if (!TryPickVoxelFaceAtHostPoint(hostPoint, out var picked))
            {
                return;
            }

            if (continuousStroke && _lastStrokePaintFace.HasValue && _lastStrokePaintFace.Value.Equals(picked))
                return;

            _lastStrokePaintFace = picked;

            FacePainterMode mode = _voxelToolState.ActiveToolId switch
            {
                VoxelToolIds.FaceDropper => FacePainterMode.Sample,
                VoxelToolIds.FaceEraseOverride => FacePainterMode.EraseOverride,
                _ => FacePainterMode.Paint,
            };

            switch (mode)
            {
                case FacePainterMode.Paint:
                {
                    uint color = _palette?.Foreground ?? 0xFF000000;
                    SetFaceColorFromTool(picked.X, picked.Y, picked.Z, ToVoxelFace(picked.Face), color);
                    break;
                }

                case FacePainterMode.Sample:
                {
                    var sampled = _lastVolume.GetFaceColor(picked.X, picked.Y, picked.Z, picked.Face);
                    uint color = PackedBgraFromRgba(sampled);
                    _palette?.SetForeground(color);
                    break;
                }

                case FacePainterMode.EraseOverride:
                {
                    ClearFaceColorOverrideFromTool(picked.X, picked.Y, picked.Z, ToVoxelFace(picked.Face));
                    break;
                }
            }
        }

        private bool TryPickVoxelFaceAtHostPoint(Windows.Foundation.Point hostPoint, out PickedVoxelFace picked)
        {
            picked = default;
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0 || ViewportHost == null || _viewportBitmap == null)
                return false;

            if (!TryMapHostPointToCameraViewport(hostPoint, out float viewportX, out float viewportY))
                return false;

            return TryRayPickVoxelFace(_lastVolume, viewportX, viewportY, out picked);
        }

        private bool TryMapHostPointToCameraViewport(
            Windows.Foundation.Point hostPoint,
            out float viewportX,
            out float viewportY)
        {
            viewportX = 0f;
            viewportY = 0f;

            if (ViewportHost == null || _viewportBitmap == null)
                return false;

            if (!TryGetPresentedImageRectInHostDip(out var imageRect))
                return false;

            if (hostPoint.X < imageRect.X || hostPoint.Y < imageRect.Y ||
                hostPoint.X >= imageRect.X + imageRect.Width || hostPoint.Y >= imageRect.Y + imageRect.Height)
            {
                return false;
            }

            double srcW = Math.Max(1, _viewportBitmap.PixelWidth);
            double srcH = Math.Max(1, _viewportBitmap.PixelHeight);
            double u = (hostPoint.X - imageRect.X) / Math.Max(1e-6, imageRect.Width);
            double v = (hostPoint.Y - imageRect.Y) / Math.Max(1e-6, imageRect.Height);

            double imgX = Math.Clamp(u, 0.0, 0.999999) * srcW;
            double imgY = Math.Clamp(v, 0.0, 0.999999) * srcH;

            float camViewportW = MathF.Max(1f, _camera.ViewportWidth);
            float camViewportH = MathF.Max(1f, _camera.ViewportHeight);
            float sx = (float)(srcW / camViewportW);
            float sy = (float)(srcH / camViewportH);

            viewportX = (float)imgX / MathF.Max(1e-6f, sx);
            viewportY = (float)imgY / MathF.Max(1e-6f, sy);
            return true;
        }

        private bool TryGetPresentedImageRectInHostDip(out Windows.Foundation.Rect rect)
        {
            rect = default;
            if (ViewportHost == null || _viewportBitmap == null)
                return false;

            double hostW = Math.Max(1.0, ViewportHost.ActualWidth);
            double hostH = Math.Max(1.0, ViewportHost.ActualHeight);
            double srcW = Math.Max(1.0, _viewportBitmap.PixelWidth);
            double srcH = Math.Max(1.0, _viewportBitmap.PixelHeight);

            bool pixelMode = PixelPreviewCheckBox?.IsChecked == true && _lastVolume != null;
            double drawW;
            double drawH;

            if (pixelMode && ViewportImage != null &&
                !double.IsNaN(ViewportImage.Width) && !double.IsNaN(ViewportImage.Height))
            {
                drawW = Math.Max(1.0, ViewportImage.Width);
                drawH = Math.Max(1.0, ViewportImage.Height);
            }
            else
            {
                double scale = Math.Min(hostW / srcW, hostH / srcH);
                if (!(scale > 0)) return false;
                drawW = srcW * scale;
                drawH = srcH * scale;
            }

            double x = (hostW - drawW) * 0.5;
            double y = (hostH - drawH) * 0.5;
            rect = new Windows.Foundation.Rect(x, y, drawW, drawH);
            return true;
        }

        private void RefreshOccupiedBoundsCache()
        {
            if (!TryComputeOccupiedBounds(_lastVolume, out var min, out var max))
            {
                _hasOccupiedBounds = false;
                _occupiedBoundsMin = default;
                _occupiedBoundsMax = default;
                return;
            }

            _hasOccupiedBounds = true;
            _occupiedBoundsMin = min;
            _occupiedBoundsMax = max;
        }

        private static bool TryComputeOccupiedBounds(VoxelVolume? volume, out Vector3 min, out Vector3 max)
        {
            min = default;
            max = default;
            if (volume == null || volume.OccupiedCount <= 0)
                return false;

            int size = volume.Size;
            float half = size * 0.5f;
            bool found = false;

            for (int z = 0; z < size; z++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!volume.IsOccupied(x, y, z))
                            continue;

                        var center = new Vector3(
                            (x - half) + 0.5f,
                            (y - half) + 0.5f,
                            (z - half) + 0.5f);

                        if (!found)
                        {
                            min = center;
                            max = center;
                            found = true;
                            continue;
                        }

                        min = Vector3.Min(min, center);
                        max = Vector3.Max(max, center);
                    }
                }
            }

            return found;
        }

        private bool TryGetRecommendedLightSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = default;
            if (!_hasOccupiedBounds)
                return false;

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float spanX = MathF.Abs(extents.X);
            float spanY = MathF.Abs(extents.Y);
            float spanZ = MathF.Abs(extents.Z);

            float lift = MathF.Max(5f, (spanY * 0.5f) + 2f);
            float sideOffset = MathF.Max(2f, MathF.Max(spanX, spanZ) * 0.25f);

            spawnPosition = new Vector3(
                center.X + sideOffset,
                _occupiedBoundsMax.Y + lift,
                center.Z + sideOffset);

            spawnPosition = ClampLightPositionToModelNeighborhood(spawnPosition);
            return true;
        }

        private bool TryGetCameraFacingLightSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = default;
            if (!_hasOccupiedBounds)
                return false;

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float span = MathF.Max(1f, MathF.Max(MathF.Abs(extents.X), MathF.Max(MathF.Abs(extents.Y), MathF.Abs(extents.Z))));

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            Vector3 toCamera = Vector3.Normalize(pose.Position - center);

            float frontOffset = MathF.Max(5f, span * 0.9f);
            float lift = MathF.Max(4f, span * 0.45f);
            float side = MathF.Max(1.5f, span * 0.15f);

            spawnPosition = center + (toCamera * frontOffset) + (basis.Up * lift) + (basis.Right * side);
            spawnPosition = ClampLightPositionToModelNeighborhood(spawnPosition);
            return true;
        }

        private bool IsLightPositionUsableForCurrentVolume(Vector3 position)
        {
            if (!_hasOccupiedBounds)
                return true;

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float spanX = MathF.Abs(extents.X);
            float spanY = MathF.Abs(extents.Y);
            float spanZ = MathF.Abs(extents.Z);
            float span = MathF.Max(spanX, MathF.Max(spanY, spanZ));
            float range = MathF.Max(12f, (span * 2.5f) + 5f);

            if (MathF.Abs(position.X - center.X) > range ||
                MathF.Abs(position.Y - center.Y) > range ||
                MathF.Abs(position.Z - center.Z) > range)
            {
                return false;
            }

            if (!TryProjectWorldToHostDip(position, out var hostDip, out var depth) || depth <= 0f)
                return false;

            if (TryGetPresentedImageRectInHostDip(out var imageRect))
            {
                const float pad = 4f;
                if (hostDip.X < imageRect.X + pad || hostDip.X > imageRect.X + imageRect.Width - pad ||
                    hostDip.Y < imageRect.Y + pad || hostDip.Y > imageRect.Y + imageRect.Height - pad)
                {
                    return false;
                }
            }

            return true;
        }

        private Vector3 ClampLightPositionToModelNeighborhood(Vector3 worldPosition)
        {
            if (!_hasOccupiedBounds)
            {
                return new Vector3(
                    Math.Clamp(worldPosition.X, -1024f, 1024f),
                    Math.Clamp(worldPosition.Y, -1024f, 1024f),
                    Math.Clamp(worldPosition.Z, -1024f, 1024f));
            }

            var center = (_occupiedBoundsMin + _occupiedBoundsMax) * 0.5f;
            var extents = _occupiedBoundsMax - _occupiedBoundsMin;
            float spanX = MathF.Abs(extents.X);
            float spanY = MathF.Abs(extents.Y);
            float spanZ = MathF.Abs(extents.Z);
            float span = MathF.Max(spanX, MathF.Max(spanY, spanZ));
            float range = MathF.Max(12f, (span * 2.5f) + 5f);

            var min = center - new Vector3(range, range, range);
            var max = center + new Vector3(range, range, range);
            return Vector3.Clamp(worldPosition, min, max);
        }

        private Vector3 GetCurrentLightPosition()
        {
            var ws = _document.VoxelWorkspace;
            return new Vector3(ws.LightPosX, ws.LightPosY, ws.LightPosZ);
        }

        private void SetCurrentLightPosition(Vector3 worldPosition, bool commitUiRefresh)
        {
            var ws = _document.VoxelWorkspace;
            var clamped = ClampLightPositionToModelNeighborhood(worldPosition);
            ws.HasState = true;
            ws.LightPosX = clamped.X;
            ws.LightPosY = clamped.Y;
            ws.LightPosZ = clamped.Z;

            _pixelPreviewSpriteCache = null;
            // During drag we avoid syncing NumberBoxes every pointer tick (commitUiRefresh=false) and
            // perform one UI sync at the end of the drag for smoother interaction.
            if (commitUiRefresh)
                SyncLightingControlsFromDocument();
            RenderViewport();
        }

        private float GetCameraDepthForWorldPoint(Vector3 worldPoint)
        {
            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            return Vector3.Dot(worldPoint - pose.Position, basis.Forward);
        }

        private bool TryMapHostPointToWorldAtCameraDepth(
            Windows.Foundation.Point hostPoint,
            float cameraDepth,
            out Vector3 world)
        {
            world = default;
            if (!TryMapHostPointToCameraViewport(hostPoint, out float viewportX, out float viewportY))
                return false;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();

            float viewportW = MathF.Max(1f, _camera.ViewportWidth);
            float viewportH = MathF.Max(1f, _camera.ViewportHeight);
            float xNdc = ((viewportX / viewportW) * 2f) - 1f;
            float yNdc = 1f - ((viewportY / viewportH) * 2f);
            float cx = ((xNdc * fr.Width) + (fr.Left + fr.Right)) * 0.5f;
            float cy = ((yNdc * fr.Height) + (fr.Top + fr.Bottom)) * 0.5f;

            world = pose.Position + (basis.Right * cx) + (basis.Up * cy) + (basis.Forward * cameraDepth);
            return true;
        }

        private bool TryProjectWorldToHostDip(Vector3 worldPoint, out Vector2 hostPoint, out float cameraDepth)
        {
            hostPoint = default;
            cameraDepth = 0f;

            if (!TryGetPresentedImageRectInHostDip(out var imageRect))
                return false;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();

            float viewportW = MathF.Max(1f, _camera.ViewportWidth);
            float viewportH = MathF.Max(1f, _camera.ViewportHeight);
            float frWidth = MathF.Max(1e-6f, fr.Width);
            float frHeight = MathF.Max(1e-6f, fr.Height);
            float frCenterX = (fr.Left + fr.Right) * 0.5f;
            float frCenterY = (fr.Top + fr.Bottom) * 0.5f;

            var rel = worldPoint - pose.Position;
            float cx = Vector3.Dot(rel, basis.Right);
            float cy = Vector3.Dot(rel, basis.Up);
            cameraDepth = Vector3.Dot(rel, basis.Forward);

            float xNdc = (2f * cx - 2f * frCenterX) / frWidth;
            float yNdc = (2f * cy - 2f * frCenterY) / frHeight;
            float camSx = (xNdc * 0.5f + 0.5f) * viewportW;
            float camSy = (1f - (yNdc * 0.5f + 0.5f)) * viewportH;

            double hostX = imageRect.X + (camSx / viewportW) * imageRect.Width;
            double hostY = imageRect.Y + (camSy / viewportH) * imageRect.Height;
            hostPoint = new Vector2((float)hostX, (float)hostY);
            return true;
        }

        private void UpdateLightHandleOverlay()
        {
            if (LightHandleVisual == null)
                return;

            if (_lastVolume == null || _lastVolume.OccupiedCount <= 0 || !_document.VoxelWorkspace.LightingEnabled || !IsViewportFocused())
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            Vector3 lightPos = GetCurrentLightPosition();
            if (!TryProjectWorldToHostDip(lightPos, out var host, out var depth) || depth <= 0f)
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            if (!TryGetPresentedImageRectInHostDip(out var imageRect))
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            float handleHalfW = (float)(Math.Max(1.0, LightHandleVisual.Width) * 0.5);
            float handleHalfH = (float)(Math.Max(1.0, LightHandleVisual.Height) * 0.5);
            float left = (float)imageRect.X;
            float top = (float)imageRect.Y;
            float right = (float)(imageRect.X + imageRect.Width);
            float bottom = (float)(imageRect.Y + imageRect.Height);

            bool fullyInsideImageRect =
                host.X - handleHalfW >= left &&
                host.X + handleHalfW <= right &&
                host.Y - handleHalfH >= top &&
                host.Y + handleHalfH <= bottom;

            if (!fullyInsideImageRect)
            {
                _lightHandleHostDip = new Vector2(float.NaN, float.NaN);
                LightHandleVisual.Visibility = Visibility.Collapsed;
                return;
            }

            _lightHandleHostDip = host;
            LightHandleVisual.Visibility = Visibility.Visible;
            Canvas.SetLeft(LightHandleVisual, host.X - ((float)LightHandleVisual.Width * 0.5f));
            Canvas.SetTop(LightHandleVisual, host.Y - ((float)LightHandleVisual.Height * 0.5f));

            if (LightHandleCore != null || LightHandleRing != null)
            {
                var lightColor = BgraToColor(_document.VoxelWorkspace.LightColorBgra);
                var rayStroke = new SolidColorBrush(lightColor);
                var coreFill = new SolidColorBrush(lightColor);

                if (LightHandleCore != null)
                    LightHandleCore.Fill = coreFill;
                if (LightHandleRing != null)
                    LightHandleRing.Stroke = rayStroke;

                if (LightHandleVisual.Children != null)
                {
                    foreach (var child in LightHandleVisual.Children)
                    {
                        if (child is Line line)
                            line.Stroke = rayStroke;
                    }
                }
            }
        }

        private bool TryBeginLightHandleDrag(Windows.Foundation.Point hostPoint)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount <= 0 || !_document.VoxelWorkspace.LightingEnabled)
                return false;
            if (float.IsNaN(_lightHandleHostDip.X) || float.IsNaN(_lightHandleHostDip.Y))
                return false;

            float dist = Vector2.Distance(_lightHandleHostDip, new Vector2((float)hostPoint.X, (float)hostPoint.Y));
            if (dist > LightHandleHitRadiusDip)
                return false;

            _lightDragCameraDepth = GetCameraDepthForWorldPoint(GetCurrentLightPosition());
            return true;
        }

        private void UpdateLightHandleDrag(Windows.Foundation.Point hostPoint)
        {
            if (!TryMapHostPointToWorldAtCameraDepth(hostPoint, _lightDragCameraDepth, out var world))
                return;

            SetCurrentLightPosition(world, commitUiRefresh: false);
        }

        private void EndLightHandleDrag()
        {
            SyncLightingControlsFromDocument();
            _lightDragCameraDepth = 0f;
        }

        private void NudgeLightPosition(Vector3 delta)
        {
            var current = GetCurrentLightPosition();
            SetCurrentLightPosition(current + delta, commitUiRefresh: true);
        }

        private static Windows.UI.Color BgraToColor(uint bgra)
        {
            byte b = (byte)(bgra & 0xFF);
            byte g = (byte)((bgra >> 8) & 0xFF);
            byte r = (byte)((bgra >> 16) & 0xFF);
            byte a = (byte)((bgra >> 24) & 0xFF);
            return Windows.UI.Color.FromArgb(a, r, g, b);
        }

        private bool TryRayPickVoxelFace(VoxelVolume volume, float screenX, float screenY, out PickedVoxelFace picked)
        {
            picked = default;
            if (volume == null) return false;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();

            float viewportW = MathF.Max(1f, _camera.ViewportWidth);
            float viewportH = MathF.Max(1f, _camera.ViewportHeight);

            float xNdc = ((screenX / viewportW) * 2f) - 1f;
            float yNdc = 1f - ((screenY / viewportH) * 2f);
            float cx = ((xNdc * fr.Width) + (fr.Left + fr.Right)) * 0.5f;
            float cy = ((yNdc * fr.Height) + (fr.Top + fr.Bottom)) * 0.5f;

            var rayOrigin = pose.Position + (basis.Right * cx) + (basis.Up * cy);
            var rayDir = basis.Forward;

            int size = volume.Size;
            float half = size * 0.5f;
            var boxMin = new Vector3(-half, -half, -half);
            var boxMax = new Vector3(half, half, half);

            if (!TryIntersectRayAabb(rayOrigin, rayDir, boxMin, boxMax, out float tEnter, out float tExit, out int hitAxis))
                return false;
            if (tExit < 0f)
                return false;

            tEnter = MathF.Max(0f, tEnter);
            const float eps = 1e-4f;
            var p = rayOrigin + (rayDir * (tEnter + eps));

            int ix = Math.Clamp((int)MathF.Floor(p.X + half), 0, size - 1);
            int iy = Math.Clamp((int)MathF.Floor(p.Y + half), 0, size - 1);
            int iz = Math.Clamp((int)MathF.Floor(p.Z + half), 0, size - 1);

            int stepX = rayDir.X > 0f ? 1 : (rayDir.X < 0f ? -1 : 0);
            int stepY = rayDir.Y > 0f ? 1 : (rayDir.Y < 0f ? -1 : 0);
            int stepZ = rayDir.Z > 0f ? 1 : (rayDir.Z < 0f ? -1 : 0);

            float tMaxX = ComputeNextAxisBoundaryT(rayOrigin.X, rayDir.X, ix, stepX, half);
            float tMaxY = ComputeNextAxisBoundaryT(rayOrigin.Y, rayDir.Y, iy, stepY, half);
            float tMaxZ = ComputeNextAxisBoundaryT(rayOrigin.Z, rayDir.Z, iz, stepZ, half);
            float tDeltaX = stepX == 0 ? float.PositiveInfinity : MathF.Abs(1f / rayDir.X);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : MathF.Abs(1f / rayDir.Y);
            float tDeltaZ = stepZ == 0 ? float.PositiveInfinity : MathF.Abs(1f / rayDir.Z);

            var entryFace = EntryFaceFromRayAabbAxis(hitAxis, rayDir);
            int maxSteps = (size * 3) + 8;
            float tCurrent = tEnter;

            for (int step = 0; step < maxSteps; step++)
            {
                if ((uint)ix >= (uint)size || (uint)iy >= (uint)size || (uint)iz >= (uint)size)
                    return false;

                if (volume.IsOccupied(ix, iy, iz))
                {
                    picked = new PickedVoxelFace(ix, iy, iz, entryFace);
                    return true;
                }

                if (tCurrent > tExit)
                    return false;

                if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
                {
                    ix += stepX;
                    tCurrent = tMaxX;
                    tMaxX += tDeltaX;
                    entryFace = stepX > 0 ? Face.Left : Face.Right;
                }
                else if (tMaxY <= tMaxX && tMaxY <= tMaxZ)
                {
                    iy += stepY;
                    tCurrent = tMaxY;
                    tMaxY += tDeltaY;
                    entryFace = stepY > 0 ? Face.Bottom : Face.Top;
                }
                else
                {
                    iz += stepZ;
                    tCurrent = tMaxZ;
                    tMaxZ += tDeltaZ;
                    entryFace = stepZ > 0 ? Face.Front : Face.Back;
                }
            }

            return false;
        }

        private static float ComputeNextAxisBoundaryT(float origin, float dir, int voxelIndex, int step, float half)
        {
            if (step == 0 || MathF.Abs(dir) < 1e-12f)
                return float.PositiveInfinity;

            float boundary = step > 0
                ? (voxelIndex + 1) - half
                : voxelIndex - half;

            return (boundary - origin) / dir;
        }

        private static Face EntryFaceFromRayAabbAxis(int axis, Vector3 dir)
        {
            return axis switch
            {
                0 => dir.X >= 0f ? Face.Left : Face.Right,
                1 => dir.Y >= 0f ? Face.Bottom : Face.Top,
                2 => dir.Z >= 0f ? Face.Front : Face.Back,
                _ => Face.Front,
            };
        }

        private static bool TryIntersectRayAabb(
            Vector3 origin,
            Vector3 dir,
            Vector3 boxMin,
            Vector3 boxMax,
            out float tEnter,
            out float tExit,
            out int enterAxis)
        {
            tEnter = float.NegativeInfinity;
            tExit = float.PositiveInfinity;
            enterAxis = -1;

            for (int axis = 0; axis < 3; axis++)
            {
                float o = axis == 0 ? origin.X : (axis == 1 ? origin.Y : origin.Z);
                float d = axis == 0 ? dir.X : (axis == 1 ? dir.Y : dir.Z);
                float min = axis == 0 ? boxMin.X : (axis == 1 ? boxMin.Y : boxMin.Z);
                float max = axis == 0 ? boxMax.X : (axis == 1 ? boxMax.Y : boxMax.Z);

                if (MathF.Abs(d) < 1e-12f)
                {
                    if (o < min || o > max)
                        return false;
                    continue;
                }

                float inv = 1f / d;
                float t0 = (min - o) * inv;
                float t1 = (max - o) * inv;
                if (t0 > t1)
                {
                    (t0, t1) = (t1, t0);
                }

                if (t0 > tEnter)
                {
                    tEnter = t0;
                    enterAxis = axis;
                }

                if (t1 < tExit)
                    tExit = t1;

                if (tEnter > tExit)
                    return false;
            }

            return true;
        }

        // ════════════════════════════════════════════════════════════════════
        // POINTER INTERACTION (ORBIT + ZOOM)
        // ════════════════════════════════════════════════════════════════════

        private void Viewport_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            UIElement? inputTarget = (UIElement?)ViewportHost ?? ViewportImage;
            if (inputTarget == null) return;

            if (ViewportHost != null)
            {
                _ = ViewportHost.Focus(FocusState.Programmatic);
            }

            var point = e.GetCurrentPoint(inputTarget);
            var props = point.Properties;
            _lastPointerPos = point.Position;
            _lastStrokePaintFace = null;
            string? activeToolId = _voxelToolState.ActiveToolId;
            bool canUseVoxelTool =
                (!string.IsNullOrWhiteSpace(activeToolId)) &&
                IsVoxelClickToolActive(activeToolId);
            bool activeToolHandlesRightClick = _voxelToolState.ActiveRegistration?.Behavior?.HandlesRightClick == true;

            bool forceOrbit = props.IsRightButtonPressed && (!activeToolHandlesRightClick || !canUseVoxelTool);

            if (props.IsMiddleButtonPressed)
            {
                // Middle button pans, like the 2D canvas; right button orbits.
                _pointerDragMode = PointerDragMode.Pan;
            }
            else if (props.IsLeftButtonPressed && TryBeginLightHandleDrag(point.Position))
            {
                _pointerDragMode = PointerDragMode.LightHandle;
            }
            else if (forceOrbit)
            {
                _pointerDragMode = PointerDragMode.Orbit;
                _camera.BeginDrag();
            }
            else if ((props.IsLeftButtonPressed || (props.IsRightButtonPressed && activeToolHandlesRightClick)) &&
                     canUseVoxelTool)
            {
                _pointerDragMode = PointerDragMode.FacePaintStroke;
                TryApplyActiveVoxelToolAtHostPoint(point.Position, props, ToolPointerPhase.Pressed);
            }
            else
            {
                _pointerDragMode = PointerDragMode.Orbit;
                _camera.BeginDrag();
            }

            ((UIElement)sender).CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void ViewportHost_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdateLightingQuickActionsState();
            UpdateLightHandleOverlay();
        }

        private void ViewportHost_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateLightingQuickActionsState();
            UpdateLightHandleOverlay();
        }

        private void Viewport_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            UIElement? inputTarget = (UIElement?)ViewportHost ?? ViewportImage;
            if (inputTarget == null) return;

            var current = e.GetCurrentPoint(inputTarget);
            var pos = current.Position;
            float dx = (float)(pos.X - _lastPointerPos.X);
            float dy = (float)(pos.Y - _lastPointerPos.Y);

            if (_pointerDragMode == PointerDragMode.Orbit && _camera.IsDragging)
            {
                _lastPointerPos = pos;
                _camera.UpdateDrag(dx, dy);
                RenderViewport();
                e.Handled = true;
                return;
            }

            if (_pointerDragMode == PointerDragMode.Pan)
            {
                _lastPointerPos = pos;
                // Host DIPs → render pixels, so the point under the pointer stays put at any zoom.
                float sx = 1f, sy = 1f;
                if (TryGetPresentedImageRectInHostDip(out var imageRect) && imageRect.Width > 0 && imageRect.Height > 0)
                {
                    sx = _camera.ViewportWidth / (float)imageRect.Width;
                    sy = _camera.ViewportHeight / (float)imageRect.Height;
                }
                _camera.PanByScreen(dx * sx, dy * sy);
                RenderViewport();
                e.Handled = true;
                return;
            }

            if (_pointerDragMode == PointerDragMode.FacePaintStroke)
            {
                _lastPointerPos = pos;
                var props = current.Properties;
                if (props.IsLeftButtonPressed || props.IsRightButtonPressed)
                {
                    TryApplyActiveVoxelToolAtHostPoint(pos, props, ToolPointerPhase.Moved);
                }
                else
                {
                    _pointerDragMode = PointerDragMode.None;
                    _lastStrokePaintFace = null;
                }

                e.Handled = true;
                return;
            }

            if (_pointerDragMode == PointerDragMode.LightHandle)
            {
                _lastPointerPos = pos;
                if (current.Properties.IsLeftButtonPressed)
                {
                    UpdateLightHandleDrag(pos);
                }
                else
                {
                    EndLightHandleDrag();
                    _pointerDragMode = PointerDragMode.None;
                }

                e.Handled = true;
                return;
            }

            _lastPointerPos = pos;
        }

        private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            UIElement? inputTarget = (UIElement?)ViewportHost ?? ViewportImage;

            if (_pointerDragMode == PointerDragMode.FacePaintStroke && inputTarget != null)
            {
                var released = e.GetCurrentPoint(inputTarget);
                TryApplyActiveVoxelToolAtHostPoint(released.Position, released.Properties, ToolPointerPhase.Released);
            }

            if (_pointerDragMode == PointerDragMode.Orbit)
            {
                _camera.EndDrag();
            }
            else if (_pointerDragMode == PointerDragMode.Pan)
            {
                PersistVoxelPreviewStateToDocument();
            }
            else if (_pointerDragMode == PointerDragMode.LightHandle)
            {
                EndLightHandleDrag();
            }

            _pointerDragMode = PointerDragMode.None;
            _lastStrokePaintFace = null;
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        private void ViewportHost_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            bool ctrl = IsKeyDown(VirtualKey.Control);
            bool shift = IsKeyDown(VirtualKey.Shift);
            bool alt = IsKeyDown(VirtualKey.Menu);

            if (alt)
            {
                float step = shift ? 5f : 1f;
                Vector3 lightDelta = e.Key switch
                {
                    VirtualKey.Left => new Vector3(-step, 0f, 0f),
                    VirtualKey.Right => new Vector3(step, 0f, 0f),
                    VirtualKey.Up => new Vector3(0f, step, 0f),
                    VirtualKey.Down => new Vector3(0f, -step, 0f),
                    VirtualKey.PageUp => new Vector3(0f, 0f, step),
                    VirtualKey.PageDown => new Vector3(0f, 0f, -step),
                    _ => default
                };

                if (lightDelta != default)
                {
                    NudgeLightPosition(lightDelta);
                    e.Handled = true;
                    return;
                }
            }

            // Only voxel items are stepped here; a canvas item on top is left unhandled so the
            // window's accelerator routes it through the canvas host.
            if (ctrl && e.Key == VirtualKey.Z)
            {
                e.Handled = shift ? TryRedoVoxelEdit() : TryUndoVoxelEdit();
                return;
            }

            if (ctrl && e.Key == VirtualKey.Y)
            {
                e.Handled = TryRedoVoxelEdit();
                return;
            }

            if (_editEngine.Selection.Count == 0)
                return;

            Int3 delta = e.Key switch
            {
                VirtualKey.Left => new Int3(-1, 0, 0),
                VirtualKey.Right => new Int3(1, 0, 0),
                VirtualKey.Up => new Int3(0, 1, 0),
                VirtualKey.Down => new Int3(0, -1, 0),
                VirtualKey.PageUp => new Int3(0, 0, 1),
                VirtualKey.PageDown => new Int3(0, 0, -1),
                _ => default
            };

            if (delta != default)
            {
                if (_editEngine.MoveSelection(delta))
                {
                    UpdateVoxelSelectionStatusText($"Moved selection by ({delta.X},{delta.Y},{delta.Z}).");
                }
                else
                {
                    UpdateVoxelSelectionStatusText($"Move blocked for delta ({delta.X},{delta.Y},{delta.Z}).");
                }
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Delete)
            {
                DeleteSelectedVoxels();
                e.Handled = true;
            }
        }

        private void Viewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint((UIElement)sender).Properties;
            float delta = props.MouseWheelDelta / 120f;
            AdjustZoomForCurrentMode(delta);
            RenderViewport();
            e.Handled = true;
        }

        private void DeleteSelectedVoxels()
        {
            var selected = _editEngine.Selection.ToArray();
            if (selected.Length == 0)
                return;

            _editEngine.BeginHistoryTransaction("Delete Selected Voxels");
            bool any = false;
            try
            {
                for (int i = 0; i < selected.Length; i++)
                {
                    var s = selected[i];
                    any |= _editEngine.DeleteVoxel(s.X, s.Y, s.Z);
                }

                if (any)
                {
                    _editEngine.ClearSelection();
                    _editEngine.CommitHistoryTransaction();
                    UpdateVoxelSelectionStatusText("Deleted selected voxels.");
                }
                else
                {
                    _editEngine.CancelHistoryTransaction();
                    UpdateVoxelSelectionStatusText("Delete selected voxels: nothing changed.");
                }
            }
            catch
            {
                _editEngine.CancelHistoryTransaction();
                throw;
            }
        }
    }
}
