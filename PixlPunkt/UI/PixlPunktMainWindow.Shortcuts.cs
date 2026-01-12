using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Tools;
using PixlPunkt.UI.Animation;
using Windows.System;

namespace PixlPunkt.UI
{
    public sealed partial class PixlPunktMainWindow : Window
    {
        /// <summary>
        /// Tracks whether Space was used for pan mode (vs animation toggle).
        /// This ensures symmetric handling in KeyUp.
        /// </summary>
        private bool _spaceUsedForPan;

        /// <summary>
        /// Tracks whether the Animation Panel was the last area the user interacted with.
        /// When true, Space/Arrow shortcuts control animation. When false, they control pan/selection nudge.
        /// </summary>
        private bool _animationPanelHasFocus;

        private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Space for pan override OR animation playback toggle
            if (e.Key == VirtualKey.Space)
            {
                // Check if animation panel has focus AND is visible - use Space for play/pause
                if (_animationPanelHasFocus && IsAnimationPanelVisible())
                {
                    ToggleAnimationPlayback();
                    _spaceUsedForPan = false; // Space used for animation, not pan
                    e.Handled = true;
                    return;
                }

                // Otherwise, use Space for pan override
                CurrentHost?.BeginSpacePan();
                _spaceUsedForPan = true; // Space used for pan
                e.Handled = true;
                return;
            }

            // Track modifier state
            if (e.Key == VirtualKey.Shift) _shiftDown = true;
            if (e.Key == VirtualKey.Control) _ctrlDown = true;

            // ─────────────────────────────────────────────────────────────────
            // UNIVERSAL SHORTCUTS (Ctrl+Z, Ctrl+Y, Ctrl+C, etc.)
            // On Uno Skia platforms, KeyboardAccelerator.Invoked doesn't fire
            // reliably, so we handle these shortcuts here as a fallback.
            // ─────────────────────────────────────────────────────────────────
            bool ctrl = _ctrlDown || (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) &
                         Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
            bool shift = _shiftDown || (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) &
                         Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            if (ctrl && !IsTextInputFocused())
            {
                switch (e.Key)
                {
                    // Undo: Ctrl+Z
                    case VirtualKey.Z when !shift:
                        if (CurrentHost?.CanUndo == true)
                        {
                            CurrentHost.Undo();
                            UpdateHistoryUI();
                            e.Handled = true;
                            return;
                        }
                        break;

                    // Redo: Ctrl+Y or Ctrl+Shift+Z
                    case VirtualKey.Y:
                    case VirtualKey.Z when shift:
                        if (CurrentHost?.CanRedo == true)
                        {
                            CurrentHost.Redo();
                            UpdateHistoryUI();
                            e.Handled = true;
                            return;
                        }
                        break;

                    // Copy: Ctrl+C
                    case VirtualKey.C:
                        if (CurrentHost?.HasSelection == true)
                        {
                            CurrentHost.CopySelection();
                            e.Handled = true;
                            return;
                        }
                        break;

                    // Cut: Ctrl+X
                    case VirtualKey.X:
                        if (CurrentHost?.HasSelection == true)
                        {
                            CurrentHost.CutSelection();
                            e.Handled = true;
                            return;
                        }
                        break;

                    // Paste: Ctrl+V
                    case VirtualKey.V:
                        CurrentHost?.PasteClipboard();
                        e.Handled = true;
                        return;

                    // Select All: Ctrl+A
                    case VirtualKey.A:
                        CurrentHost?.Selection_SelectAll();
                        e.Handled = true;
                        return;

                    // New: Ctrl+N
                    case VirtualKey.N:
                        _ = NewCanvasAsync();
                        e.Handled = true;
                        return;

                    // Open: Ctrl+O
                    case VirtualKey.O:
                        _ = OpenDocumentAsync();
                        e.Handled = true;
                        return;

                    // Save: Ctrl+S
                    case VirtualKey.S when !shift:
                        _ = SaveDocumentAsync();
                        e.Handled = true;
                        return;

                    // Save As: Ctrl+Shift+S
                    case VirtualKey.S when shift:
                        _ = SaveDocumentAsAsync();
                        e.Handled = true;
                        return;

                    // Invert Selection: Ctrl+Shift+I
                    case VirtualKey.I when shift:
                        CurrentHost?.Selection_InvertSelection();
                        e.Handled = true;
                        return;

                    // Zoom In: Ctrl+Plus (Add key)
                    case VirtualKey.Add:
                        CurrentHost?.ZoomIn();
                        e.Handled = true;
                        return;

                    // Zoom Out: Ctrl+Minus (Subtract key)
                    case VirtualKey.Subtract:
                        CurrentHost?.ZoomOut();
                        e.Handled = true;
                        return;

                    // Fit to Screen: Ctrl+Home
                    case VirtualKey.Home:
                        CurrentHost?.Fit();
                        e.Handled = true;
                        return;

                    // Actual Size: Ctrl+End
                    case VirtualKey.End:
                        CurrentHost?.CanvasActualSize();
                        e.Handled = true;
                        return;
                }
            }

