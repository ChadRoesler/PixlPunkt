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
        // EXPORT
        // ════════════════════════════════════════════════════════════════════

        private void SetExportButtonsEnabled(bool enabled)
        {
            if (ExportButton != null)
                ExportButton.IsEnabled = enabled;
            if (ExportModelButton != null)
                ExportModelButton.IsEnabled = enabled;
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0) return;

            try
            {
                var options = await PromptImageExportOptionsAsync();
                if (options is null)
                    return;

                var ownerWindow = App.PixlPunktMainWindow;
                if (ownerWindow == null)
                    return;

                if (options.Value.BatchExportViews)
                {
                    await ExportImageBatchAsync(ownerWindow, options.Value);
                    return;
                }

                if (!TryBuildImageExportBuffer(options.Value, out var exportBuffer, out int exportWidth, out int exportHeight))
                    return;

                var savePicker = WindowHost.CreateFileSavePicker(ownerWindow, "voxel_preview", ".png");
                savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                WindowHost.TrySetDefaultFileExtension(savePicker, ".png");

                var file = await savePicker.PickSaveFileAsync();
                if (file == null) return;

                if (!string.IsNullOrWhiteSpace(file.Path))
                {
                    await VoxelImageExporter.SaveBgraPngAsync(file.Path, exportWidth, exportHeight, exportBuffer, options.Value.TransparentBackground);
                }
                else
                {
                    using var outStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite);
                    var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                        Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, outStream);
                    encoder.SetPixelData(
                        Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                        options.Value.TransparentBackground
                            ? Windows.Graphics.Imaging.BitmapAlphaMode.Straight
                            : Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
                        (uint)exportWidth, (uint)exportHeight,
                        96, 96,
                        exportBuffer);
                    await encoder.FlushAsync();
                }

                LoggingService.Info("Voxel exported to {Path}", file.Path);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Voxel export failed", ex);
            }
        }

        private async void ExportModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount == 0)
                return;

            try
            {
                var options = await PromptModelExportOptionsAsync();
                if (options is null)
                    return;

                if (options.Value.Format == ModelExportFormat.Vox)
                {
                    var bounds = VoxelModelExporter.ComputeTransformedVoxelBounds(_lastVolume, options.Value.AxisPreset);
                    if (bounds.HasValue)
                    {
                        var b = bounds.Value;
                        if (b.SizeX > 255 || b.SizeY > 255 || b.SizeZ > 255)
                        {
                            var owner = App.PixlPunktMainWindow;
                            if (owner != null)
                            {
                                var warn = new ContentDialog
                                {
                                    XamlRoot = XamlRoot,
                                    Title = "VOX Export Limit",
                                    PrimaryButtonText = "OK",
                                    DefaultButton = ContentDialogButton.Primary,
                                    Content = $"VOX supports up to 255 units per axis. Current transformed size is {b.SizeX}x{b.SizeY}x{b.SizeZ}.",
                                };
                                await warn.ShowAsync();
                            }

                            return;
                        }
                    }
                }

                var ownerWindow = App.PixlPunktMainWindow;
                if (ownerWindow == null)
                    return;

                var extension = VoxelModelExporter.GetPrimaryExtension(options.Value.Format);
                var savePicker = WindowHost.CreateFileSavePicker(ownerWindow, "voxel_model", extension);
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                WindowHost.TrySetDefaultFileExtension(savePicker, extension);

                var outputFile = await savePicker.PickSaveFileAsync();
                if (outputFile == null)
                    return;

                var outputPath = outputFile.Path;
                if (string.IsNullOrWhiteSpace(outputPath))
                    return;

                var folder = System.IO.Path.GetDirectoryName(outputPath);
                if (string.IsNullOrWhiteSpace(folder))
                    return;

                var baseName = System.IO.Path.GetFileNameWithoutExtension(outputPath);
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = "voxel_model";

                switch (options.Value.Format)
                {
                    case ModelExportFormat.Glb:
                    {
                        var glbBytes = await VoxelModelExporter.BuildGlbExportAsync(_lastVolume, options.Value);
                        await System.IO.File.WriteAllBytesAsync(outputPath, glbBytes);
                        break;
                    }
                    case ModelExportFormat.Stl:
                    {
                        var stlBytes = VoxelModelExporter.BuildStlExport(_lastVolume, options.Value);
                        await System.IO.File.WriteAllBytesAsync(outputPath, stlBytes);
                        break;
                    }
                    case ModelExportFormat.Vox:
                    {
                        var voxBytes = VoxelModelExporter.BuildVoxExport(_lastVolume, options.Value);
                        await System.IO.File.WriteAllBytesAsync(outputPath, voxBytes);
                        break;
                    }
                    default:
                    {
                        // Keep external references OBJ/MTL-friendly (avoid spaces and odd tokens that some importers misread).
                        var refBaseName = VoxelModelExporter.SanitizeFileName(baseName).Replace(' ', '_');
                        if (string.IsNullOrWhiteSpace(refBaseName))
                            refBaseName = "voxel_model";

                        var mtlFileName = $"{refBaseName}.mtl";
                        var textureFileName = $"{refBaseName}.png";
                        var mtlPath = System.IO.Path.Combine(folder, mtlFileName);
                        var texturePath = System.IO.Path.Combine(folder, textureFileName);

                        var export = VoxelModelExporter.BuildObjExport(_lastVolume, mtlFileName, textureFileName, options.Value);

                        await System.IO.File.WriteAllTextAsync(outputPath, export.ObjText, Utf8NoBom);
                        await System.IO.File.WriteAllTextAsync(mtlPath, export.MtlText, Utf8NoBom);
                        await VoxelImageExporter.SaveBgraTextureToPngAsync(texturePath, export.TextureWidth, export.TextureHeight, export.TexturePixelsBgra);
                        break;
                    }
                }

                LoggingService.Info("Voxel model exported to {Path} as {Format}", outputPath, options.Value.Format);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Voxel model export failed", ex);
            }
        }

        private async Task ExportImageBatchAsync(Window ownerWindow, VoxelImageExportOptions options)
        {
            var folderPicker = WindowHost.CreateFolderPicker(ownerWindow, PickerLocationId.PicturesLibrary);
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder == null || string.IsNullOrWhiteSpace(folder.Path))
                return;

            var views = new List<string>(16);
            if (options.BatchIncludeCardinalViews)
                views.AddRange(BatchCardinalViewNames);
            if (options.BatchIncludeDirectionalViews)
                views.AddRange(BatchDirectionalViewNames);
            if (views.Count == 0)
                return;

            float oldPitch = _camera.Pitch;
            float oldYaw = _camera.Yaw;
            float oldZoom = _camera.ZoomPercent;
            string? oldSnap = _camera.CurrentSnapName;

            string rawBaseName = string.IsNullOrWhiteSpace(_document.Name) ? "voxel_preview" : _document.Name;
            string safeBaseName = VoxelModelExporter.SanitizeFileName(rawBaseName);

            try
            {
                foreach (var view in views)
                {
                    _camera.SetView(view, animated: false);
                    _camera.SetZoomPercent(oldZoom);

                    if (!TryBuildImageExportBuffer(options, out var exportBuffer, out int exportWidth, out int exportHeight))
                        continue;

                    string safeView = VoxelModelExporter.SanitizeFileName(view);
                    string fileName = $"{safeBaseName}_{safeView}.png";
                    string path = System.IO.Path.Combine(folder.Path, fileName);
                    await VoxelImageExporter.SaveBgraPngAsync(path, exportWidth, exportHeight, exportBuffer, options.TransparentBackground);
                }
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(oldSnap))
                    _camera.SetView(oldSnap, animated: false);
                else
                    _camera.SetOrientation(oldPitch, oldYaw, allowSnap: false);

                _camera.SetZoomPercent(oldZoom);
                RenderViewport();
            }
        }

        private bool TryBuildImageExportBuffer(
            VoxelImageExportOptions options,
            out byte[] exportBuffer,
            out int exportWidth,
            out int exportHeight)
        {
            exportBuffer = Array.Empty<byte>();
            exportWidth = 0;
            exportHeight = 0;

            byte[]? rendered = null;
            int renderedW = 0;
            int renderedH = 0;
            uint exportClearColor = options.TransparentBackground ? 0x00000000u : ClearColor;

            RenderViewport(
                new ViewportRenderOverrides
                {
                    DrawOutline = options.IncludeOutline,
                    DrawBackdropGrid = options.IncludeBackdropCage,
                    DrawBackdropProjectionTiles = options.IncludeProjectionTiles,
                    DrawSurfaceVoxelGrid = options.IncludeModelGrid,
                    IncludeSelectionOverlay = false,
                    ClearColor = exportClearColor,
                },
                presentOnViewport: false,
                renderedBufferSink: (buffer, w, h) =>
                {
                    renderedW = w;
                    renderedH = h;
                    rendered = new byte[w * h * 4];
                    Buffer.BlockCopy(buffer, 0, rendered, 0, rendered.Length);
                });

            if (rendered == null || renderedW <= 0 || renderedH <= 0)
                return false;

            int scale = Math.Max(1, options.Scale);
            if (scale == 1)
            {
                exportBuffer = rendered;
                exportWidth = renderedW;
                exportHeight = renderedH;
            }
            else
            {
                exportWidth = renderedW * scale;
                exportHeight = renderedH * scale;
                exportBuffer = new byte[exportWidth * exportHeight * 4];
                VoxelImageExporter.UpscaleNearestBgra(rendered, renderedW, renderedH, exportBuffer, exportWidth, exportHeight, scale);
            }

            if (options.TransparentBackground && options.TrimTransparentBounds)
            {
                VoxelImageExporter.TrimTransparentBoundsBgra(
                    exportBuffer,
                    exportWidth,
                    exportHeight,
                    Math.Clamp(options.TrimPadding, 0, 128),
                    out var trimmedBuffer,
                    out var trimmedWidth,
                    out var trimmedHeight);
                exportBuffer = trimmedBuffer;
                exportWidth = trimmedWidth;
                exportHeight = trimmedHeight;
            }

            return true;
        }

        private async Task<VoxelImageExportOptions?> PromptImageExportOptionsAsync()
        {
            var defaults = GetImageExportDefaultsFromLastOrUi();
            int defaultScale = Math.Clamp(defaults.Scale, 1, 64);
            bool useCustomScale = defaultScale != 1 && defaultScale != 2 && defaultScale != 4;

            var scaleModeCombo = new ComboBox
            {
                MinWidth = 170,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "1x", Tag = 1 });
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "2x", Tag = 2 });
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "4x", Tag = 4 });
            scaleModeCombo.Items.Add(new ComboBoxItem { Content = "Custom", Tag = 0 });
            scaleModeCombo.SelectedIndex = useCustomScale
                ? 3
                : (defaultScale == 2 ? 1 : (defaultScale == 4 ? 2 : 0));

            var customScaleBox = new NumberBox
            {
                Minimum = 1,
                Maximum = 64,
                Value = defaultScale,
                Width = 110,
                IsEnabled = useCustomScale,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            scaleModeCombo.SelectionChanged += (_, _) =>
            {
                bool custom = scaleModeCombo.SelectedIndex == 3;
                customScaleBox.IsEnabled = custom;
                if (!custom && scaleModeCombo.SelectedItem is ComboBoxItem item && item.Tag is int p && p > 0)
                {
                    customScaleBox.Value = p;
                }
            };

            var transparentBackgroundBox = new CheckBox
            {
                Content = "Transparent background",
                IsChecked = defaults.TransparentBackground,
            };

            var includeOutlineBox = new CheckBox
            {
                Content = "Include outline",
                IsChecked = defaults.IncludeOutline,
            };
            var includeCageBox = new CheckBox
            {
                Content = "Include backdrop cage",
                IsChecked = defaults.IncludeBackdropCage,
            };
            var includeProjectionTilesBox = new CheckBox
            {
                Content = "Include projection tiles",
                IsChecked = defaults.IncludeProjectionTiles,
            };
            var includeModelGridBox = new CheckBox
            {
                Content = "Include model voxel grid",
                IsChecked = defaults.IncludeModelGrid,
            };

            var trimTransparentBoundsBox = new CheckBox
            {
                Content = "Trim transparent bounds",
                IsChecked = defaults.TrimTransparentBounds,
                Margin = new Thickness(0, 4, 0, 0),
            };
            var trimPaddingRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(20, 0, 0, 0),
            };
            trimPaddingRow.Children.Add(new TextBlock { Text = "Trim padding", Opacity = 0.85, VerticalAlignment = VerticalAlignment.Center });
            var trimPaddingBox = new NumberBox
            {
                Width = 90,
                Minimum = 0,
                Maximum = 128,
                Value = defaults.TrimPadding,
            };
            trimPaddingRow.Children.Add(trimPaddingBox);

            var batchExportViewsBox = new CheckBox
            {
                Content = "Batch export views (pack)",
                IsChecked = defaults.BatchExportViews,
                Margin = new Thickness(0, 6, 0, 0),
            };
            var includeBatchCardinalViewsBox = new CheckBox
            {
                Content = "Include Front/Back/Left/Right/Top/Bottom",
                IsChecked = defaults.BatchIncludeCardinalViews,
                Margin = new Thickness(20, 0, 0, 0),
            };
            var includeBatchDirectionalViewsBox = new CheckBox
            {
                Content = "Include N/NE/E/SE/S/SW/W/NW",
                IsChecked = defaults.BatchIncludeDirectionalViews,
                Margin = new Thickness(20, 0, 0, 0),
            };

            void SyncImageExportDependentUi()
            {
                bool transparent = transparentBackgroundBox.IsChecked == true;
                trimTransparentBoundsBox.IsEnabled = transparent;
                bool trimEnabled = transparent && (trimTransparentBoundsBox.IsChecked == true);
                trimPaddingRow.IsHitTestVisible = trimEnabled;
                trimPaddingRow.Opacity = trimEnabled ? 1.0 : 0.55;

                bool batch = batchExportViewsBox.IsChecked == true;
                includeBatchCardinalViewsBox.IsEnabled = batch;
                includeBatchDirectionalViewsBox.IsEnabled = batch;
            }
            transparentBackgroundBox.Checked += (_, _) => SyncImageExportDependentUi();
            transparentBackgroundBox.Unchecked += (_, _) => SyncImageExportDependentUi();
            trimTransparentBoundsBox.Checked += (_, _) => SyncImageExportDependentUi();
            trimTransparentBoundsBox.Unchecked += (_, _) => SyncImageExportDependentUi();
            batchExportViewsBox.Checked += (_, _) => SyncImageExportDependentUi();
            batchExportViewsBox.Unchecked += (_, _) => SyncImageExportDependentUi();
            SyncImageExportDependentUi();

            VoxelImageExportOptions BuildCurrentImageOptionsFromControls()
            {
                int scale = 1;
                if (scaleModeCombo.SelectedItem is ComboBoxItem selectedScaleItem &&
                    selectedScaleItem.Tag is int presetScale &&
                    presetScale > 0)
                {
                    scale = presetScale;
                }
                else
                {
                    scale = (int)Math.Round(double.IsFinite(customScaleBox.Value) ? customScaleBox.Value : defaultScale);
                }

                return new VoxelImageExportOptions(
                    Scale: Math.Clamp(scale, 1, 64),
                    TransparentBackground: transparentBackgroundBox.IsChecked == true,
                    IncludeOutline: includeOutlineBox.IsChecked == true,
                    IncludeBackdropCage: includeCageBox.IsChecked == true,
                    IncludeProjectionTiles: includeProjectionTilesBox.IsChecked == true,
                    IncludeModelGrid: includeModelGridBox.IsChecked == true,
                    TrimTransparentBounds: trimTransparentBoundsBox.IsChecked == true,
                    TrimPadding: Math.Clamp((int)Math.Round(double.IsFinite(trimPaddingBox.Value) ? trimPaddingBox.Value : defaults.TrimPadding), 0, 128),
                    BatchExportViews: batchExportViewsBox.IsChecked == true,
                    BatchIncludeCardinalViews: includeBatchCardinalViewsBox.IsChecked != false,
                    BatchIncludeDirectionalViews: includeBatchDirectionalViewsBox.IsChecked != false);
            }

            void ApplyImageOptionsToControls(VoxelImageExportOptions loadedOptions)
            {
                int loadedScale = Math.Clamp(loadedOptions.Scale, 1, 64);
                bool loadedCustomScale = loadedScale != 1 && loadedScale != 2 && loadedScale != 4;
                scaleModeCombo.SelectedIndex = loadedCustomScale
                    ? 3
                    : (loadedScale == 2 ? 1 : (loadedScale == 4 ? 2 : 0));
                customScaleBox.Value = loadedScale;
                customScaleBox.IsEnabled = loadedCustomScale;

                transparentBackgroundBox.IsChecked = loadedOptions.TransparentBackground;
                includeOutlineBox.IsChecked = loadedOptions.IncludeOutline;
                includeCageBox.IsChecked = loadedOptions.IncludeBackdropCage;
                includeProjectionTilesBox.IsChecked = loadedOptions.IncludeProjectionTiles;
                includeModelGridBox.IsChecked = loadedOptions.IncludeModelGrid;
                trimTransparentBoundsBox.IsChecked = loadedOptions.TrimTransparentBounds;
                trimPaddingBox.Value = loadedOptions.TrimPadding;
                batchExportViewsBox.IsChecked = loadedOptions.BatchExportViews;
                includeBatchCardinalViewsBox.IsChecked = loadedOptions.BatchIncludeCardinalViews;
                includeBatchDirectionalViewsBox.IsChecked = loadedOptions.BatchIncludeDirectionalViews;
                SyncImageExportDependentUi();
            }

            var savePresetButton = new Button { Content = "Save Preset…" };
            savePresetButton.Click += async (_, _) =>
            {
                try
                {
                    var image = BuildCurrentImageOptionsFromControls();
                    var model = GetModelExportDefaultsFromLast();
                    await SaveExportPresetAsync(new VoxelExportPreset(image, model));
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to save voxel export preset", ex);
                }
            };

            var loadPresetButton = new Button { Content = "Load Preset…" };
            loadPresetButton.Click += async (_, _) =>
            {
                try
                {
                    var loadedPreset = await LoadExportPresetAsync();
                    if (loadedPreset is null)
                        return;

                    ApplyImageOptionsToControls(loadedPreset.Value.Image);
                    ApplyModelExportOptionDefaults(loadedPreset.Value.Model);
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to load voxel export preset", ex);
                }
            };

            var layout = new StackPanel { Spacing = 10 };
            layout.Children.Add(new TextBlock { Text = "Scale", Opacity = 0.85 });
            var scaleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            scaleRow.Children.Add(scaleModeCombo);
            scaleRow.Children.Add(customScaleBox);
            layout.Children.Add(scaleRow);
            var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            presetRow.Children.Add(savePresetButton);
            presetRow.Children.Add(loadPresetButton);
            layout.Children.Add(presetRow);
            layout.Children.Add(transparentBackgroundBox);
            layout.Children.Add(includeOutlineBox);
            layout.Children.Add(includeCageBox);
            layout.Children.Add(includeProjectionTilesBox);
            layout.Children.Add(includeModelGridBox);
            layout.Children.Add(trimTransparentBoundsBox);
            layout.Children.Add(trimPaddingRow);
            layout.Children.Add(batchExportViewsBox);
            layout.Children.Add(includeBatchCardinalViewsBox);
            layout.Children.Add(includeBatchDirectionalViewsBox);

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Export Image",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = layout,
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return null;

            var chosen = BuildCurrentImageOptionsFromControls();
            ApplyImageExportOptionDefaults(chosen);
            return chosen;
        }

        private async Task<VoxelModelExportOptions?> PromptModelExportOptionsAsync()
        {
            var formatCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "OBJ (.obj + .mtl + .png)",
                Tag = ModelExportFormat.Obj,
            });
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "glTF Binary (.glb)",
                Tag = ModelExportFormat.Glb,
            });
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "STL Binary (.stl)",
                Tag = ModelExportFormat.Stl,
            });
            formatCombo.Items.Add(new ComboBoxItem
            {
                Content = "MagicaVoxel (.vox)",
                Tag = ModelExportFormat.Vox,
            });
            formatCombo.SelectedIndex = _lastModelExportFormat switch
            {
                ModelExportFormat.Glb => 1,
                ModelExportFormat.Stl => 2,
                ModelExportFormat.Vox => 3,
                _ => 0,
            };

            var meshModeCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            meshModeCombo.Items.Add(new ComboBoxItem
            {
                Content = "Merge coplanar faces",
                Tag = ModelMeshMode.MergeCoplanar,
            });
            meshModeCombo.Items.Add(new ComboBoxItem
            {
                Content = "Keep per-voxel faces",
                Tag = ModelMeshMode.PerVoxel,
            });
            meshModeCombo.SelectedIndex = _lastModelExportMeshMode == ModelMeshMode.PerVoxel ? 1 : 0;

            var axisPresetCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            axisPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "PixlPunkt (Y-up)",
                Tag = ModelAxisPreset.PixlPunkt,
            });
            axisPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Blender (Z-up)",
                Tag = ModelAxisPreset.BlenderZUp,
            });
            axisPresetCombo.SelectedIndex = _lastModelExportAxisPreset == ModelAxisPreset.BlenderZUp ? 1 : 0;

            var pivotPresetCombo = new ComboBox
            {
                MinWidth = 230,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            pivotPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Center",
                Tag = ModelPivotPreset.Center,
            });
            pivotPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Bottom Center",
                Tag = ModelPivotPreset.BottomCenter,
            });
            pivotPresetCombo.Items.Add(new ComboBoxItem
            {
                Content = "Origin",
                Tag = ModelPivotPreset.Origin,
            });
            pivotPresetCombo.SelectedIndex = _lastModelExportPivotPreset switch
            {
                ModelPivotPreset.BottomCenter => 1,
                ModelPivotPreset.Origin => 2,
                _ => 0,
            };

            var unitScaleBox = new NumberBox
            {
                MinWidth = 120,
                Maximum = 10000,
                Minimum = 0.0001,
                Value = _lastModelExportUnitScale <= 0f ? 1f : _lastModelExportUnitScale,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            var glbDoubleSidedBox = new CheckBox
            {
                Content = "GLB: Double-sided material",
                IsChecked = _lastModelExportGlbDoubleSided,
            };

            var voxWarningText = new TextBlock
            {
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Goldenrod),
                Opacity = 0.95,
                Visibility = Visibility.Collapsed,
            };

            VoxelModelExportOptions BuildCurrentModelOptionsFromControls()
            {
                var format = formatCombo.SelectedItem is ComboBoxItem formatItem && formatItem.Tag is ModelExportFormat formatValue
                    ? formatValue
                    : ModelExportFormat.Obj;
                var meshMode = meshModeCombo.SelectedItem is ComboBoxItem meshItem && meshItem.Tag is ModelMeshMode meshValue
                    ? meshValue
                    : ModelMeshMode.MergeCoplanar;
                var axisPreset = axisPresetCombo.SelectedItem is ComboBoxItem axisItem && axisItem.Tag is ModelAxisPreset axisValue
                    ? axisValue
                    : ModelAxisPreset.PixlPunkt;
                var pivotPreset = pivotPresetCombo.SelectedItem is ComboBoxItem pivotItem && pivotItem.Tag is ModelPivotPreset pivotValue
                    ? pivotValue
                    : ModelPivotPreset.Center;
                float unitScale = (float)(double.IsFinite(unitScaleBox.Value) ? unitScaleBox.Value : 1d);
                unitScale = Math.Clamp(unitScale, 0.0001f, 10000f);
                bool glbDoubleSided = glbDoubleSidedBox.IsChecked != false;
                return new VoxelModelExportOptions(format, meshMode, axisPreset, unitScale, pivotPreset, glbDoubleSided);
            }

            void ApplyModelOptionsToControls(VoxelModelExportOptions loaded)
            {
                formatCombo.SelectedIndex = loaded.Format switch
                {
                    ModelExportFormat.Glb => 1,
                    ModelExportFormat.Stl => 2,
                    ModelExportFormat.Vox => 3,
                    _ => 0,
                };
                meshModeCombo.SelectedIndex = loaded.MeshMode == ModelMeshMode.PerVoxel ? 1 : 0;
                axisPresetCombo.SelectedIndex = loaded.AxisPreset == ModelAxisPreset.BlenderZUp ? 1 : 0;
                pivotPresetCombo.SelectedIndex = loaded.PivotPreset switch
                {
                    ModelPivotPreset.BottomCenter => 1,
                    ModelPivotPreset.Origin => 2,
                    _ => 0,
                };
                unitScaleBox.Value = Math.Clamp(loaded.UnitScale, 0.0001f, 10000f);
                glbDoubleSidedBox.IsChecked = loaded.GlbDoubleSided;
            }

            void RefreshFormatSpecificControls()
            {
                var selectedFormat = formatCombo.SelectedItem is ComboBoxItem selectedItem &&
                                     selectedItem.Tag is ModelExportFormat selectedValue
                    ? selectedValue
                    : ModelExportFormat.Obj;

                glbDoubleSidedBox.Visibility = selectedFormat == ModelExportFormat.Glb
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (selectedFormat != ModelExportFormat.Vox)
                {
                    voxWarningText.Visibility = Visibility.Collapsed;
                    return;
                }

                if (_lastVolume == null || _lastVolume.OccupiedCount <= 0)
                {
                    voxWarningText.Text = "VOX exports palette-indexed voxel colors only.";
                    voxWarningText.Visibility = Visibility.Visible;
                    return;
                }

                var currentOptions = BuildCurrentModelOptionsFromControls();
                var bounds = VoxelModelExporter.ComputeTransformedVoxelBounds(_lastVolume, currentOptions.AxisPreset);
                if (!bounds.HasValue)
                {
                    voxWarningText.Text = "VOX exports palette-indexed voxel colors only.";
                    voxWarningText.Visibility = Visibility.Visible;
                    return;
                }

                var b = bounds.Value;
                bool outOfRange = b.SizeX > 255 || b.SizeY > 255 || b.SizeZ > 255;
                voxWarningText.Text = outOfRange
                    ? $"Warning: VOX supports <=255 units per axis. Current transformed size is {b.SizeX}x{b.SizeY}x{b.SizeZ}."
                    : $"VOX note: size {b.SizeX}x{b.SizeY}x{b.SizeZ}, 255 max per axis, pivot/unit scale are ignored.";
                voxWarningText.Visibility = Visibility.Visible;
            }

            var savePresetButton = new Button { Content = "Save Preset…" };
            savePresetButton.Click += async (_, _) =>
            {
                try
                {
                    var model = BuildCurrentModelOptionsFromControls();
                    var image = GetImageExportDefaultsFromLastOrUi();
                    await SaveExportPresetAsync(new VoxelExportPreset(image, model));
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to save voxel export preset", ex);
                }
            };

            var loadPresetButton = new Button { Content = "Load Preset…" };
            loadPresetButton.Click += async (_, _) =>
            {
                try
                {
                    var loadedPreset = await LoadExportPresetAsync();
                    if (loadedPreset is null)
                        return;

                    ApplyModelOptionsToControls(loadedPreset.Value.Model);
                    ApplyImageExportOptionDefaults(loadedPreset.Value.Image);
                    RefreshFormatSpecificControls();
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to load voxel export preset", ex);
                }
            };

            var layout = new StackPanel { Spacing = 10 };
            var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            presetRow.Children.Add(savePresetButton);
            presetRow.Children.Add(loadPresetButton);
            layout.Children.Add(presetRow);
            layout.Children.Add(new TextBlock { Text = "Format", Opacity = 0.85 });
            layout.Children.Add(formatCombo);
            layout.Children.Add(new TextBlock { Text = "Mesh", Opacity = 0.85 });
            layout.Children.Add(meshModeCombo);
            layout.Children.Add(new TextBlock { Text = "Axis Preset", Opacity = 0.85 });
            layout.Children.Add(axisPresetCombo);
            layout.Children.Add(new TextBlock { Text = "Pivot", Opacity = 0.85 });
            layout.Children.Add(pivotPresetCombo);
            layout.Children.Add(new TextBlock { Text = "Unit Scale", Opacity = 0.85 });
            layout.Children.Add(unitScaleBox);
            layout.Children.Add(glbDoubleSidedBox);
            layout.Children.Add(voxWarningText);

            formatCombo.SelectionChanged += (_, _) => RefreshFormatSpecificControls();
            axisPresetCombo.SelectionChanged += (_, _) => RefreshFormatSpecificControls();
            RefreshFormatSpecificControls();

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Export Model",
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                Content = layout,
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return null;

            var chosen = BuildCurrentModelOptionsFromControls();
            ApplyModelExportOptionDefaults(chosen);
            return chosen;
        }

        private VoxelImageExportOptions GetImageExportDefaultsFromLastOrUi()
            => new(
                Scale: Math.Clamp(_lastImageExportScale, 1, 64),
                TransparentBackground: _lastImageExportTransparentBackground,
                IncludeOutline: _lastImageExportIncludeOutline,
                IncludeBackdropCage: _lastImageExportIncludeBackdropCage,
                IncludeProjectionTiles: _lastImageExportIncludeProjectionTiles,
                IncludeModelGrid: _lastImageExportIncludeModelGrid,
                TrimTransparentBounds: _lastImageExportTrimTransparentBounds,
                TrimPadding: Math.Clamp(_lastImageExportTrimPadding, 0, 128),
                BatchExportViews: _lastImageExportBatchViews,
                BatchIncludeCardinalViews: _lastImageExportBatchCardinalViews,
                BatchIncludeDirectionalViews: _lastImageExportBatchDirectionalViews);

        private VoxelModelExportOptions GetModelExportDefaultsFromLast()
            => new(
                Format: _lastModelExportFormat,
                MeshMode: _lastModelExportMeshMode,
                AxisPreset: _lastModelExportAxisPreset,
                UnitScale: _lastModelExportUnitScale <= 0f ? 1f : _lastModelExportUnitScale,
                PivotPreset: _lastModelExportPivotPreset,
                GlbDoubleSided: _lastModelExportGlbDoubleSided);

        private void ApplyImageExportOptionDefaults(VoxelImageExportOptions options)
        {
            _lastImageExportScale = Math.Clamp(options.Scale, 1, 64);
            _lastImageExportTransparentBackground = options.TransparentBackground;
            _lastImageExportIncludeOutline = options.IncludeOutline;
            _lastImageExportIncludeBackdropCage = options.IncludeBackdropCage;
            _lastImageExportIncludeProjectionTiles = options.IncludeProjectionTiles;
            _lastImageExportIncludeModelGrid = options.IncludeModelGrid;
            _lastImageExportTrimTransparentBounds = options.TrimTransparentBounds;
            _lastImageExportTrimPadding = Math.Clamp(options.TrimPadding, 0, 128);
            _lastImageExportBatchViews = options.BatchExportViews;
            _lastImageExportBatchCardinalViews = options.BatchIncludeCardinalViews;
            _lastImageExportBatchDirectionalViews = options.BatchIncludeDirectionalViews;
        }

        private void ApplyModelExportOptionDefaults(VoxelModelExportOptions options)
        {
            _lastModelExportFormat = options.Format;
            _lastModelExportMeshMode = options.MeshMode;
            _lastModelExportAxisPreset = options.AxisPreset;
            _lastModelExportUnitScale = Math.Clamp(options.UnitScale, 0.0001f, 10000f);
            _lastModelExportPivotPreset = options.PivotPreset;
            _lastModelExportGlbDoubleSided = options.GlbDoubleSided;
        }

        private async Task SaveExportPresetAsync(VoxelExportPreset preset)
        {
            var ownerWindow = App.PixlPunktMainWindow;
            if (ownerWindow == null)
                return;

            var picker = WindowHost.CreateFileSavePicker(ownerWindow, "voxel_export_preset", ".pxvexpreset", ".json");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            WindowHost.TrySetDefaultFileExtension(picker, ".pxvexpreset");
            var file = await picker.PickSaveFileAsync();
            if (file == null)
                return;

            var payload = new VoxelExportPresetFile
            {
                Version = 1,
                Image = new VoxelImageExportPresetFile
                {
                    Scale = preset.Image.Scale,
                    TransparentBackground = preset.Image.TransparentBackground,
                    IncludeOutline = preset.Image.IncludeOutline,
                    IncludeBackdropCage = preset.Image.IncludeBackdropCage,
                    IncludeProjectionTiles = preset.Image.IncludeProjectionTiles,
                    IncludeModelGrid = preset.Image.IncludeModelGrid,
                    TrimTransparentBounds = preset.Image.TrimTransparentBounds,
                    TrimPadding = preset.Image.TrimPadding,
                    BatchExportViews = preset.Image.BatchExportViews,
                    BatchIncludeCardinalViews = preset.Image.BatchIncludeCardinalViews,
                    BatchIncludeDirectionalViews = preset.Image.BatchIncludeDirectionalViews,
                },
                Model = new VoxelModelExportPresetFile
                {
                    Format = (int)preset.Model.Format,
                    MeshMode = (int)preset.Model.MeshMode,
                    AxisPreset = (int)preset.Model.AxisPreset,
                    UnitScale = preset.Model.UnitScale,
                    PivotPreset = (int)preset.Model.PivotPreset,
                    GlbDoubleSided = preset.Model.GlbDoubleSided,
                },
            };

            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await Windows.Storage.FileIO.WriteTextAsync(file, json);
        }

        private async Task<VoxelExportPreset?> LoadExportPresetAsync()
        {
            var ownerWindow = App.PixlPunktMainWindow;
            if (ownerWindow == null)
                return null;

            var picker = WindowHost.CreateFileOpenPicker(ownerWindow, ".pxvexpreset", ".json");
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            var file = await picker.PickSingleFileAsync();
            if (file == null)
                return null;

            string json = await Windows.Storage.FileIO.ReadTextAsync(file);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            VoxelExportPresetFile? payload = JsonSerializer.Deserialize<VoxelExportPresetFile>(json);
            if (payload == null)
                return null;

            var fallbackImage = GetImageExportDefaultsFromLastOrUi();
            var fallbackModel = GetModelExportDefaultsFromLast();

            var loadedImage = payload.Image != null
                ? new VoxelImageExportOptions(
                    Scale: Math.Clamp(payload.Image.Scale, 1, 64),
                    TransparentBackground: payload.Image.TransparentBackground,
                    IncludeOutline: payload.Image.IncludeOutline,
                    IncludeBackdropCage: payload.Image.IncludeBackdropCage,
                    IncludeProjectionTiles: payload.Image.IncludeProjectionTiles,
                    IncludeModelGrid: payload.Image.IncludeModelGrid,
                    TrimTransparentBounds: payload.Image.TrimTransparentBounds,
                    TrimPadding: Math.Clamp(payload.Image.TrimPadding, 0, 128),
                    BatchExportViews: payload.Image.BatchExportViews,
                    BatchIncludeCardinalViews: payload.Image.BatchIncludeCardinalViews,
                    BatchIncludeDirectionalViews: payload.Image.BatchIncludeDirectionalViews)
                : fallbackImage;

            var loadedModel = payload.Model != null
                ? new VoxelModelExportOptions(
                    Format: Enum.IsDefined(typeof(ModelExportFormat), payload.Model.Format)
                        ? (ModelExportFormat)payload.Model.Format
                        : fallbackModel.Format,
                    MeshMode: Enum.IsDefined(typeof(ModelMeshMode), payload.Model.MeshMode)
                        ? (ModelMeshMode)payload.Model.MeshMode
                        : fallbackModel.MeshMode,
                    AxisPreset: Enum.IsDefined(typeof(ModelAxisPreset), payload.Model.AxisPreset)
                        ? (ModelAxisPreset)payload.Model.AxisPreset
                        : fallbackModel.AxisPreset,
                    UnitScale: Math.Clamp(payload.Model.UnitScale, 0.0001f, 10000f),
                    PivotPreset: Enum.IsDefined(typeof(ModelPivotPreset), payload.Model.PivotPreset)
                        ? (ModelPivotPreset)payload.Model.PivotPreset
                        : fallbackModel.PivotPreset,
                    GlbDoubleSided: payload.Model.GlbDoubleSided)
                : fallbackModel;

            return new VoxelExportPreset(loadedImage, loadedModel);
        }
    }
}
