using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Core.Tools;
using Windows.System;

namespace PixlPunkt.UI
{
    public sealed partial class PixlPunktMainWindow : Window
    {
        private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Space for pan override
            if (e.Key == VirtualKey.Space)
            {
                CurrentHost?.BeginSpacePan();
                e.Handled = true;
                return;
            }

            // Track modifier state
            if (e.Key == VirtualKey.Shift) _shiftDown = true;
            if (e.Key == VirtualKey.Control) _ctrlDown = true;

            // Skip tool shortcuts if text input is focused or accelerators are suspended
            if (IsTextInputFocused() || _suspendToolAccelerators)
            {
                return;
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
            // ─────────────────────────────────────────────────────────────────
            if (CurrentHost?.HasSelection == true)
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
            bool ctrl = _ctrlDown || (e.KeyStatus.IsMenuKeyDown == false &&
                        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) &
                         Windows.UI.Core.CoreVirtualKeyStates.Down) != 0);
            bool shift = _shiftDown ||
                        (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) &
                         Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
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
                CurrentHost?.EndSpacePan();
                e.Handled = true;
            }
            if (e.Key == VirtualKey.Shift) _shiftDown = false;
            if (e.Key == VirtualKey.Control) _ctrlDown = false;
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