            // Delete/Backspace: Delete selection
            if ((e.Key == VirtualKey.Delete || e.Key == VirtualKey.Back) && !IsTextInputFocused() && !_suspendToolAccelerators)
            {
                if (CurrentHost?.HasSelection == true)
                {
                    CurrentHost.DeleteSelection();
                    e.Handled = true;
                    return;
                }
            }

            // Enter: Commit selection
            if (e.Key == VirtualKey.Enter && !IsTextInputFocused() && !_suspendToolAccelerators)
            {
                CurrentHost?.CommitSelection();
                e.Handled = true;
                return;
            }

            // Escape: Cancel selection
            if (e.Key == VirtualKey.Escape && !IsTextInputFocused() && !_suspendToolAccelerators)
            {
                CurrentHost?.CancelSelection();
                e.Handled = true;
                return;
            }

            // Skip tool shortcuts if text input is focused or accelerators are suspended
            if (IsTextInputFocused() || _suspendToolAccelerators)
            {
                return;
            }

            // ─────────────────────────────────────────────────────────────────
            // ANIMATION FRAME NAVIGATION (Arrow keys when animation panel has focus)
            // Left/Right: Previous/Next frame
            // Only when animation panel was the last thing interacted with
            // ─────────────────────────────────────────────────────────────────
            if (_animationPanelHasFocus && IsAnimationPanelVisible() && !_ctrlDown && !_shiftDown)
            {
                if (e.Key == VirtualKey.Left)
                {
                    StepAnimationBackward();
                    e.Handled = true;
                    return;
                }
                else if (e.Key == VirtualKey.Right)
                {
                    StepAnimationForward();
                    e.Handled = true;
                    return;
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // X KEY: SWAP FOREGROUND/BACKGROUND COLORS
            // Classic shortcut for quickly swapping active colors
            // ─────────────────────────────────────────────────────────────────
            if (e.Key == VirtualKey.X && !_ctrlDown && !_shiftDown)
            {
                _palette.Swap();
                e.Handled = true;
                return;
            }

            // ─────────────────────────────────────────────────────────────────
            // SELECTION NUDGE WITH ARROW KEYS
            // Arrow keys: 1px nudge
            // Shift+Arrow keys: 5px nudge
            // Only when NOT in animation panel focus mode
            // ─────────────────────────────────────────────────────────────────
            if (CurrentHost?.HasSelection == true && !_animationPanelHasFocus)
            {
                int nudgeAmount = _shiftDown ? 5 : 1;
                bool handled = true;

                switch (e.Key)
                {
                    case VirtualKey.Left:
                        CurrentHost.NudgeSelection(-nudgeAmount, 0);
                        break;
                    case VirtualKey.Right:
                        CurrentHost.NudgeSelection(nudgeAmount, 0);
                        break;
                    case VirtualKey.Up:
                        CurrentHost.NudgeSelection(0, -nudgeAmount);
                        break;
                    case VirtualKey.Down:
                        CurrentHost.NudgeSelection(0, nudgeAmount);
                        break;
                    default:
                        handled = false;
                        break;
                }

                if (handled)
                {
                    e.Handled = true;
                    return;
                }
            }

            // ─────────────────────────────────────────────────────────────────
            // DYNAMIC TOOL SHORTCUTS (from ToolSettingsBase.Shortcut)
            // This allows tools to define their own shortcuts declaratively
            // ─────────────────────────────────────────────────────────────────
            bool alt = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu) &
                        Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            if (_toolState.TryActivateByShortcut(e.Key, ctrl, shift, alt))
            {
                e.Handled = true;
                return;
            }

            // ─────────────────────────────────────────────────────────────────
            // BRUSH SIZE SHORTCUTS ([ and ])
            // Skip for utility tools that don't have brush size (Pan, Zoom, Dropper)
            // ─────────────────────────────────────────────────────────────────
            bool isUtilityWithoutBrush = _toolState.ActiveToolId == ToolIds.Pan ||
                                         _toolState.ActiveToolId == ToolIds.Zoom ||
                                         _toolState.ActiveToolId == ToolIds.Dropper;

            if (!isUtilityWithoutBrush)
            {
                if (e.Key == (VirtualKey)219) // '['
                {
                    int step = shift ? 5 : ctrl ? 10 : 1;
                    _toolState.UpdateBrush(b => b.Size = Math.Clamp(b.Size - step, 1, 128));
                    e.Handled = true;
                }
                else if (e.Key == (VirtualKey)221) // ']'
                {
                    int step = shift ? 5 : ctrl ? 10 : 1;
                    _toolState.UpdateBrush(b => b.Size = Math.Clamp(b.Size + step, 1, 128));
                    e.Handled = true;
                }
            }
        }

