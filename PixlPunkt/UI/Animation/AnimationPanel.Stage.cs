using System;
using System.Collections.Generic;
using System.Linq;
using FluentIcons.Common;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tools;
using Windows.Foundation;
using Windows.UI;

namespace PixlPunkt.UI.Animation
{
    public partial class AnimationPanel
    {

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when the Stage track is selected or deselected.
        /// Parameter is true when selected, false when deselected.
        /// </summary>
        public event Action<bool>? StageSelectionChanged;

        /// <summary>
        /// Gets whether the Stage track is currently selected.
        /// </summary>
        public bool IsStageSelected { get; private set; }

        /// <summary>
        /// Refreshes the stage preview to reflect canvas changes.
        /// Call this after painting or other canvas modifications.
        /// </summary>
        public void RefreshStagePreview()
        {
            if (CurrentMode == AnimationMode.Canvas)
            {
                StagePreview.RefreshPreview();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // EVENT HANDLERS - STAGE PREVIEW
        // ═══════════════════════════════════════════════════════════════

        private void OnStagePreviewExpandRequested(bool expand)
        {
            // Toggle between normal (200) and expanded (350) width
            StagePreviewColumn.Width = expand
                ? new GridLength(350)
                : new GridLength(200);
        }

        private void StagePreviewUndock_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimationPreviewFloating)
            {
                AnimationPreviewDockRequested?.Invoke();
            }
            else
            {
                AnimationPreviewUndockRequested?.Invoke();
            }
        }

        private void UpdateAnimationPreviewUndockButton()
        {
            if (StagePreviewUndockIcon != null)
            {
                StagePreviewUndockIcon.Icon = _isAnimationPreviewFloating
                    ? FluentIcons.Common.Icon.PanelRight
                    : FluentIcons.Common.Icon.Open;
            }

            if (StagePreviewUndockButton != null)
            {
                ToolTipService.SetToolTip(StagePreviewUndockButton,
                    _isAnimationPreviewFloating
                        ? "Dock back to animation panel"
                        : "Undock to separate window");
            }
        }

        /// <summary>
        /// Gets the StagePreviewContainer grid for undocking purposes.
        /// Returns null if the container doesn't exist.
        /// </summary>
        public Grid? GetStagePreviewContainer()
        {
            // Use FindName to be safe in case XAML isn't fully loaded
            return FindName("StagePreviewContainer") as Grid ?? StagePreviewContainer;
        }

        /// <summary>
        /// Gets the stage preview column definition for hiding when undocked.
        /// </summary>
        public ColumnDefinition GetStagePreviewColumn() => StagePreviewColumn;

        /// <summary>
        /// Gets the stage preview splitter column for hiding when undocked.
        /// </summary>
        public ColumnDefinition GetStagePreviewSplitterColumn() => StagePreviewSplitterColumn;

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - STAGE (CAMERA) CONTROLS
        // ════════════════════════════════════════════════════════════════════

        private bool _suppressStageValueChanges;

        private void StageEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.Stage.Enabled = StageEnabledToggle.IsChecked ?? false;

            // Update visibility of the quick add keyframe button
            UpdateStageQuickButtonVisibility();
        }

        /// <summary>
        /// Updates the visibility of the Stage quick-add keyframe button.
        /// </summary>
        private void UpdateStageQuickButtonVisibility()
        {
            bool stageEnabled = _canvasAnimationState?.Stage.Enabled ?? false;

            if (StageAddKeyframeQuickButton != null)
            {
                StageAddKeyframeQuickButton.Visibility = stageEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (StageRemoveKeyframeQuickButton != null)
            {
                StageRemoveKeyframeQuickButton.Visibility = stageEnabled
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void StageAddKeyframeQuick_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.CaptureStageKeyframe(_canvasAnimationState.CurrentFrameIndex);
            RefreshCanvasKeyframeGrid();

            // Clear pending edits since we just saved them
            _canvasHost?.ClearStagePendingEdits();
            _canvasHost?.InvalidateCanvas();
            StagePreview.RefreshPreview();
        }

        private void StageRemoveKeyframeQuick_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.RemoveStageKeyframe(_canvasAnimationState.CurrentFrameIndex);
            RefreshCanvasKeyframeGrid();
            _canvasHost?.InvalidateCanvas();
            StagePreview.RefreshPreview();
        }

        private void StageSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            RefreshStageSettingsUI();
        }

