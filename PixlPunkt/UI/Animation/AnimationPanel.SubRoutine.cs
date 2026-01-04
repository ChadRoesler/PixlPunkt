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
        // SUB-ROUTINE SELECTION STATE
        // ====================================================================

        /// <summary>
        /// Gets the currently selected sub-routine, or null if none selected.
        /// </summary>
        public AnimationSubRoutine? SelectedSubRoutine { get; private set; }

        /// <summary>
        /// Gets whether a sub-routine is currently selected.
        /// </summary>
        public bool IsSubRoutineSelected => SelectedSubRoutine != null;

        /// <summary>
        /// Raised when the sub-routine selection changes.
        /// Parameter is the selected sub-routine (null if deselected).
        /// </summary>
        public event Action<AnimationSubRoutine?>? SubRoutineSelectionChanged;

        /// <summary>
        /// Selects a sub-routine for editing.
        /// </summary>
        /// <param name="subRoutine">The sub-routine to select, or null to deselect.</param>
        public void SelectSubRoutine(AnimationSubRoutine? subRoutine)
        {
            if (SelectedSubRoutine == subRoutine) return;

            SelectedSubRoutine = subRoutine;

            // Deselect layer and stage when sub-routine is selected
            if (subRoutine != null)
            {
                _selectedLayerId = Guid.Empty;
                DeselectStage();
            }

            // Sync selection to canvas host
            _canvasHost?.SetSelectedSubRoutine(subRoutine);

            // Update visual state
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();

            // Notify listeners
            SubRoutineSelectionChanged?.Invoke(subRoutine);
        }

        /// <summary>
        /// Deselects the current sub-routine (called when a layer or stage is selected).
        /// </summary>
        public void DeselectSubRoutine()
        {
            if (SelectedSubRoutine != null)
            {
                SelectedSubRoutine = null;
                
                // Sync deselection to canvas host
                _canvasHost?.SetSelectedSubRoutine(null);
                
                RefreshCanvasLayerNames();
                RefreshCanvasKeyframeGrid();
                SubRoutineSelectionChanged?.Invoke(null);
            }
        }

        /// <summary>
        /// Draws all sub-routine bars in the sub-routine track row(s).
        /// Each sub-routine is displayed as a horizontal bar showing its start frame and duration.
        /// Each sub-routine gets its own dedicated track row.
        /// </summary>
        private void DrawSubRoutineTrack(int rowIndex)
        {
            if (_canvasAnimationState == null) return;

            int frameCount = _canvasAnimationState.FrameCount;
            const int barHeight = CellHeight - 8; // Space for borders and gaps
            const int verticalPadding = 4; // Center the bar vertically

            // Get all sub-routines (including disabled ones for display)
            var subRoutines = _canvasAnimationState.SubRoutines.SubRoutines.ToList();

            if (subRoutines.Count == 0)
                return;

            // Draw each sub-routine in its own track row
            for (int subIdx = 0; subIdx < subRoutines.Count; subIdx++)
            {
                var subRoutine = subRoutines[subIdx];
                bool isSelected = subRoutine == SelectedSubRoutine;
                bool isEnabled = subRoutine.IsEnabled;

                int startFrame = subRoutine.StartFrame;
                int endFrame = subRoutine.EndFrame;

                // Skip if completely outside visible range
                if (endFrame < 0 || startFrame >= frameCount)
                    continue;

                // For visual display, clamp to visible range, but allow full width for interaction
                int visibleStart = Math.Max(0, startFrame);
                int visibleEnd = endFrame; // Don't clamp the end - let it extend

                // Each sub-routine gets its own track row
                int trackY = (rowIndex + subIdx) * CellHeight;

                // Draw sub-routine bar (centered vertically, highlight if selected, dim if disabled)
                Color barFillColor;
                Color barStrokeColor;
                
                if (!isEnabled)
                {
                    // Disabled: dim/grayed out
                    barFillColor = Color.FromArgb(100, 120, 120, 120);
                    barStrokeColor = Colors.Gray;
                }
                else if (isSelected)
                {
                    // Selected: bright orange
                    barFillColor = Color.FromArgb(255, 255, 180, 80);
                    barStrokeColor = Colors.Orange;
                }
                else
                {
                    // Normal: tan/sandy color
                    barFillColor = Color.FromArgb(200, 200, 120, 60);
                    barStrokeColor = Colors.SandyBrown;
                }

                // Calculate bar width - allow extending beyond visible frame count
                int barWidthFrames = visibleEnd - visibleStart;

                var bar = new Rectangle
                {
                    Width = barWidthFrames * CellWidth,
                    Height = barHeight,
                    Fill = new SolidColorBrush(barFillColor),
                    RadiusX = 3,
                    RadiusY = 3,
                    StrokeThickness = isSelected ? 2.5 : 1.5,
                    Stroke = new SolidColorBrush(barStrokeColor)
                };
                Canvas.SetLeft(bar, visibleStart * CellWidth);
                Canvas.SetTop(bar, trackY + verticalPadding);

                // Add tag for interaction handling
                bar.Tag = subRoutine;
                bar.PointerPressed += SubRoutineBar_PointerPressed;
                bar.PointerMoved += SubRoutineBar_PointerMoved;
                bar.PointerReleased += SubRoutineBar_PointerReleased;
                bar.RightTapped += SubRoutineBar_RightTapped;

                CanvasKeyframeCanvas.Children.Add(bar);

                // Only show handles and keyframes for enabled sub-routines
                if (isEnabled)
                {
                    // Draw left resize handle (darker, visible indicator)
                    var leftHandle = new Rectangle
                    {
                        Width = 8,
                        Height = barHeight,
                        Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30)),
                        RadiusX = 2,
                        RadiusY = 2,
                        IsHitTestVisible = true
                    };
                    Canvas.SetLeft(leftHandle, visibleStart * CellWidth);
                    Canvas.SetTop(leftHandle, trackY + verticalPadding);
                    leftHandle.Tag = ("left_handle", subRoutine);
                    leftHandle.PointerPressed += SubRoutineHandle_PointerPressed;
                    leftHandle.PointerMoved += SubRoutineHandle_PointerMoved;
                    leftHandle.PointerReleased += SubRoutineHandle_PointerReleased;
                    leftHandle.PointerEntered += (s, e) => { 
                        leftHandle.Fill = new SolidColorBrush(Color.FromArgb(220, 120, 80, 50));
                        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
                    };
                    leftHandle.PointerExited += (s, e) => { 
                        leftHandle.Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30));
                        if (!_isSubRoutineInteracting) ProtectedCursor = null;
                    };
                    CanvasKeyframeCanvas.Children.Add(leftHandle);

                    // Draw right resize handle (darker, visible indicator)
                    var rightHandle = new Rectangle
                    {
                        Width = 8,
                        Height = barHeight,
                        Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30)),
                        RadiusX = 2,
                        RadiusY = 2,
                        IsHitTestVisible = true
                    };
                    // Position the right handle at the actual end of the bar
                    Canvas.SetLeft(rightHandle, visibleStart * CellWidth + barWidthFrames * CellWidth - 8);
                    Canvas.SetTop(rightHandle, trackY + verticalPadding);
                    rightHandle.Tag = ("right_handle", subRoutine);
                    rightHandle.PointerPressed += SubRoutineHandle_PointerPressed;
                    rightHandle.PointerMoved += SubRoutineHandle_PointerMoved;
                    rightHandle.PointerReleased += SubRoutineHandle_PointerReleased;
                    rightHandle.PointerEntered += (s, e) => { 
                        rightHandle.Fill = new SolidColorBrush(Color.FromArgb(220, 120, 80, 50));
                        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
                    };
                    rightHandle.PointerExited += (s, e) => { 
                        rightHandle.Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30));
                        if (!_isSubRoutineInteracting) ProtectedCursor = null;
                    };
                    CanvasKeyframeCanvas.Children.Add(rightHandle);

                    // Draw keyframe diamonds for position, scale, rotation
                    DrawSubRoutineKeyframesForRow(subRoutine, rowIndex + subIdx);
                }

                // Draw reel name label (centered in bar) - show for all, with strikethrough if disabled
                if (barWidthFrames >= 3) // Only show if wide enough
                {
                    var labelText = isEnabled ? subRoutine.DisplayName : $"⊘ {subRoutine.DisplayName}";
                    var label = new TextBlock
                    {
                        Text = labelText,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(isEnabled ? Colors.White : Colors.Gray),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center,
                        MaxWidth = Math.Max(0, barWidthFrames * CellWidth - 24), // Leave room for handles
                        IsHitTestVisible = false,  // Allow clicks to pass through to bar
                    };
                    Canvas.SetLeft(label, visibleStart * CellWidth + 12);
                    Canvas.SetTop(label, trackY + verticalPadding + (barHeight - 12) / 2);
                    CanvasKeyframeCanvas.Children.Add(label);
                }
            }
        }

        /// <summary>
        /// Draws a single sub-routine track row at the specified row index.
        /// </summary>
        private void DrawSubRoutineTrackRow(int rowIndex, AnimationSubRoutine subRoutine)
        {
            if (_canvasAnimationState == null) return;

            int frameCount = _canvasAnimationState.FrameCount;
            const int barHeight = CellHeight - 8;
            const int verticalPadding = 4;

            bool isSelected = subRoutine == SelectedSubRoutine;
            bool isEnabled = subRoutine.IsEnabled;

            int startFrame = subRoutine.StartFrame;
            int endFrame = subRoutine.EndFrame;

            // Skip if completely outside visible range
            if (endFrame < 0 || startFrame >= frameCount)
                return;

            int visibleStart = Math.Max(0, startFrame);
            int visibleEnd = endFrame;
            int trackY = rowIndex * CellHeight;

            // Determine colors
            Color barFillColor;
            Color barStrokeColor;
            
            if (!isEnabled)
            {
                barFillColor = Color.FromArgb(100, 120, 120, 120);
                barStrokeColor = Colors.Gray;
            }
            else if (isSelected)
            {
                barFillColor = Color.FromArgb(255, 255, 180, 80);
                barStrokeColor = Colors.Orange;
            }
            else
            {
                barFillColor = Color.FromArgb(200, 200, 120, 60);
                barStrokeColor = Colors.SandyBrown;
            }

            int barWidthFrames = visibleEnd - visibleStart;

            var bar = new Rectangle
            {
                Width = barWidthFrames * CellWidth,
                Height = barHeight,
                Fill = new SolidColorBrush(barFillColor),
                RadiusX = 3,
                RadiusY = 3,
                StrokeThickness = isSelected ? 2.5 : 1.5,
                Stroke = new SolidColorBrush(barStrokeColor)
            };
            Canvas.SetLeft(bar, visibleStart * CellWidth);
            Canvas.SetTop(bar, trackY + verticalPadding);

            // Add tag for interaction handling
            bar.Tag = subRoutine;
            bar.PointerPressed += SubRoutineBar_PointerPressed;
            bar.PointerMoved += SubRoutineBar_PointerMoved;
            bar.PointerReleased += SubRoutineBar_PointerReleased;
            bar.RightTapped += SubRoutineBar_RightTapped;

            CanvasKeyframeCanvas.Children.Add(bar);

            if (isEnabled)
            {
                // Left resize handle
                var leftHandle = new Rectangle
                {
                    Width = 8,
                    Height = barHeight,
                    Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30)),
                    RadiusX = 2,
                    RadiusY = 2,
                    IsHitTestVisible = true
                };
                Canvas.SetLeft(leftHandle, visibleStart * CellWidth);
                Canvas.SetTop(leftHandle, trackY + verticalPadding);
                leftHandle.Tag = ("left_handle", subRoutine);
                leftHandle.PointerPressed += SubRoutineHandle_PointerPressed;
                leftHandle.PointerMoved += SubRoutineHandle_PointerMoved;
                leftHandle.PointerReleased += SubRoutineHandle_PointerReleased;
                leftHandle.PointerEntered += (s, e) => { 
                    leftHandle.Fill = new SolidColorBrush(Color.FromArgb(220, 120, 80, 50));
                    ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
                };
                leftHandle.PointerExited += (s, e) => { 
                    leftHandle.Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30));
                    if (!_isSubRoutineInteracting) ProtectedCursor = null;
                };
                CanvasKeyframeCanvas.Children.Add(leftHandle);

                // Right resize handle
                var rightHandle = new Rectangle
                {
                    Width = 8,
                    Height = barHeight,
                    Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30)),
                    RadiusX = 2,
                    RadiusY = 2,
                    IsHitTestVisible = true
                };
                Canvas.SetLeft(rightHandle, visibleStart * CellWidth + barWidthFrames * CellWidth - 8);
                Canvas.SetTop(rightHandle, trackY + verticalPadding);
                rightHandle.Tag = ("right_handle", subRoutine);
                rightHandle.PointerPressed += SubRoutineHandle_PointerPressed;
                rightHandle.PointerMoved += SubRoutineHandle_PointerMoved;
                rightHandle.PointerReleased += SubRoutineHandle_PointerReleased;
                rightHandle.PointerEntered += (s, e) => { 
                    rightHandle.Fill = new SolidColorBrush(Color.FromArgb(220, 120, 80, 50));
                    ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
                };
                rightHandle.PointerExited += (s, e) => { 
                    rightHandle.Fill = new SolidColorBrush(Color.FromArgb(180, 80, 50, 30));
                    if (!_isSubRoutineInteracting) ProtectedCursor = null;
                };
                CanvasKeyframeCanvas.Children.Add(rightHandle);

                // Draw keyframe diamonds for position, scale, rotation
                DrawSubRoutineKeyframesForRow(subRoutine, rowIndex);
            }

            // Label
            if (barWidthFrames >= 3)
            {
                var labelText = isEnabled ? subRoutine.DisplayName : $"⊘ {subRoutine.DisplayName}";
                var label = new TextBlock
                {
                    Text = labelText,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(isEnabled ? Colors.White : Colors.Gray),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = Math.Max(0, barWidthFrames * CellWidth - 24),
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(label, visibleStart * CellWidth + 12);
                Canvas.SetTop(label, trackY + verticalPadding + (barHeight - 12) / 2);
                CanvasKeyframeCanvas.Children.Add(label);
            }
        }

        /// <summary>
        /// Draws keyframe diamonds for a sub-routine at a specific row index.
        /// </summary>
        private void DrawSubRoutineKeyframesForRow(AnimationSubRoutine subRoutine, int rowIndex)
        {
            if (_canvasAnimationState == null) return;

            const int smallDiamondSize = 6;
            int trackY = rowIndex * CellHeight;

            var allKeyframePositions = new HashSet<float>();
            foreach (var key in subRoutine.PositionKeyframes.Keys)
                allKeyframePositions.Add(key);
            foreach (var key in subRoutine.ScaleKeyframes.Keys)
                allKeyframePositions.Add(key);
            foreach (var key in subRoutine.RotationKeyframes.Keys)
                allKeyframePositions.Add(key);

            foreach (var normalizedPos in allKeyframePositions)
            {
                int frameIndex = subRoutine.StartFrame + (int)(normalizedPos * subRoutine.DurationFrames);
                
                if (frameIndex < 0 || frameIndex >= _canvasAnimationState.FrameCount)
                    continue;

                bool hasPosition = subRoutine.PositionKeyframes.ContainsKey(normalizedPos);
                bool hasScale = subRoutine.ScaleKeyframes.ContainsKey(normalizedPos);
                bool hasRotation = subRoutine.RotationKeyframes.ContainsKey(normalizedPos);

                Color diamondColor;
                if (hasPosition && hasScale && hasRotation)
                    diamondColor = Colors.Gold;
                else if (hasPosition)
                    diamondColor = Colors.LimeGreen;
                else if (hasScale)
                    diamondColor = Colors.DeepSkyBlue;
                else
                    diamondColor = Colors.HotPink;

                var diamond = new Polygon
                {
                    Points =
                    [
                        new Point(smallDiamondSize / 2, 0),
                        new Point(smallDiamondSize, smallDiamondSize / 2),
                        new Point(smallDiamondSize / 2, smallDiamondSize),
                        new Point(0, smallDiamondSize / 2)
                    ],
                    Fill = new SolidColorBrush(diamondColor),
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 0.5,
                    IsHitTestVisible = false
                };

                double x = frameIndex * CellWidth + (CellWidth - smallDiamondSize) / 2;
                double y = trackY + CellHeight - smallDiamondSize - 2;
                Canvas.SetLeft(diamond, x);
                Canvas.SetTop(diamond, y);
                CanvasKeyframeCanvas.Children.Add(diamond);
            }
        }

        /// <summary>
        /// Calculates how many vertical stack rows are needed for sub-routines.
        /// Sub-routines that overlap in time share the same row.
        /// </summary>
        private int CalculateSubRoutineStackHeight(List<AnimationSubRoutine> sortedSubRoutines)
        {
            // Each sub-routine now gets its own track row
            return sortedSubRoutines.Count;
        }

        /// <summary>
        /// Calculates which stack row (vertical position) a sub-routine belongs in.
        /// </summary>
        private int CalculateStackPosition(AnimationSubRoutine subRoutine, List<AnimationSubRoutine> sortedSubRoutines)
        {
            // Each sub-routine is at its own index
            return sortedSubRoutines.IndexOf(subRoutine);
        }


        // ====================================================================
        // SUB-ROUTINE TIMELINE INTERACTION
        // ====================================================================

        private AnimationSubRoutine? _draggingSubRoutine;
        private int _dragStartFrame;
        private double _dragStartX;
        private AnimationSubRoutine? _resizingSubRoutine;
        private string? _resizeDirection;
        private int _resizeStartDuration;
        private int _resizeStartFrame;
        private double _resizeStartX;
        private bool _isSubRoutineInteracting;

        private void SubRoutineBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Rectangle bar) return;
            if (bar.Tag is not AnimationSubRoutine subRoutine) return;

            // Check for right-click (context menu)
            var props = e.GetCurrentPoint(bar).Properties;
            if (props.IsRightButtonPressed)
            {
                // Will be handled by RightTapped event
                return;
            }

            // DON'T select the sub-routine on bar click - only track header selects (like stage)
            // Just check if this sub-routine is already selected
            if (SelectedSubRoutine != subRoutine)
            {
                // Not selected - don't allow drag/resize, just consume the click
                e.Handled = true;
                return;
            }

            // Check if Ctrl is held - required for drag/resize operations
            var ctrlDown = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) 
                & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            if (!ctrlDown)
            {
                // Ctrl not held - just consume the click
                e.Handled = true;
                return;
            }

            // Get pointer position relative to the bar
            var pointerPosition = e.GetCurrentPoint(bar).Position;

            // Check if clicking on handle zones (left or right 8 pixels)
            bool isLeftHandle = pointerPosition.X < 10;
            bool isRightHandle = pointerPosition.X > bar.Width - 10;

            if (isLeftHandle)
            {
                // Start left resize
                _resizingSubRoutine = subRoutine;
                _resizeDirection = "left";
                _resizeStartFrame = subRoutine.StartFrame;
                _resizeStartDuration = subRoutine.DurationFrames;
                _resizeStartX = e.GetCurrentPoint(CanvasKeyframeCanvas).Position.X;
                _isSubRoutineInteracting = true;
                ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
            }
            else if (isRightHandle)
            {
                // Start right resize
                _resizingSubRoutine = subRoutine;
                _resizeDirection = "right";
                _resizeStartFrame = subRoutine.StartFrame;
                _resizeStartDuration = subRoutine.DurationFrames;
                _resizeStartX = e.GetCurrentPoint(CanvasKeyframeCanvas).Position.X;
                _isSubRoutineInteracting = true;
                ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
            }
            else
            {
                // Start drag (move)
                _draggingSubRoutine = subRoutine;
                _dragStartFrame = subRoutine.StartFrame;
                _dragStartX = e.GetCurrentPoint(CanvasKeyframeCanvas).Position.X;
                _isSubRoutineInteracting = true;
                ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeAll);
            }

            // Capture pointer on the CANVAS, not the bar - this way we receive events
            // even when the pointer moves outside the bar's bounds during drag/resize
            CanvasKeyframeCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void SubRoutineBar_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // Actual tracking is done via CanvasKeyframeCanvas_PointerMoved when we have capture
        }

        private void SubRoutineBar_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // Release is handled by CanvasKeyframeCanvas_PointerReleased when we captured there
        }

        private void EndSubRoutineInteraction()
        {
            // Refresh grid to reflect final state
            if (_isSubRoutineInteracting)
            {
                RefreshCanvasKeyframeGrid();
                UpdateCanvasPlayhead();
                
                // Refresh the stage preview to show updated position
                StagePreview?.RefreshPreview();
            }

            _draggingSubRoutine = null;
            _resizingSubRoutine = null;
            _resizeDirection = null;
            _isSubRoutineInteracting = false;
            ProtectedCursor = null;
        }

        // ====================================================================
        // HANDLE EVENT FORWARDING
        // ====================================================================

        private void SubRoutineHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Rectangle handle) return;
            
            // Extract the sub-routine and direction from the tag tuple
            if (handle.Tag is not ValueTuple<string, AnimationSubRoutine> tagData) return;
            
            string direction = tagData.Item1;
            AnimationSubRoutine subRoutine = tagData.Item2;

            // DON'T select the sub-routine on handle click - only track header selects (like stage)
            // Just check if this sub-routine is already selected
            if (SelectedSubRoutine != subRoutine)
            {
                // Not selected - don't allow resize, just consume the click
                e.Handled = true;
                return;
            }

            // Check if Ctrl is held - required for resize operations
            var ctrlDown = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) 
                & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            if (!ctrlDown)
            {
                // Just consume the click, don't start resize
                e.Handled = true;
                return;
            }

            // Start resize operation
            _resizingSubRoutine = subRoutine;
            _resizeDirection = direction == "left_handle" ? "left" : "right";
            _resizeStartFrame = subRoutine.StartFrame;
            _resizeStartDuration = subRoutine.DurationFrames;
            _resizeStartX = e.GetCurrentPoint(CanvasKeyframeCanvas).Position.X;
            _isSubRoutineInteracting = true;

            ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);

            // Capture pointer on the CANVAS, not the handle - this way we receive events
            // even when the pointer moves outside the handle's small bounds
            CanvasKeyframeCanvas.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void SubRoutineHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            // This handler on the handle itself won't receive events once pointer leaves bounds
            // The actual tracking is done via CanvasKeyframeCanvas_PointerMoved when we have capture
        }

        private void SubRoutineHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            // Release is handled by CanvasKeyframeCanvas when we captured there
        }

        private void OnSubRoutinesChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshCanvasLayerNames();
                RefreshCanvasKeyframeGrid();
                UpdateCanvasPlayhead();
            });
        }

        // ====================================================================
        // SUB-ROUTINE TRACK HEADER INTERACTION
        // ====================================================================

        private void SubRoutineTrackHeader_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Border header) return;
            if (header.Tag is not AnimationSubRoutine subRoutine) return;

            // Select this sub-routine (like stage selection)
            SelectSubRoutine(subRoutine);
            e.Handled = true;
        }

        private void SubRoutineEyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            if (button.Tag is not AnimationSubRoutine subRoutine) return;

            // Toggle visibility
            subRoutine.IsEnabled = !subRoutine.IsEnabled;
            
            // Refresh UI
            RefreshCanvasKeyframeGrid();
            RefreshCanvasLayerNames();
            StagePreview?.RefreshPreview();
            
            _document?.RaiseDocumentModified();
        }

        private void SubRoutineTrackHeader_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is not Border header) return;
            if (header.Tag is not AnimationSubRoutine subRoutine) return;

            _contextMenuSubRoutine = subRoutine;

            // Select the sub-routine
            SelectSubRoutine(subRoutine);

            // Create context menu
            var menu = CreateSubRoutineContextMenu(subRoutine);

            // Show menu at pointer position
            menu.ShowAt(header, e.GetPosition(header));
            e.Handled = true;
        }

        // ====================================================================
        // SUB-ROUTINE CONTEXT MENU
        // ====================================================================

        private AnimationSubRoutine? _contextMenuSubRoutine;

        /// <summary>
        /// Creates a context menu for a sub-routine with all available options.
        /// </summary>
        private MenuFlyout CreateSubRoutineContextMenu(AnimationSubRoutine subRoutine)
        {
            var menu = new MenuFlyout();

            // Toggle visibility option
            var visibilityItem = new MenuFlyoutItem
            {
                Text = subRoutine.IsEnabled ? "Hide Sub-Routine" : "Show Sub-Routine",
                Icon = new FluentIcons.WinUI.SymbolIcon 
                { 
                    Symbol = (FluentIcons.Common.Symbol)(subRoutine.IsEnabled ? Icon.EyeOff : Icon.Eye)
                }
            };
            visibilityItem.Click += SubRoutineContext_ToggleVisibility;
            menu.Items.Add(visibilityItem);

            menu.Items.Add(new MenuFlyoutSeparator());

            // Z-Order options
            var moveUpItem = new MenuFlyoutItem
            {
                Text = "Move Up (Forward)",
                Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = (FluentIcons.Common.Symbol)Icon.ArrowUp }
            };
            moveUpItem.Click += SubRoutineContext_MoveUp;
            menu.Items.Add(moveUpItem);

            var moveDownItem = new MenuFlyoutItem
            {
                Text = "Move Down (Backward)",
                Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = (FluentIcons.Common.Symbol)Icon.ArrowDown }
            };
            moveDownItem.Click += SubRoutineContext_MoveDown;
            menu.Items.Add(moveDownItem);

            var moveToTopItem = new MenuFlyoutItem
            {
                Text = "Bring to Front",
                Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = (FluentIcons.Common.Symbol)Icon.ArrowUpload }
            };
            moveToTopItem.Click += SubRoutineContext_MoveToTop;
            menu.Items.Add(moveToTopItem);

            var moveToBottomItem = new MenuFlyoutItem
            {
                Text = "Send to Back",
                Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = (FluentIcons.Common.Symbol)Icon.ArrowDownload }
            };
            moveToBottomItem.Click += SubRoutineContext_MoveToBottom;
            menu.Items.Add(moveToBottomItem);

            menu.Items.Add(new MenuFlyoutSeparator());

            // Remove option
            var removeItem = new MenuFlyoutItem
            {
                Text = "Remove Sub-Routine",
                Icon = new FluentIcons.WinUI.SymbolIcon { Symbol = (FluentIcons.Common.Symbol)Icon.Delete }
            };
            removeItem.Click += SubRoutineContext_Remove;
            menu.Items.Add(removeItem);

            return menu;
        }

        private void SubRoutineBar_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is not Rectangle bar) return;
            if (bar.Tag is not AnimationSubRoutine subRoutine) return;

            _contextMenuSubRoutine = subRoutine;

            // Select the sub-routine
            SelectSubRoutine(subRoutine);

            // Create context menu
            var menu = CreateSubRoutineContextMenu(subRoutine);

            // Show menu at pointer position
            menu.ShowAt(bar, e.GetPosition(bar));
            e.Handled = true;
        }

        private void SubRoutineContext_ToggleVisibility(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSubRoutine == null) return;

            _contextMenuSubRoutine.IsEnabled = !_contextMenuSubRoutine.IsEnabled;
            
            RefreshCanvasKeyframeGrid();
            RefreshCanvasLayerNames();
            StagePreview?.RefreshPreview();
            
            _document?.RaiseDocumentModified();
        }

        private void SubRoutineContext_Remove(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSubRoutine == null || _canvasAnimationState == null) return;

            if (SelectedSubRoutine == _contextMenuSubRoutine)
            {
                DeselectSubRoutine();
            }

            _canvasAnimationState.SubRoutines.Remove(_contextMenuSubRoutine);
            
            RefreshCanvasKeyframeGrid();
            RefreshCanvasLayerNames();
            StagePreview?.RefreshPreview();
            
            _document?.RaiseDocumentModified();
            
            _contextMenuSubRoutine = null;
        }

        private void SubRoutineContext_MoveUp(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSubRoutine == null || _canvasAnimationState == null) return;

            // Increase Z-order by 1
            _contextMenuSubRoutine.ZOrder++;
            
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
            StagePreview?.RefreshPreview();
            _document?.RaiseDocumentModified();
        }

        private void SubRoutineContext_MoveDown(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSubRoutine == null || _canvasAnimationState == null) return;

            // Decrease Z-order by 1
            _contextMenuSubRoutine.ZOrder--;
            
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
            StagePreview?.RefreshPreview();
            _document?.RaiseDocumentModified();
        }

        private void SubRoutineContext_MoveToTop(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSubRoutine == null || _canvasAnimationState == null) return;

            // Set Z-order to be higher than the highest layer index
            int maxLayerIndex = _canvasAnimationState.Tracks.Count;
            _contextMenuSubRoutine.ZOrder = maxLayerIndex + 1;
            
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
            StagePreview?.RefreshPreview();
            _document?.RaiseDocumentModified();
        }

        private void SubRoutineContext_MoveToBottom(object sender, RoutedEventArgs e)
        {
            if (_contextMenuSubRoutine == null || _canvasAnimationState == null) return;

            // Set Z-order to -1 (below all layers)
            _contextMenuSubRoutine.ZOrder = -1;
            
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
            StagePreview?.RefreshPreview();
            _document?.RaiseDocumentModified();
        }
    }
}
