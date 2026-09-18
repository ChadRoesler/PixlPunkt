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
        // LIGHTING CONTROLS
        // ════════════════════════════════════════════════════════════════════

        private void LightingControls_Changed(object sender, RoutedEventArgs e)
            => ApplyLightingFromControls();

        private void LightingColor_Changed(object sender, uint e)
            => ApplyLightingFromControls();

        private void LightingShadowColor_Changed(object sender, uint e)
            => ApplyLightingFromControls();

        private void LightingShadowStrengthSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressLightingUiSync) return;
            if (LightingShadowStrengthBox != null && !NearlyEqual(LightingShadowStrengthBox.Value, e.NewValue))
            {
                _suppressLightingUiSync = true;
                LightingShadowStrengthBox.Value = e.NewValue;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingShadowStrengthBox_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressLightingUiSync) return;
            if (LightingShadowStrengthSlider != null && !NearlyEqual(LightingShadowStrengthSlider.Value, sender.Value))
            {
                _suppressLightingUiSync = true;
                LightingShadowStrengthSlider.Value = sender.Value;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingIntensitySlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressLightingUiSync) return;
            if (LightingIntensityBox != null && !NearlyEqual(LightingIntensityBox.Value, e.NewValue))
            {
                _suppressLightingUiSync = true;
                LightingIntensityBox.Value = e.NewValue;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingIntensityBox_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressLightingUiSync) return;
            if (LightingIntensitySlider != null && !NearlyEqual(LightingIntensitySlider.Value, sender.Value))
            {
                _suppressLightingUiSync = true;
                LightingIntensitySlider.Value = sender.Value;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingFalloffSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_suppressLightingUiSync) return;
            if (LightingFalloffBox != null && !NearlyEqual(LightingFalloffBox.Value, e.NewValue))
            {
                _suppressLightingUiSync = true;
                LightingFalloffBox.Value = e.NewValue;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingFalloffBox_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressLightingUiSync) return;
            if (LightingFalloffSlider != null && !NearlyEqual(LightingFalloffSlider.Value, sender.Value))
            {
                _suppressLightingUiSync = true;
                LightingFalloffSlider.Value = sender.Value;
                _suppressLightingUiSync = false;
            }
            ApplyLightingFromControls();
        }

        private void LightingPosition_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
            => ApplyLightingFromControls();

        private void UpdateVoxelSelectionStatusText(string? custom = null)
        {
            if (VoxelSelectionStatusText == null)
                return;

            if (!string.IsNullOrWhiteSpace(custom))
            {
                VoxelSelectionStatusText.Text = custom!;
                return;
            }

            int count = _editEngine.Selection.Count;
            string activeTool = _voxelToolState.ActiveRegistration?.DisplayName ?? "None";
            VoxelSelectionStatusText.Text =
                $"Selection: {count} voxel{(count == 1 ? "" : "s")}. Active Tool: {activeTool}. " +
                "RMB drag orbits. Use move buttons for nudges.";
        }

        private void ClearVoxelSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editEngine.ClearSelection())
            {
                UpdateVoxelSelectionStatusText("Cleared voxel selection.");
            }
            else
            {
                UpdateVoxelSelectionStatusText();
            }
        }

        private void ExpandVoxelSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editEngine.ExpandSelectionConnected())
            {
                UpdateVoxelSelectionStatusText("Expanded selection to connected voxels.");
            }
            else
            {
                UpdateVoxelSelectionStatusText("Expand selection: no connected voxels to expand.");
            }
        }

        private void MoveSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string tag })
                return;

            var parts = tag.Split(',');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out int dx) ||
                !int.TryParse(parts[1], out int dy) ||
                !int.TryParse(parts[2], out int dz))
            {
                return;
            }

            if (_editEngine.MoveSelection(new Int3(dx, dy, dz)))
            {
                UpdateVoxelSelectionStatusText($"Moved selection by ({dx},{dy},{dz}).");
            }
            else
            {
                UpdateVoxelSelectionStatusText($"Move blocked for delta ({dx},{dy},{dz}).");
            }
        }

        /// <summary>True when the next undo step on the document's history is a voxel edit.</summary>
        public bool CanUndoVoxelEdits => _editEngine.History.CanUndoVoxel;

        /// <summary>True when the next redo step on the document's history is a voxel edit.</summary>
        public bool CanRedoVoxelEdits => _editEngine.History.CanRedoVoxel;

        /// <summary>
        /// Undoes the top history item if it is a voxel edit. Returns false when it is a canvas
        /// item, which the caller should route to the canvas host instead.
        /// </summary>
        public bool TryUndoVoxelEdit()
        {
            FlushPendingLightingHistory();
            if (!_editEngine.Undo())
                return false;

            UpdateVoxelSelectionStatusText("Undo voxel edit.");
            return true;
        }

        public bool TryRedoVoxelEdit()
        {
            FlushPendingLightingHistory();
            if (!_editEngine.Redo())
                return false;

            UpdateVoxelSelectionStatusText("Redo voxel edit.");
            return true;
        }

        public bool IsWorkspaceFocused()
        {
            if (XamlRoot == null)
                return false;

            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (focused == null)
                return false;

            return IsDescendantOf(this, focused);
        }

        private bool IsViewportFocused()
        {
            if (ViewportHost == null || XamlRoot == null)
                return false;

            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (focused == null)
                return false;

            return IsDescendantOf(ViewportHost, focused);
        }

        private static bool IsDescendantOf(DependencyObject ancestor, DependencyObject? element)
        {
            var cur = element;
            while (cur != null)
            {
                if (ReferenceEquals(cur, ancestor))
                    return true;
                cur = VisualTreeHelper.GetParent(cur);
            }

            return false;
        }

        private bool TryPickFaceFromScreen(float screenX, float screenY, out VoxelFaceHit hit)
        {
            hit = default;
            if (!TryPickVoxelFaceAtHostPoint(new Windows.Foundation.Point(screenX, screenY), out var picked) || _lastVolume == null)
                return false;

            float half = _lastVolume.Size * 0.5f;
            var center = new Vector3(
                picked.X + 0.5f - half,
                picked.Y + 0.5f - half,
                picked.Z + 0.5f - half);
            var pose = _camera.GetCameraPose();
            float distance = Vector3.Distance(pose.Position, center);
            uint color = PackedBgraFromRgba(_lastVolume.GetFaceColor(picked.X, picked.Y, picked.Z, picked.Face));

            hit = new VoxelFaceHit(
                new Int3(picked.X, picked.Y, picked.Z),
                ToVoxelFace(picked.Face),
                distance,
                color);
            return true;
        }

        private bool TryPickVoxelFromScreen(float screenX, float screenY, out VoxelVoxelHit hit)
        {
            hit = default;
            if (!TryPickVoxelFaceAtHostPoint(new Windows.Foundation.Point(screenX, screenY), out var picked) || _lastVolume == null)
                return false;

            float half = _lastVolume.Size * 0.5f;
            var center = new Vector3(
                picked.X + 0.5f - half,
                picked.Y + 0.5f - half,
                picked.Z + 0.5f - half);
            var pose = _camera.GetCameraPose();
            float distance = Vector3.Distance(pose.Position, center);

            hit = new VoxelVoxelHit(
                new Int3(picked.X, picked.Y, picked.Z),
                distance,
                ToVoxelFace(picked.Face));
            return true;
        }

        private void SetFaceColorFromTool(int x, int y, int z, VoxelFace face, uint bgra)
        {
            var coreFace = ToCoreFace(face);
            var state = _document.VoxelPreviewState;
            bool hadOverrideBefore = state.TryGetFaceColorOverride(x, y, z, coreFace, out uint beforeOverrideColor);
            bool openedTransaction = !_editEngine.History.IsTransactionOpen;

            if (openedTransaction)
                _editEngine.BeginHistoryTransaction("Voxel Face Color");

            try
            {
                if (!_editEngine.SetFaceColor(x, y, z, coreFace, bgra))
                {
                    if (openedTransaction)
                        _editEngine.CancelHistoryTransaction();
                    return;
                }

                state.HasState = true;
                state.SetFaceColorOverride(x, y, z, coreFace, bgra);

                _editEngine.History.Push(new VoxelCommandHistory.DelegateCommand(
                    "Voxel Face Override",
                    undo: () =>
                    {
                        if (hadOverrideBefore)
                            state.SetFaceColorOverride(x, y, z, coreFace, beforeOverrideColor);
                        else
                            state.RemoveFaceColorOverride(x, y, z, coreFace);

                        PersistVoxelPreviewStateToDocument();
                    },
                    redo: () =>
                    {
                        state.SetFaceColorOverride(x, y, z, coreFace, bgra);
                        PersistVoxelPreviewStateToDocument();
                    }));

                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
                PersistVoxelPreviewStateToDocument();
                if (openedTransaction)
                    _editEngine.CommitHistoryTransaction();
            }
            catch
            {
                if (openedTransaction)
                    _editEngine.CancelHistoryTransaction();
                throw;
            }
        }

        private void ClearFaceColorOverrideFromTool(int x, int y, int z, VoxelFace face)
        {
            var coreFace = ToCoreFace(face);
            var state = _document.VoxelPreviewState;
            if (!state.TryGetFaceColorOverride(x, y, z, coreFace, out _))
                return;

            var before = CaptureWorkspaceHistorySnapshot();
            ClearManualFaceColorOverride(x, y, z, coreFace);
            var after = CaptureWorkspaceHistorySnapshot();

            _editEngine.History.Push(new VoxelCommandHistory.DelegateCommand(
                "Erase Face Override",
                undo: () => RestoreWorkspaceHistorySnapshot(before),
                redo: () => RestoreWorkspaceHistorySnapshot(after)));
        }

        private void SetVoxelFromTool(int x, int y, int z, uint colorBgra)
        {
            if (_editEngine.CreateVoxel(x, y, z, colorBgra))
            {
                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
            }
        }

        private void ClearVoxelFromTool(int x, int y, int z)
        {
            if (_editEngine.DeleteVoxel(x, y, z))
            {
                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
            }
        }

        private void MoveSelectionFromTool(Int3 delta, VoxelMoveMode mode)
        {
            if (_editEngine.MoveSelection(delta, mode))
            {
                _document.VoxelModel.SourceKind = VoxelModelSourceKind.Hybrid;
            }
        }

        private void ClearSelectionFromTool()
            => _editEngine.ClearSelection();

        private void SetSelectionFromTool(IEnumerable<Int3> voxels, VoxelSelectionMode mode)
            => _editEngine.SetSelection(voxels, mode);

        private void ExpandSelectionFromTool()
            => _editEngine.ExpandSelectionConnected();

        private VoxelViewportState BuildViewportState()
            => new(
                (int)MathF.Round(MathF.Max(1f, _camera.ViewportWidth)),
                (int)MathF.Round(MathF.Max(1f, _camera.ViewportHeight)),
                _camera.Pitch,
                _camera.Yaw,
                _camera.ZoomPercent,
                _camera.CurrentSnapName,
                PixelPreviewCheckBox?.IsChecked == true);

        private VoxelLightingSettings GetLightingSettingsSnapshot()
        {
            var ws = _document.VoxelWorkspace;
            return new VoxelLightingSettings
            {
                Enabled = ws.LightingEnabled,
                Position = new Vector3(ws.LightPosX, ws.LightPosY, ws.LightPosZ),
                LightColorBgra = ws.LightColorBgra,
                ShadowColorBgra = ws.ShadowColorBgra,
                ShadowStrength = ws.LightShadowStrength,
                Intensity = ws.LightIntensity,
                Falloff = ws.LightFalloff,
                CastShadows = ws.LightCastShadows,
            };
        }

        private void EnsureRecommendedLightingDefaultsForCurrentVolume()
        {
            if (_lastVolume == null || _lastVolume.OccupiedCount <= 0)
                return;

            int size = Math.Max(1, _lastVolume.Size);
            if (_lastAutoLightingPresetVolumeSize == size)
                return;

            var ws = _document.VoxelWorkspace;
            if (!IsLegacyLightingDefaults(ws))
            {
                _lastAutoLightingPresetVolumeSize = size;
                return;
            }

            float scale = MathF.Max(1f, size);
            ws.LightPosX = scale * 1.15f;
            ws.LightPosY = scale * 1.55f;
            ws.LightPosZ = scale * 1.15f;
            ws.LightIntensity = 1.1f;
            ws.LightFalloff = 0.75f / scale;
            ws.LightColorBgra = 0xFFFFFFFF;
            ws.ShadowColorBgra = 0xC0000000;
            ws.LightShadowStrength = 1f;

            _lastAutoLightingPresetVolumeSize = size;
            SyncLightingControlsFromDocument();
        }

        private static bool IsLegacyLightingDefaults(VoxelWorkspaceDocumentState ws)
        {
            return NearlyEqual(ws.LightPosX, 32f) &&
                   NearlyEqual(ws.LightPosY, 48f) &&
                   NearlyEqual(ws.LightPosZ, 32f) &&
                   ws.LightColorBgra == 0xFFFFFFFF &&
                   ws.ShadowColorBgra == 0xC0000000 &&
                   NearlyEqual(ws.LightShadowStrength, 1f) &&
                   NearlyEqual(ws.LightIntensity, 1f) &&
                   NearlyEqual(ws.LightFalloff, 0.05f);
        }

        private void UpdateLightingSettingsFromTool(Action<VoxelLightingSettings> edit)
        {
            if (edit == null)
                return;

            var before = GetLightingSettingsSnapshot();
            var next = CloneLightingSettings(before);
            edit(next);
            if (!IsLightingSettingsEqual(before, next))
            {
                ApplyLightingSnapshotToWorkspace(next, before);
                SyncLightingControlsFromDocument();
            }
        }

        private static Face ToCoreFace(VoxelFace face)
            => face switch
            {
                VoxelFace.Front => Face.Front,
                VoxelFace.Back => Face.Back,
                VoxelFace.Left => Face.Left,
                VoxelFace.Right => Face.Right,
                VoxelFace.Top => Face.Top,
                VoxelFace.Bottom => Face.Bottom,
                _ => Face.Front,
            };

        private static VoxelFace ToVoxelFace(Face face)
            => face switch
            {
                Face.Front => VoxelFace.Front,
                Face.Back => VoxelFace.Back,
                Face.Left => VoxelFace.Left,
                Face.Right => VoxelFace.Right,
                Face.Top => VoxelFace.Top,
                Face.Bottom => VoxelFace.Bottom,
                _ => VoxelFace.Front,
            };
    }
}