        private void RefreshStageSettingsUI()
        {
            if (_canvasAnimationState == null) return;
            var stage = _canvasAnimationState.Stage;

            _suppressStageValueChanges = true;

            StageXBox.Value = stage.StageX;
            StageYBox.Value = stage.StageY;
            StageWidthBox.Value = stage.StageWidth;
            StageHeightBox.Value = stage.StageHeight;
            StageOutputWidthBox.Value = stage.OutputWidth;
            StageOutputHeightBox.Value = stage.OutputHeight;

            // Set maximum values based on canvas dimensions
            if (_document != null)
            {
                StageXBox.Maximum = _document.PixelWidth - 1;
                StageYBox.Maximum = _document.PixelHeight - 1;
                StageWidthBox.Maximum = _document.PixelWidth;
                StageHeightBox.Maximum = _document.PixelHeight;
                StageOutputWidthBox.Maximum = _document.PixelWidth * 4; // Allow upscaling
                StageOutputHeightBox.Maximum = _document.PixelHeight * 4;
            }

            // Scaling algorithm
            int scalingIndex = stage.ScalingAlgorithm switch
            {
                StageScalingAlgorithm.NearestNeighbor => 0,
                StageScalingAlgorithm.Bilinear => 1,
                StageScalingAlgorithm.Bicubic => 2,
                _ => 0
            };
            StageScalingCombo.SelectedIndex = scalingIndex;

            // Bounds mode
            int boundsIndex = stage.BoundsMode switch
            {
                StageBoundsMode.Free => 0,
                StageBoundsMode.Constrained => 1,
                StageBoundsMode.CenterLocked => 2,
                _ => 1
            };
            StageBoundsCombo.SelectedIndex = boundsIndex;

            _suppressStageValueChanges = false;
        }