        private void Root_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                // Only end space pan if Space was actually used for panning
                if (_spaceUsedForPan)
                {
                    CurrentHost?.EndSpacePan();
                }
                _spaceUsedForPan = false;
                e.Handled = true;
            }
            if (e.Key == VirtualKey.Shift) _shiftDown = false;
            if (e.Key == VirtualKey.Control) _ctrlDown = false;
        }

        // ─────────────────────────────────────────────────────────────────
        // ANIMATION PANEL FOCUS TRACKING
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called when the user interacts with the Animation Panel.
        /// Sets focus to animation mode for shortcuts.
        /// </summary>
        internal void SetAnimationPanelFocus(bool hasFocus)
        {
            _animationPanelHasFocus = hasFocus;
        }

        /// <summary>
        /// Checks if the animation panel is visible (but not necessarily focused).
        /// </summary>
        private bool IsAnimationPanelVisible()
        {
            return AnimationPanel != null && 
                   AnimationPanel.Visibility == Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────────
        // ANIMATION PLAYBACK HELPERS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Toggles play/pause for the current animation mode.
        /// </summary>
        private void ToggleAnimationPlayback()
        {
            if (AnimationPanel == null) return;

            var doc = CurrentHost?.Document;
            if (doc == null) return;

            if (AnimationPanel.CurrentMode == AnimationMode.Canvas)
            {
                doc.CanvasAnimationState?.TogglePlayPause();
            }
            else // Tile mode
            {
                doc.TileAnimationState?.TogglePlayPause();
            }
        }

        /// <summary>
        /// Steps the animation backward one frame.
        /// </summary>
        private void StepAnimationBackward()
        {
            if (AnimationPanel == null) return;

            var doc = CurrentHost?.Document;
            if (doc == null) return;

            if (AnimationPanel.CurrentMode == AnimationMode.Canvas)
            {
                doc.CanvasAnimationState?.PreviousFrame();
            }
            else // Tile mode
            {
                doc.TileAnimationState?.PreviousFrame();
            }
        }

        /// <summary>
        /// Steps the animation forward one frame.
        /// </summary>
        private void StepAnimationForward()
        {
            if (AnimationPanel == null) return;

            var doc = CurrentHost?.Document;
            if (doc == null) return;

            if (AnimationPanel.CurrentMode == AnimationMode.Canvas)
            {
                doc.CanvasAnimationState?.NextFrame();
            }
            else // Tile mode
            {
                doc.TileAnimationState?.NextFrame();
            }
        }

        private void NewDocument_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            // Fire-and-forget async; underlying method already checks text input focus.
            _ = NewCanvasAsync();
            args.Handled = true;
        }

        private readonly Rendering.PatternBackgroundService _patternService = new();

        private void OpenDocument_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _ = OpenDocumentAsync();
            args.Handled = true;
        }

        private void SaveDocument_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _ = SaveDocumentAsync();
            args.Handled = true;
        }

        private void SaveAsDocument_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            _ = SaveDocumentAsAsync();
            args.Handled = true;
        }

        private void UndoAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            // Allow undo even when list controls have focus, only block for text input
            if (IsTextInputFocused()) return;
            if (CurrentHost?.CanUndo == true)
            {
                CurrentHost.Undo();
                UpdateHistoryUI();
                args.Handled = true;
            }
        }

        private void RedoAccel_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (IsTextInputFocused()) return;
            if (CurrentHost?.CanRedo == true)
            {
                CurrentHost.Redo();
                UpdateHistoryUI();
                args.Handled = true;
            }
        }

        private void CopyAccel_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (CurrentHost?.HasSelection == true)
            {
                CurrentHost.CopySelection();
                e.Handled = true;
            }
        }

        private void CutAccel_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (CurrentHost?.HasSelection == true)
            {
                CurrentHost.CutSelection();
                e.Handled = true;
            }
        }

        private void PasteAccel_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.PasteClipboard();
            e.Handled = true;
        }

        private void DeleteAccel_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (_suspendToolAccelerators) return;
            if (CurrentHost?.HasSelection == true)
            {
                CurrentHost.DeleteSelection();
                e.Handled = true;
            }
        }

        private void CommitSelection_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (_suspendToolAccelerators) return;
            CurrentHost?.CommitSelection();
            e.Handled = true;
        }

        private void CancelSelection_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            if (_suspendToolAccelerators) return;
            CurrentHost?.CancelSelection();
            e.Handled = true;
        }

        private void SelectAll_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.Selection_SelectAll();
            e.Handled = true;
        }

        private void InvertSelection_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.Selection_InvertSelection();
            e.Handled = true;
        }

        private void ZoomIn_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.ZoomIn();
            e.Handled = true;
        }

        private void ZoomOut_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.ZoomOut();
            e.Handled = true;
        }

        private void ZoomFit_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.Fit();
            e.Handled = true;
        }

        private void ZoomActualSize_Invoked(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            if (IsTextInputFocused()) return;
            CurrentHost?.CanvasActualSize();
            e.Handled = true;
        }
    }
}
