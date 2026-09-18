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
        // PRESET VIEW BUTTONS
        // ════════════════════════════════════════════════════════════════════

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string viewName)
            {
                _camera.SetView(viewName);
                StartAnimationLoop();
            }
        }

        private void ApplyIsoPreset_Click(object sender, RoutedEventArgs e)
        {
            var viewName = GetPresetTagFromCombo(IsoPresetCombo);
            if (string.IsNullOrWhiteSpace(viewName))
                return;

            _camera.SetView(viewName);
            StartAnimationLoop();
        }

        private void ApplyCardinalPreset_Click(object sender, RoutedEventArgs e)
        {
            var viewName = GetPresetTagFromCombo(CardinalPresetCombo);
            if (string.IsNullOrWhiteSpace(viewName))
                return;

            _camera.SetView(viewName);
            StartAnimationLoop();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _camera.Reset();
            RenderViewport();
        }

        private void FocusLightButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_document.VoxelWorkspace.LightingEnabled)
                return;

            if (!TryGetCameraFacingLightSpawnPosition(out var spawn) &&
                !TryGetRecommendedLightSpawnPosition(out spawn))
            {
                return;
            }

            SetCurrentLightPosition(spawn, commitUiRefresh: true);
            FlushPendingLightingHistory();
        }

        private void ResetLightButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_document.VoxelWorkspace.LightingEnabled)
                return;

            if (!TryGetRecommendedLightSpawnPosition(out var spawn))
                return;

            SetCurrentLightPosition(spawn, commitUiRefresh: true);
            FlushPendingLightingHistory();
        }

        /// <summary>
        /// Starts a render loop that ticks until the camera animation completes.
        /// </summary>
        private void StartAnimationLoop()
        {
            if (!_camera.IsAnimating)
            {
                RenderViewport();
                return;
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (s, _) =>
            {
                bool running = _camera.UpdateAnimation();
                RenderViewport();
                if (!running) timer.Stop();
            };
            timer.Start();
        }

        private static string? GetPresetTagFromCombo(ComboBox? combo)
        {
            if (combo?.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
                return tag;
            return null;
        }

        private void AdjustZoomForCurrentMode(float wheelSteps)
        {
            if (wheelSteps == 0f) return;

            bool pixelMode = PixelPreviewCheckBox?.IsChecked == true && _lastVolume != null;
            if (!pixelMode || _lastVolume == null)
            {
                _camera.Zoom(wheelSteps);
                return;
            }

            int basePixelSize = GetPixelPreviewBaseSize();
            int currentScreenPixelSize = ComputePixelPreviewScreenPixelSize(basePixelSize, _camera.ZoomPercent);

            int stepCount = Math.Max(1, (int)MathF.Round(MathF.Abs(wheelSteps)));
            int direction = wheelSteps > 0f ? 1 : -1;
            int targetScreenPixelSize = Math.Max(1, currentScreenPixelSize + (direction * stepCount));

            // Convert integer pixel-preview scale back into zoom percent.
            float targetZoomPercent = (targetScreenPixelSize * 100f) / basePixelSize;
            _camera.SetZoomPercent(targetZoomPercent);
        }

        private static int ComputePixelPreviewScreenPixelSize(int basePixelSize, float zoomPercent)
        {
            float zoomScale = MathF.Max(0.01f, zoomPercent / 100f);
            return Math.Max(1, (int)MathF.Floor((basePixelSize * zoomScale) + 1e-3f));
        }

        /// <summary>
        /// Pixel-preview zoom in PixZel feels stable because zooming mainly scales a low-res
        /// render, rather than re-rasterizing geometry at every zoom step. For free rotation
        /// / isometric views, cache a pixel-perfect low-res sprite per camera orientation and
        /// reuse it while only zoom changes.
        /// </summary>
        private bool TryRenderCachedPixelPreviewSprite(
            VoxelVolume volume,
            int renderW,
            int renderH,
            byte[] buffer,
            uint clearColor,
            VoxelRenderer.RenderOptions opts)
        {
            if (volume == null) return false;
            if (buffer == null || buffer.Length < renderW * renderH * 4) return false;

            int spriteSide = ComputePixelPreviewSpriteSide(volume.Size, opts.OutlineSize);
            EnsurePixelPreviewSpriteCache(volume, spriteSide, opts);
            if (_pixelPreviewSpriteCache == null) return false;

            bool cacheValid =
                MathF.Abs(_pixelPreviewSpriteCache.Pitch - _camera.Pitch) <= 1e-6f &&
                MathF.Abs(_pixelPreviewSpriteCache.Yaw - _camera.Yaw) <= 1e-6f &&
                string.Equals(_pixelPreviewSpriteCache.SnapName, _camera.CurrentSnapName, StringComparison.OrdinalIgnoreCase) &&
                _pixelPreviewSpriteCache.DrawOutline == opts.DrawOutline &&
                _pixelPreviewSpriteCache.OutlineColor == opts.OutlineColor &&
                _pixelPreviewSpriteCache.OutlineSize == opts.OutlineSize &&
                _pixelPreviewSpriteCache.DrawSurfaceVoxelGrid == opts.DrawSurfaceVoxelGrid &&
                _pixelPreviewSpriteCache.SurfaceVoxelGridColor == opts.SurfaceVoxelGridColor &&
                _pixelPreviewSpriteCache.LightingEnabled == opts.LightingEnabled &&
                NearlyEqual(_pixelPreviewSpriteCache.LightPosX, opts.LightPosition.X) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightPosY, opts.LightPosition.Y) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightPosZ, opts.LightPosition.Z) &&
                _pixelPreviewSpriteCache.LightColor == opts.LightColor &&
                _pixelPreviewSpriteCache.ShadowColor == opts.ShadowColor &&
                NearlyEqual(_pixelPreviewSpriteCache.ShadowStrength, opts.ShadowStrength) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightIntensity, opts.LightIntensity) &&
                NearlyEqual(_pixelPreviewSpriteCache.AmbientIntensity, opts.AmbientIntensity) &&
                NearlyEqual(_pixelPreviewSpriteCache.LightFalloff, opts.LightFalloff) &&
                _pixelPreviewSpriteCache.LightCastShadows == opts.LightCastShadows;

            if (!cacheValid)
            {
                // The cache render temporarily changes the camera's pixel-perfect render target.
                // Restore the current viewport/frustum immediately afterward.
                _camera.EnablePixelPerfectFrustum(spriteSide, spriteSide);
                _camera.ResizeViewport(spriteSide, spriteSide);

                VoxelRenderer.Render(
                    volume, _camera,
                    spriteSide, spriteSide,
                    _pixelPreviewSpriteCache.Buffer,
                    clearColor: 0x00000000, // transparent background for compositing
                    CloneRenderOptionsForSpriteCache(opts));

                _pixelPreviewSpriteCache.Width = spriteSide;
                _pixelPreviewSpriteCache.Height = spriteSide;
                _pixelPreviewSpriteCache.Pitch = _camera.Pitch;
                _pixelPreviewSpriteCache.Yaw = _camera.Yaw;
                _pixelPreviewSpriteCache.SnapName = _camera.CurrentSnapName;
                _pixelPreviewSpriteCache.DrawOutline = opts.DrawOutline;
                _pixelPreviewSpriteCache.OutlineColor = opts.OutlineColor;
                _pixelPreviewSpriteCache.OutlineSize = opts.OutlineSize;
                _pixelPreviewSpriteCache.DrawSurfaceVoxelGrid = opts.DrawSurfaceVoxelGrid;
                _pixelPreviewSpriteCache.SurfaceVoxelGridColor = opts.SurfaceVoxelGridColor;
                _pixelPreviewSpriteCache.LightingEnabled = opts.LightingEnabled;
                _pixelPreviewSpriteCache.LightPosX = opts.LightPosition.X;
                _pixelPreviewSpriteCache.LightPosY = opts.LightPosition.Y;
                _pixelPreviewSpriteCache.LightPosZ = opts.LightPosition.Z;
                _pixelPreviewSpriteCache.LightColor = opts.LightColor;
                _pixelPreviewSpriteCache.ShadowColor = opts.ShadowColor;
                _pixelPreviewSpriteCache.ShadowStrength = opts.ShadowStrength;
                _pixelPreviewSpriteCache.LightIntensity = opts.LightIntensity;
                _pixelPreviewSpriteCache.AmbientIntensity = opts.AmbientIntensity;
                _pixelPreviewSpriteCache.LightFalloff = opts.LightFalloff;
                _pixelPreviewSpriteCache.LightCastShadows = opts.LightCastShadows;
            }

            _camera.EnablePixelPerfectFrustum(renderW, renderH);
            _camera.ResizeViewport(renderW, renderH);

            FillClear(buffer, renderW * renderH, clearColor);
            BlitCenteredOpaqueOverBackground(
                _pixelPreviewSpriteCache.Buffer,
                _pixelPreviewSpriteCache.Width, _pixelPreviewSpriteCache.Height,
                buffer, renderW, renderH);

            return true;
        }

        private void EnsurePixelPreviewSpriteCache(
            VoxelVolume volume,
            int spriteSide,
            VoxelRenderer.RenderOptions opts)
        {
            bool needsNew =
                _pixelPreviewSpriteCache == null ||
                !ReferenceEquals(_pixelPreviewSpriteCache.Volume, volume) ||
                _pixelPreviewSpriteCache.Width != spriteSide ||
                _pixelPreviewSpriteCache.Height != spriteSide ||
                _pixelPreviewSpriteCache.Buffer.Length != spriteSide * spriteSide * 4;

            if (needsNew)
            {
                _pixelPreviewSpriteCache = new PixelPreviewSpriteCache
                {
                    Volume = volume,
                    Buffer = new byte[spriteSide * spriteSide * 4],
                    Width = spriteSide,
                    Height = spriteSide,
                    Pitch = float.NaN,
                    Yaw = float.NaN,
                    SnapName = null,
                    DrawOutline = opts.DrawOutline,
                    OutlineColor = opts.OutlineColor,
                    OutlineSize = opts.OutlineSize,
                    DrawSurfaceVoxelGrid = opts.DrawSurfaceVoxelGrid,
                    SurfaceVoxelGridColor = opts.SurfaceVoxelGridColor,
                    LightingEnabled = opts.LightingEnabled,
                    LightPosX = opts.LightPosition.X,
                    LightPosY = opts.LightPosition.Y,
                    LightPosZ = opts.LightPosition.Z,
                    LightColor = opts.LightColor,
                    ShadowColor = opts.ShadowColor,
                    ShadowStrength = opts.ShadowStrength,
                    LightIntensity = opts.LightIntensity,
                    AmbientIntensity = opts.AmbientIntensity,
                    LightFalloff = opts.LightFalloff,
                    LightCastShadows = opts.LightCastShadows,
                };
                return;
            }

            // If only zoom changed, the cache remains valid. We don't need to clear or rerender here.
        }

        private static int ComputePixelPreviewSpriteSide(int volumeSize, int outlineSize)
        {
            volumeSize = Math.Max(1, volumeSize);
            outlineSize = Math.Max(0, outlineSize);

            // Max projected cube extent on one screen axis is < size * sqrt(3). Add padding
            // for outline and breathing room so the sprite can be re-centered/cropped safely.
            int pad = Math.Max(8, outlineSize + 6);
            int side = (int)MathF.Ceiling((volumeSize * 1.9f) + (pad * 2));
            side = Math.Max(side, volumeSize + (pad * 2));
            if ((side & 1) == 0) side++;
            return side;
        }

        private static VoxelRenderer.RenderOptions CloneRenderOptionsForSpriteCache(VoxelRenderer.RenderOptions src)
        {
            return new VoxelRenderer.RenderOptions
            {
                BackfaceCull = src.BackfaceCull,
                BackfaceCullEpsilon = src.BackfaceCullEpsilon,
                DrawOutline = src.DrawOutline,
                OutlineColor = src.OutlineColor,
                OutlineSize = src.OutlineSize,
                DrawBackdropGrid = false,
                DrawBackdropProjectionTiles = false,
                BackdropGridMinorColor = src.BackdropGridMinorColor,
                BackdropGridMajorColor = src.BackdropGridMajorColor,
                BackdropGridMajorEvery = src.BackdropGridMajorEvery,
                BackdropGridMarginVoxels = src.BackdropGridMarginVoxels,
                BackdropCageScale = src.BackdropCageScale,
                BackdropFrontProjection = null,
                BackdropBackProjection = null,
                BackdropLeftProjection = null,
                BackdropRightProjection = null,
                BackdropTopProjection = null,
                BackdropBottomProjection = null,
                DrawSurfaceVoxelGrid = src.DrawSurfaceVoxelGrid,
                SurfaceVoxelGridColor = src.SurfaceVoxelGridColor,
                LightingEnabled = src.LightingEnabled,
                LightPosition = src.LightPosition,
                LightColor = src.LightColor,
                ShadowColor = src.ShadowColor,
                ShadowStrength = src.ShadowStrength,
                LightIntensity = src.LightIntensity,
                AmbientIntensity = src.AmbientIntensity,
                LightFalloff = src.LightFalloff,
                LightCastShadows = src.LightCastShadows,
            };
        }

        private static void BlitCenteredOpaqueOverBackground(
            byte[] src, int srcW, int srcH,
            byte[] dst, int dstW, int dstH)
        {
            if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return;
            if (src == null || dst == null) return;

            int offsetX = (dstW - srcW) / 2;
            int offsetY = (dstH - srcH) / 2;

            for (int sy = 0; sy < srcH; sy++)
            {
                int dy = sy + offsetY;
                if ((uint)dy >= (uint)dstH) continue;

                for (int sx = 0; sx < srcW; sx++)
                {
                    int dx = sx + offsetX;
                    if ((uint)dx >= (uint)dstW) continue;

                    int si = (sy * srcW + sx) * 4;
                    byte a = src[si + 3];
                    if (a == 0) continue;

                    int di = (dy * dstW + dx) * 4;
                    dst[di] = src[si];
                    dst[di + 1] = src[si + 1];
                    dst[di + 2] = src[si + 2];
                    dst[di + 3] = a;
                }
            }
        }

        /// <summary>
        /// In pixel preview + snapped orthographic views, bypasses the triangle rasterizer and
        /// stamps an exact voxel-orthographic image into the low-res render target. This makes
        /// zoom behave like scaling a rendered image (PixZel-style) instead of re-quantizing
        /// geometry on each step.
        /// </summary>
        private bool TryRenderExactCardinalPixelPreview(
            VoxelVolume volume,
            int renderW,
            int renderH,
            byte[] buffer,
            uint clearColor,
            VoxelRenderer.RenderOptions opts)
        {
            if (volume == null) return false;
            if (_camera.IsAnimating) return false;

            string? snap = _camera.CurrentSnapName;
            if (string.IsNullOrWhiteSpace(snap)) return false;

            if (!IsCardinalOrthoSnap(snap))
                return false;

            if (!TryGetCachedCardinalPixelPreviewImage(volume, snap, out var img, out var visibleFace))
                return false;

            // Keep cardinal pixel-preview on the exact image path even with lighting enabled.
            // This avoids low-res triangle-raster cracks when lighting is toggled on.

            FillClear(buffer, renderW * renderH, clearColor);

            int size = img.Width;
            int startX = (renderW - size) / 2;
            int startY = (renderH - size) / 2;

            bool needMask = opts.DrawOutline || opts.DrawSurfaceVoxelGrid;
            bool[]? objectMask = needMask ? ArrayPool<bool>.Shared.Rent(renderW * renderH) : null;
            if (objectMask != null) Array.Clear(objectMask, 0, renderW * renderH);

            for (int y = 0; y < img.Height; y++)
            {
                int dy = startY + y;
                if ((uint)dy >= (uint)renderH) continue;

                for (int x = 0; x < img.Width; x++)
                {
                    int dx = startX + x;
                    if ((uint)dx >= (uint)renderW) continue;

                    var c = img.GetPixel(x, y);
                    if (c.A == 0) continue;

                    var lit = ApplyFaceLighting(c, visibleFace, opts);
                    int bi = (dy * renderW + dx) * 4;
                    buffer[bi] = lit.B;
                    buffer[bi + 1] = lit.G;
                    buffer[bi + 2] = lit.R;
                    buffer[bi + 3] = lit.A;

                    if (objectMask != null)
                        objectMask[dy * renderW + dx] = true;
                }
            }

            if (opts.DrawOutline && objectMask != null && opts.OutlineSize > 0)
            {
                ApplyExteriorSilhouetteOutlineToMask(
                    buffer, objectMask, renderW, renderH,
                    opts.OutlineColor, opts.OutlineSize);
            }

            if (objectMask != null)
                ArrayPool<bool>.Shared.Return(objectMask);

            return true;
        }

        private bool TryGetCachedCardinalPixelPreviewImage(
            VoxelVolume volume,
            string snapName,
            out ImageData image,
            out Face visibleFace)
        {
            _cachedCardinalPixelPreviewImages ??= new Dictionary<string, (ImageData Image, Face Face)>(StringComparer.OrdinalIgnoreCase);

            if (_cachedCardinalPixelPreviewImages.TryGetValue(snapName, out var cached))
            {
                image = cached.Image;
                visibleFace = cached.Face;
                return true;
            }

            if (!TryBuildCardinalPixelPreviewImage(volume, snapName, out image, out visibleFace))
                return false;

            _cachedCardinalPixelPreviewImages[snapName] = (image, visibleFace);
            return true;
        }

        private static bool IsCardinalOrthoSnap(string snapName)
        {
            return snapName.Equals("front", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("back", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("left", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("right", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("top", StringComparison.OrdinalIgnoreCase) ||
                   snapName.Equals("bottom", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryBuildCardinalPixelPreviewImage(
            VoxelVolume volume,
            string snapName,
            out ImageData image,
            out Face visibleFace)
        {
            int size = volume.Size;
            image = new ImageData(size, size);
            visibleFace = Face.Front;

            // Cardinal views are generated explicitly so they match the volume colors
            // (6-face overrides included) without going through triangle rasterization.
            image = new ImageData(size, size);

            static int Flip(int v, int n) => n - 1 - v;

            if (snapName.Equals("front", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Back;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int x = 0; x < size; x++)
                    {
                        for (int z = size - 1; z >= 0; z--) // camera at +Z → nearest is highest z
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = x;
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Back));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("back", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Front;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int x = 0; x < size; x++)
                    {
                        for (int z = 0; z < size; z++) // camera at -Z → nearest is lowest z
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = Flip(x, size);
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Front));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("left", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Right;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int z = 0; z < size; z++)
                    {
                        for (int x = size - 1; x >= 0; x--) // camera at +X → nearest is highest x
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = Flip(z, size);
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Right));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("right", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Left;
                for (int y = 0; y < size; y++)
                {
                    int yFlip = Flip(y, size);
                    for (int z = 0; z < size; z++)
                    {
                        for (int x = 0; x < size; x++) // camera at -X → nearest is lowest x
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = z;
                            int row = yFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Left));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("top", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Top;
                for (int z = 0; z < size; z++)
                {
                    int zFlip = Flip(z, size);
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = size - 1; y >= 0; y--) // camera at +Y → nearest is highest y
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = x;
                            int row = zFlip;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Top));
                            break;
                        }
                    }
                }
                return true;
            }

            if (snapName.Equals("bottom", StringComparison.OrdinalIgnoreCase))
            {
                visibleFace = Face.Bottom;
                for (int z = 0; z < size; z++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        for (int y = 0; y < size; y++) // camera at -Y → nearest is lowest y
                        {
                            if (!volume.IsOccupied(x, y, z)) continue;
                            int col = Flip(x, size);
                            int row = z;
                            image.SetPixel(col, row, volume.GetFaceColor(x, y, z, Face.Bottom));
                            break;
                        }
                    }
                }
                return true;
            }

            return false;
        }

        private static Rgba32 ApplyFaceLighting(Rgba32 c, Face face, VoxelRenderer.RenderOptions opts)
        {
            if (!opts.LightingEnabled)
                return c;

            var normal = face switch
            {
                Face.Front => new Vector3(0f, 0f, -1f),
                Face.Back => new Vector3(0f, 0f, 1f),
                Face.Left => new Vector3(-1f, 0f, 0f),
                Face.Right => new Vector3(1f, 0f, 0f),
                Face.Top => new Vector3(0f, 1f, 0f),
                Face.Bottom => new Vector3(0f, -1f, 0f),
                _ => new Vector3(0f, 0f, -1f),
            };

            Vector3 toLight = opts.LightPosition;
            float distSq = MathF.Max(1e-6f, toLight.LengthSquared());
            float invDist = 1f / MathF.Sqrt(distSq);
            float dist = distSq * invDist;
            Vector3 lightDir = toLight * invDist;

            float ndotl = MathF.Max(0f, Vector3.Dot(normal, lightDir));
            float attenuation = 1f / (1f + MathF.Max(0f, opts.LightFalloff) * dist);
            float diffuse = ndotl * MathF.Max(0f, opts.LightIntensity) * attenuation;

            float lr = ((opts.LightColor >> 16) & 0xFF) / 255f;
            float lg = ((opts.LightColor >> 8) & 0xFF) / 255f;
            float lb = (opts.LightColor & 0xFF) / 255f;
            float shadowR = ((opts.ShadowColor >> 16) & 0xFF) / 255f;
            float shadowG = ((opts.ShadowColor >> 8) & 0xFF) / 255f;
            float shadowB = (opts.ShadowColor & 0xFF) / 255f;
            float shadowA = ((opts.ShadowColor >> 24) & 0xFF) / 255f;
            float litScalar = Math.Clamp(diffuse, 0f, 1f);
            float shadowStrength = Math.Clamp(opts.ShadowStrength, 0f, 1f);
            float shadowMix = (1f - litScalar) * shadowA * shadowStrength;

            float litR = c.R * (diffuse * lr);
            float litG = c.G * (diffuse * lg);
            float litB = c.B * (diffuse * lb);
            float outR = litR * (1f - shadowMix) + (shadowR * 255f * shadowMix);
            float outG = litG * (1f - shadowMix) + (shadowG * 255f * shadowMix);
            float outB = litB * (1f - shadowMix) + (shadowB * 255f * shadowMix);

            return new Rgba32(
                ClampToByte(outR),
                ClampToByte(outG),
                ClampToByte(outB),
                c.A);
        }

        private static void AlphaBlendPackedBgra(byte[] buffer, int bi, uint bgra)
        {
            byte srcB = (byte)(bgra & 0xFF);
            byte srcG = (byte)((bgra >> 8) & 0xFF);
            byte srcR = (byte)((bgra >> 16) & 0xFF);
            byte srcA = (byte)((bgra >> 24) & 0xFF);
            if (srcA == 0) return;

            if (srcA == 255)
            {
                buffer[bi] = srcB;
                buffer[bi + 1] = srcG;
                buffer[bi + 2] = srcR;
                buffer[bi + 3] = 255;
                return;
            }

            int invA = 255 - srcA;
            buffer[bi] = (byte)((srcB * srcA + buffer[bi] * invA) / 255);
            buffer[bi + 1] = (byte)((srcG * srcA + buffer[bi + 1] * invA) / 255);
            buffer[bi + 2] = (byte)((srcR * srcA + buffer[bi + 2] * invA) / 255);
            buffer[bi + 3] = 255;
        }

        private static byte ClampToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 255f) return 255;
            return (byte)MathF.Round(v);
        }

        private static bool NearlyEqual(float a, float b)
            => MathF.Abs(a - b) <= 0.0001f;

        private static bool NearlyEqual(double a, double b)
            => Math.Abs(a - b) <= 0.0001d;

        private static void ApplyExteriorSilhouetteOutlineToMask(
            byte[] buffer,
            bool[] objectMask,
            int width,
            int height,
            uint outlineColor,
            int outlineSize)
        {
            if (outlineSize <= 0) return;
            int pixelCount = width * height;
            if (objectMask.Length != pixelCount) return;

            bool any = false;
            for (int i = 0; i < pixelCount; i++)
            {
                if (objectMask[i]) { any = true; break; }
            }
            if (!any) return;

            var exterior = ArrayPool<bool>.Shared.Rent(pixelCount);
            var flood = ArrayPool<int>.Shared.Rent(pixelCount);
            var dist = ArrayPool<int>.Shared.Rent(pixelCount);
            var q = ArrayPool<int>.Shared.Rent(pixelCount);

            try
            {
                Array.Clear(exterior, 0, pixelCount);
                Array.Clear(flood, 0, pixelCount);
                Array.Clear(dist, 0, pixelCount);
                Array.Clear(q, 0, pixelCount);

                int fh = 0, ft = 0;

                void TryEnqueueExterior(int x, int y)
                {
                    if ((uint)x >= (uint)width || (uint)y >= (uint)height) return;
                    int idx = y * width + x;
                    if (objectMask[idx] || exterior[idx]) return;
                    exterior[idx] = true;
                    flood[ft++] = idx;
                }

                for (int x = 0; x < width; x++)
                {
                    TryEnqueueExterior(x, 0);
                    TryEnqueueExterior(x, height - 1);
                }
                for (int y = 1; y < height - 1; y++)
                {
                    TryEnqueueExterior(0, y);
                    TryEnqueueExterior(width - 1, y);
                }

                while (fh < ft)
                {
                    int idx = flood[fh++];
                    int x = idx % width;
                    int y = idx / width;

                    TryEnqueueExterior(x - 1, y);
                    TryEnqueueExterior(x + 1, y);
                    TryEnqueueExterior(x, y - 1);
                    TryEnqueueExterior(x, y + 1);
                }

                int radius = Math.Max(1, outlineSize);
                int qh = 0, qt = 0;

                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = row + x;
                        if (!exterior[idx]) continue;

                        bool nearObject = false;
                        for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1) && !nearObject; ny++)
                        {
                            int nrow = ny * width;
                            for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                            {
                                if (objectMask[nrow + nx])
                                {
                                    nearObject = true;
                                    break;
                                }
                            }
                        }

                        if (!nearObject) continue;
                        dist[idx] = 1;
                        q[qt++] = idx;
                    }
                }

                while (qh < qt)
                {
                    int idx = q[qh++];
                    int d = dist[idx];
                    if (d >= radius) continue;

                    int x = idx % width;
                    int y = idx / width;
                    for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
                    {
                        int nrow = ny * width;
                        for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                        {
                            int ni = nrow + nx;
                            if (!exterior[ni] || dist[ni] != 0) continue;
                            dist[ni] = d + 1;
                            q[qt++] = ni;
                        }
                    }
                }

                byte b = (byte)(outlineColor & 0xFF);
                byte g = (byte)((outlineColor >> 8) & 0xFF);
                byte r = (byte)((outlineColor >> 16) & 0xFF);
                byte a = (byte)((outlineColor >> 24) & 0xFF);

                for (int i = 0; i < qt; i++)
                {
                    int idx = q[i];
                    if (objectMask[idx]) continue;
                    int bi = idx * 4;
                    buffer[bi] = b;
                    buffer[bi + 1] = g;
                    buffer[bi + 2] = r;
                    buffer[bi + 3] = a;
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(q);
                ArrayPool<int>.Shared.Return(dist);
                ArrayPool<int>.Shared.Return(flood);
                ArrayPool<bool>.Shared.Return(exterior);
            }
        }

        private void DrawPixelPreviewBackdropCage3D(
            byte[] buffer,
            int width,
            int height,
            int renderW,
            int renderH,
            int screenPixelSize,
            int volumeSize,
            uint minorColor,
            uint majorColor,
            int majorEvery,
            float cageScale)
        {
            if (buffer == null || buffer.Length < width * height * 4) return;
            if (width <= 0 || height <= 0 || renderW <= 0 || renderH <= 0) return;
            if (volumeSize <= 0 || screenPixelSize <= 0) return;

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);
            var fr = _camera.GetFrustum();
            float frWidth = MathF.Max(1e-6f, fr.Width);
            float frHeight = MathF.Max(1e-6f, fr.Height);
            float frCenterX = (fr.Left + fr.Right) * 0.5f;
            float frCenterY = (fr.Top + fr.Bottom) * 0.5f;

            Vector3 ProjectToDisplay(Vector3 world)
            {
                var rel = world - pose.Position;
                float cx = Vector3.Dot(rel, basis.Right);
                float cy = Vector3.Dot(rel, basis.Up);
                float cz = Vector3.Dot(rel, basis.Forward);
                float xNdc = (2f * cx - 2f * frCenterX) / frWidth;
                float yNdc = (2f * cy - 2f * frCenterY) / frHeight;
                float sxRender = (xNdc * 0.5f + 0.5f) * renderW;
                float syRender = (1f - (yNdc * 0.5f + 0.5f)) * renderH;
                return new Vector3(sxRender * screenPixelSize, syRender * screenPixelSize, cz);
            }

            void DrawLine(Vector3 a, Vector3 b, uint color)
            {
                if (a.Z <= 0f && b.Z <= 0f) return;
                RasterizePackedBgraLine(buffer, width, height, a.X, a.Y, b.X, b.Y, color);
            }

            void DrawGridPlane(Vector3 center, Vector3 axisA, Vector3 axisB, int extentA, int extentB)
            {
                float gridPhase = (volumeSize & 1) == 0 ? 0f : 0.5f;
                for (int a = -extentA; a <= extentA; a++)
                {
                    bool major = majorEvery > 0 && (Math.Abs(a) % majorEvery) == 0;
                    uint color = major ? majorColor : minorColor;
                    float aPos = a + gridPhase;
                    var p0 = ProjectToDisplay(center + (axisA * aPos) + (axisB * (-extentB + gridPhase)));
                    var p1 = ProjectToDisplay(center + (axisA * aPos) + (axisB * (extentB + gridPhase)));
                    DrawLine(p0, p1, color);
                }

                for (int b = -extentB; b <= extentB; b++)
                {
                    bool major = majorEvery > 0 && (Math.Abs(b) % majorEvery) == 0;
                    uint color = major ? majorColor : minorColor;
                    float bPos = b + gridPhase;
                    var p0 = ProjectToDisplay(center + (axisB * bPos) + (axisA * (-extentA + gridPhase)));
                    var p1 = ProjectToDisplay(center + (axisB * bPos) + (axisA * (extentA + gridPhase)));
                    DrawLine(p0, p1, color);
                }
            }

            float modelHalf = MathF.Max(0.5f, volumeSize * 0.5f);
            float cageHalf = MathF.Max(modelHalf + 1f, modelHalf * MathF.Max(1.05f, cageScale));
            int extent = Math.Max(1, (int)MathF.Round(cageHalf));
            majorEvery = Math.Max(0, majorEvery);
            bool farXPositive = basis.Forward.X >= 0f;
            bool farYPositive = basis.Forward.Y >= 0f;
            bool farZPositive = basis.Forward.Z >= 0f;

            DrawGridPlane(new Vector3(0f, 0f, farZPositive ? cageHalf : -cageHalf), Vector3.UnitX, Vector3.UnitY, extent, extent);
            DrawGridPlane(new Vector3(farXPositive ? cageHalf : -cageHalf, 0f, 0f), Vector3.UnitZ, Vector3.UnitY, extent, extent);
            DrawGridPlane(new Vector3(0f, farYPositive ? cageHalf : -cageHalf, 0f), Vector3.UnitX, Vector3.UnitZ, extent, extent);
        }

        private static void DrawExactCardinalPixelPreviewSurfaceGrid(
            byte[] renderBuffer,
            int renderW,
            int renderH,
            byte[] displayBuffer,
            int displayW,
            int displayH,
            int screenPixelSize,
            uint gridColor)
        {
            if (screenPixelSize <= 1) return;
            if (renderBuffer == null || displayBuffer == null) return;
            if (renderBuffer.Length < renderW * renderH * 4) return;
            if (displayBuffer.Length < displayW * displayH * 4) return;

            static bool IsOpaque(byte[] buf, int w, int x, int y)
                => buf[((y * w + x) * 4) + 3] != 0;

            for (int y = 0; y < renderH; y++)
            {
                for (int x = 0; x < renderW; x++)
                {
                    if (!IsOpaque(renderBuffer, renderW, x, y))
                        continue;

                    int baseX = x * screenPixelSize;
                    int baseY = y * screenPixelSize;

                    if (x + 1 < renderW && IsOpaque(renderBuffer, renderW, x + 1, y))
                    {
                        int lineX = baseX + screenPixelSize - 1;
                        for (int py = 0; py < screenPixelSize; py++)
                        {
                            int dy = baseY + py;
                            if ((uint)lineX >= (uint)displayW || (uint)dy >= (uint)displayH) continue;
                            AlphaBlendPackedBgra(displayBuffer, (dy * displayW + lineX) * 4, gridColor);
                        }
                    }

                    if (y + 1 < renderH && IsOpaque(renderBuffer, renderW, x, y + 1))
                    {
                        int lineY = baseY + screenPixelSize - 1;
                        for (int px = 0; px < screenPixelSize; px++)
                        {
                            int dx = baseX + px;
                            if ((uint)dx >= (uint)displayW || (uint)lineY >= (uint)displayH) continue;
                            AlphaBlendPackedBgra(displayBuffer, (lineY * displayW + dx) * 4, gridColor);
                        }
                    }
                }
            }
        }

        private static void RasterizePackedBgraLine(
            byte[] buffer, int width, int height,
            float x0, float y0, float x1, float y1,
            uint color)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            int steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy))));
            float ix = dx / steps;
            float iy = dy / steps;
            float px = x0;
            float py = y0;

            for (int i = 0; i <= steps; i++)
            {
                int sx = (int)MathF.Round(px);
                int sy = (int)MathF.Round(py);
                if ((uint)sx < (uint)width && (uint)sy < (uint)height)
                {
                    AlphaBlendPackedBgra(buffer, (sy * width + sx) * 4, color);
                }

                px += ix;
                py += iy;
            }
        }

        private void UpdateCameraStatsText(bool pixelMode, int screenPixelSize, int renderW, int renderH)
        {
            if (CameraStatsText == null) return;

            float pitchDeg = _camera.Pitch * (180f / MathF.PI);
            float yawDeg = _camera.Yaw * (180f / MathF.PI);
            if (yawDeg < 0f) yawDeg += 360f;
            int pitchDegInt = (int)MathF.Round(pitchDeg);
            int yawDegInt = ((int)MathF.Round(yawDeg)) % 360;
            if (yawDegInt < 0) yawDegInt += 360;
            string snap = _camera.CurrentSnapName ?? "custom";
            float voxPx = 0f;

            if (pixelMode && _lastVolume != null)
            {
                int pixelBaseSize = GetPixelPreviewBaseSize();
                float effectiveZoom = (screenPixelSize * 100f) / Math.Max(1, pixelBaseSize);
                voxPx = screenPixelSize;
                CameraStatsText.Text =
                    $"p {pitchDegInt,4:0}  y {yawDegInt,4:0}  z {effectiveZoom,5:0.#}%  vpx {voxPx,4:0.#}  b {pixelBaseSize}  rt {renderW}x{renderH}  {snap}";
            }
            else
            {
                var fr = _camera.GetFrustum();
                voxPx = renderH / MathF.Max(1e-6f, fr.Height);
                CameraStatsText.Text =
                    $"p {pitchDegInt,4:0}  y {yawDegInt,4:0}  z {_camera.ZoomPercent,5:0.#}%  vpx {voxPx,4:0.#}  {snap}";
            }
        }

        private void UpdateLightingQuickActionsState()
        {
            bool enabled = _document.VoxelWorkspace.LightingEnabled && _hasOccupiedBounds;

            if (FocusLightButton != null)
            {
                FocusLightButton.IsEnabled = enabled;
                FocusLightButton.Opacity = enabled ? 1.0 : 0.6;
            }

            if (ResetLightButton != null)
            {
                ResetLightButton.IsEnabled = enabled;
                ResetLightButton.Opacity = enabled ? 1.0 : 0.6;
            }
        }

        private void UpdateAxisGizmo()
        {
            if (AxisXLine == null || AxisYLine == null || AxisZLine == null ||
                AxisXLabel == null || AxisYLabel == null || AxisZLabel == null)
            {
                return;
            }

            var pose = _camera.GetCameraPose();
            var basis = _camera.GetCameraBasis(pose);

            const float center = 42f;
            const float axisLen = 22f;

            static (float cx, float cy, float cz) ProjectAxis(Vector3 axis, OrbitCamera.CameraBasis basis)
            {
                // Match the intended viewport triad look:
                // - horizontal mirror so +X appears on the expected side,
                // - invert vertical only for X/Z so SW view reads as Y up,
                //   X down-right, Z down-left (Maya-like visual cue).
                float cx = -Vector3.Dot(axis, basis.Right);
                float cy = Vector3.Dot(axis, basis.Up);
                if (MathF.Abs(axis.Y) < 0.5f)
                    cy = -cy;
                float cz = Vector3.Dot(axis, basis.Forward);
                return (cx, cy, cz);
            }

            void SetAxis(Line line, TextBlock label, Vector3 axis, bool placeLabelAtTip)
            {
                var (cx, cy, cz) = ProjectAxis(axis, basis);
                float ex = center + cx * axisLen;
                float ey = center - cy * axisLen;

                line.X1 = center;
                line.Y1 = center;
                line.X2 = ex;
                line.Y2 = ey;
                line.Opacity = 0.5 + 0.5 * Math.Clamp((double)(cz * 0.5f + 0.5f), 0.0, 1.0);

                if (placeLabelAtTip)
                {
                    // Place label along the axis direction so it sits at the line tip,
                    // not laterally offset to screen-right.
                    float vx = ex - center;
                    float vy = ey - center;
                    float vLen = MathF.Sqrt(vx * vx + vy * vy);
                    if (vLen < 1e-5f) vLen = 1f;
                    float ux = vx / vLen;
                    float uy = vy / vLen;
                    const float tipGap = 7f;
                    float lx = ex + ux * tipGap;
                    float ly = ey + uy * tipGap;

                    label.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                    float lw = (float)label.DesiredSize.Width;
                    float lh = (float)label.DesiredSize.Height;
                    Canvas.SetLeft(label, lx - lw * 0.5f);
                    Canvas.SetTop(label, ly - lh * 0.5f);
                }
                else
                {
                    Canvas.SetLeft(label, ex + 2f);
                    Canvas.SetTop(label, ey - 8f);
                }
                label.Opacity = line.Opacity;
            }

            SetAxis(AxisXLine, AxisXLabel, Vector3.UnitX, placeLabelAtTip: true);
            SetAxis(AxisYLine, AxisYLabel, Vector3.UnitY, placeLabelAtTip: true);
            SetAxis(AxisZLine, AxisZLabel, Vector3.UnitZ, placeLabelAtTip: true);
            _axisXEnd = new Vector2((float)AxisXLine.X2, (float)AxisXLine.Y2);
            _axisYEnd = new Vector2((float)AxisYLine.X2, (float)AxisYLine.Y2);
            _axisZEnd = new Vector2((float)AxisZLine.X2, (float)AxisZLine.Y2);
        }

        private void AxisGizmoCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (AxisGizmoCanvas == null)
                return;

            var p = e.GetCurrentPoint(AxisGizmoCanvas).Position;
            var point = new Vector2((float)p.X, (float)p.Y);

            const float hitRadius = 16f;
            string? target = ResolveAxisClickTarget(point, hitRadius);
            if (string.IsNullOrWhiteSpace(target))
                return;

            _camera.SetView(target);
            StartAnimationLoop();
            e.Handled = true;
        }

        private string? ResolveAxisClickTarget(Vector2 point, float hitRadius)
        {
            float dX = Vector2.Distance(point, _axisXEnd);
            float dY = Vector2.Distance(point, _axisYEnd);
            float dZ = Vector2.Distance(point, _axisZEnd);

            float min = MathF.Min(dX, MathF.Min(dY, dZ));
            if (min > hitRadius)
                return null;

            if (min == dX)
            {
                return ToggleTarget(_camera.CurrentSnapName, positive: "left", negative: "right");
            }
            if (min == dY)
            {
                return ToggleTarget(_camera.CurrentSnapName, positive: "top", negative: "bottom");
            }

            return ToggleTarget(_camera.CurrentSnapName, positive: "front", negative: "back");
        }

        private static string ToggleTarget(string? currentSnap, string positive, string negative)
        {
            if (string.Equals(currentSnap, positive, StringComparison.OrdinalIgnoreCase))
                return negative;
            return positive;
        }
    }
}