        private void StageX_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.Stage.StageX = (int)args.NewValue;
        }

        private void StageY_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.Stage.StageY = (int)args.NewValue;
        }

        private void StageWidth_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.Stage.StageWidth = (int)args.NewValue;
        }

        private void StageHeight_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.Stage.StageHeight = (int)args.NewValue;
        }

        private void StageOutputWidth_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.Stage.OutputWidth = (int)args.NewValue;
        }

        private void StageOutputHeight_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.Stage.OutputHeight = (int)args.NewValue;
        }

        private void StageScaling_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null) return;
            if (StageScalingCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _canvasAnimationState.Stage.ScalingAlgorithm = tag switch
                {
                    "NearestNeighbor" => StageScalingAlgorithm.NearestNeighbor,
                    "Bilinear" => StageScalingAlgorithm.Bilinear,
                    "Bicubic" => StageScalingAlgorithm.Bicubic,
                    _ => StageScalingAlgorithm.NearestNeighbor
                };
            }
        }

        private void StageBounds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressStageValueChanges || _canvasAnimationState == null) return;
            if (StageBoundsCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _canvasAnimationState.Stage.BoundsMode = tag switch
                {
                    "Free" => StageBoundsMode.Free,
                    "Constrained" => StageBoundsMode.Constrained,
                    "CenterLocked" => StageBoundsMode.CenterLocked,
                    _ => StageBoundsMode.Constrained
                };
            }
        }

        private void StageMatchCanvas_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null || _document == null) return;
            _canvasAnimationState.Stage.MatchCanvas(_document.PixelWidth, _document.PixelHeight);
            RefreshStageSettingsUI();
        }

        private void StageAddKeyframe_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.CaptureStageKeyframe(_canvasAnimationState.CurrentFrameIndex);
            RefreshCanvasKeyframeGrid();

            // Clear pending edits since we just saved them
            _canvasHost?.ClearStagePendingEdits();
            _canvasHost?.InvalidateCanvas();
            StagePreview.RefreshPreview();
        }

        private void StageRemoveKeyframe_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.RemoveStageKeyframe(_canvasAnimationState.CurrentFrameIndex);
            RefreshCanvasKeyframeGrid();
            _canvasHost?.InvalidateCanvas();
            StagePreview.RefreshPreview();
        }

        private void StageTrack_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Toggle stage selection
            SelectStage(!IsStageSelected);
        }

        private void StageTrack_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && !IsStageSelected)
            {
                border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
            }
        }

        private void StageTrack_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && !IsStageSelected)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(40, 255, 165, 0));
            }
        }

        /// <summary>
        /// Selects or deselects the Stage track.
        /// </summary>
        public void SelectStage(bool selected)
        {
            if (IsStageSelected == selected) return;

            IsStageSelected = selected;

            // Deselect any layer when stage is selected
            if (selected)
            {
                _selectedLayerId = Guid.Empty;
            }

            // Update visual state
            RefreshCanvasLayerNames();

            // Notify listeners
            StageSelectionChanged?.Invoke(selected);
        }

        /// <summary>
        /// Deselects the stage (called when a layer is selected).
        /// </summary>
        public void DeselectStage()
        {
            if (IsStageSelected)
            {
                IsStageSelected = false;
                RefreshCanvasLayerNames();
                StageSelectionChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Draws keyframes for the stage track.
        /// </summary>
        private void DrawStageTrackKeyframes(int trackIndex)
        {
            if (_canvasAnimationState == null) return;

            var stageTrack = _canvasAnimationState.StageTrack;
            var keyframeIndices = stageTrack.GetKeyframeIndices().ToList();

            for (int i = 0; i < keyframeIndices.Count; i++)
            {
                int frameIndex = keyframeIndices[i];
                int nextFrameIndex = (i + 1 < keyframeIndices.Count)
                    ? keyframeIndices[i + 1]
                    : _canvasAnimationState.FrameCount;

                // Draw interpolation region (stage uses interpolation, not hold)
                if (nextFrameIndex > frameIndex + 1)
                {
                    var interpRect = new Rectangle
                    {
                        Width = (nextFrameIndex - frameIndex - 1) * CellWidth,
                        Height = CellHeight - 4,
                        Fill = new SolidColorBrush(Colors.Orange) { Opacity = 0.3 },
                        RadiusX = 2,
                        RadiusY = 2
                    };
                    Canvas.SetLeft(interpRect, (frameIndex + 1) * CellWidth + 2);
                    Canvas.SetTop(interpRect, trackIndex * CellHeight + 2);
                    CanvasKeyframeCanvas.Children.Add(interpRect);

                    // Draw interpolation line
                    var interpLine = new Line
                    {
                        X1 = frameIndex * CellWidth + CellWidth / 2,
                        Y1 = trackIndex * CellHeight + CellHeight / 2,
                        X2 = nextFrameIndex * CellWidth + CellWidth / 2,
                        Y2 = trackIndex * CellHeight + CellHeight / 2,
                        Stroke = new SolidColorBrush(Colors.Orange),
                        StrokeThickness = 2,
                        StrokeDashArray = [2, 2]
                    };
                    CanvasKeyframeCanvas.Children.Add(interpLine);
                }

                // Draw keyframe diamond (orange for stage)
                DrawStageKeyframeDiamond(frameIndex, trackIndex);
            }
        }

        /// <summary>
        /// Draws a stage keyframe diamond (orange color).
        /// </summary>
        private void DrawStageKeyframeDiamond(int frameIndex, int trackIndex)
        {
            var diamond = new Polygon
            {
                Points =
                [
                    new Point(KeyframeDiamondSize / 2, 0),
                    new Point(KeyframeDiamondSize, KeyframeDiamondSize / 2),
                    new Point(KeyframeDiamondSize / 2, KeyframeDiamondSize),
                    new Point(0, KeyframeDiamondSize / 2)
                ],
                Fill = new SolidColorBrush(Colors.Orange),
                Stroke = new SolidColorBrush(Colors.DarkOrange),
                StrokeThickness = 1
            };

            double x = frameIndex * CellWidth + (CellWidth - KeyframeDiamondSize) / 2;
            double y = trackIndex * CellHeight + (CellHeight - KeyframeDiamondSize) / 2;
            Canvas.SetLeft(diamond, x);
            Canvas.SetTop(diamond, y);
            CanvasKeyframeCanvas.Children.Add(diamond);
        }
    }
}
