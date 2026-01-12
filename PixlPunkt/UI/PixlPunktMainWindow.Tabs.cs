using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Settings;
using PixlPunkt.UI.CanvasArea;
using PixlPunkt.UI.CanvasHost;
using PixlPunkt.UI.Dialogs;
using PixlPunkt.UI.Helpers;
using Windows.Graphics;
using Windows.Storage;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Partial class for tab and document management:
    /// - Tab creation and headers
    /// - Document window detachment
    /// - Host attachment/detachment
    /// - Tab event handlers
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        //////////////////////////////////////////////////////////////////
        // DETACHED DOCUMENT WINDOWS
        //////////////////////////////////////////////////////////////////

        private readonly Dictionary<CanvasDocument, DocumentWindow> _docWindows = [];

        //////////////////////////////////////////////////////////////////
        // TAB CREATION
        //////////////////////////////////////////////////////////////////

        private TabViewItem MakeTab(CanvasDocument doc)
        {
            var host = new CanvasViewHost(doc);
            // Determine effective theme: prefer runtime-forced value, fall back to persisted setting, then to system
            var effectiveTheme = _stripeForcedTheme ?? (AppSettings.Instance.StripeTheme != StripeThemeChoice.System
                ? (AppSettings.Instance.StripeTheme == StripeThemeChoice.Light ? ElementTheme.Light : ElementTheme.Dark)
                : Root.ActualTheme);
            host.UpdateTransparencyPatternForTheme(effectiveTheme);
            var tab = new TabViewItem { Content = host };
            tab.Header = MakeTabHeader(doc, tab);

            var flyout = new MenuFlyout();
            var miDetach = new MenuFlyoutItem { Text = "Open in New Window" };
            miDetach.Click += (_, __) => DetachTabToWindow(tab, doc);
            var miDuplicate = new MenuFlyoutItem { Text = "Duplicate in New Window" };
            miDuplicate.Click += (_, __) => OpenDocInWindow(doc);
            flyout.Items.Add(miDetach);
            flyout.Items.Add(miDuplicate);
            tab.ContextFlyout = flyout;
            return tab;
        }

        private Grid MakeTabHeader(CanvasDocument doc, TabViewItem ownerTab)
        {
            var grid = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = doc.Name ?? "Canvas",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(title, 0);

            var tearBtn = new Button
            {
                Content = "?",
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ButtonRevealStyle"]
            };
            ToolTipService.SetToolTip(tearBtn, "Open in new window");
            tearBtn.Click += (_, __) => DetachTabToWindow(ownerTab, doc);
            tearBtn.Content = new FluentIcon { Icon = Icon.Open };
            Grid.SetColumn(tearBtn, 1);

            grid.Children.Add(title);
            grid.Children.Add(tearBtn);
            return grid;
        }

        //////////////////////////////////////////////////////////////////
        // DOCUMENT WINDOW MANAGEMENT
        //////////////////////////////////////////////////////////////////

        private void OpenDocInWindow(CanvasDocument doc)
        {
            if (_docWindows.TryGetValue(doc, out var existing))
            {
                existing.Activate();
                return;
            }

            var win = new DocumentWindow(doc, _palette, _toolState);
            // Apply current forced/system stripe theme to detached document window host
            try
            {
                var effective = _stripeForcedTheme ?? Root.ActualTheme;
                win.Host.UpdateTransparencyPatternForTheme(effective);
                if (_stripeForcedTheme != null)
                {
                    // if forced, also set the host control theme so controls match
                    win.Host.RequestedTheme = _stripeForcedTheme.Value;
                }
            }
            catch { }
            _docWindows[doc] = win;
            void OnWindowClosed(object s, WindowEventArgs args)
            {
                _docWindows.Remove(doc);
                win.Closed -= OnWindowClosed;
            }
            win.Closed += OnWindowClosed;
            win.Activate();
        }

        private void DetachTabToWindow(TabViewItem tab, CanvasDocument doc)
        {
            DocsTab.TabItems.Remove(tab);
            OpenDocInWindow(doc);
            if (DocsTab.TabItems.Count == 0)
            {
                LayersPanel.Bind(null);
                TilePanel.Bind(null, null);
                AnimationPanel.Bind(null);
            }
        }

        //////////////////////////////////////////////////////////////////
        // TAB EVENT HANDLERS
        //////////////////////////////////////////////////////////////////

        private async void DocsTab_AddTabButtonClick(TabView sender, object args)
        {
            var dlg = new NewCanvasDialog { XamlRoot = Content.XamlRoot };
            var res = await ShowDialogGuardedAsync(dlg);
            if (res == ContentDialogResult.Primary)
            {
                var result = dlg.GetResult();
                CreateAndOpenCanvas(result);
            }
            UpdateAddButtonOffset(DocsTab);
        }

        /// <summary>
        /// Creates and opens a canvas from a NewCanvasResult.
        /// Handles special templates like BrushCanvasTemplate.
        /// </summary>
        private void CreateAndOpenCanvas(NewCanvasResult result)
        {
            // Ensure the app-wide stripe/theme choice is applied before creating a new host
            try
            {
                SetStripeTheme(AppSettings.Instance.StripeTheme);
            }
            catch { }

            // Generate unique name if using default "NewCanvas" name
            string canvasName = result.Name;
            if (string.IsNullOrWhiteSpace(canvasName) || canvasName == "NewCanvas")
            {
                canvasName = $"NewCanvas{_newCanvasCounter++}";
            }

            int pxW = result.TileSize.Width * result.TileCounts.Width;
            int pxH = result.TileSize.Height * result.TileCounts.Height;
            var doc = new CanvasDocument(canvasName, pxW, pxH, result.TileSize, result.TileCounts);

            // For brush templates, we use a single layer (16x16 canvas)
            // No special layer setup needed - default layer works

            // Apply animation defaults from app settings
            doc.CanvasAnimationState.ApplyDefaults();
            doc.TileAnimationState.ApplyDefaults();

            _workspace.Add(doc);
            _documentPaths[doc] = null;

            // Register document for auto-save
            _autoSave.RegisterDocument(doc);

            var tab = MakeTab(doc);
            DocsTab.TabItems.Add(tab);

            // Defer selection to ensure tab is fully in visual tree (fixes Release build timing issue)
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                DocsTab.SelectedItem = tab;
            });
        }

        /// <summary>
        /// Legacy overload for backward compatibility.
        /// </summary>
        private void CreateAndOpenCanvas(string name, SizeInt32 tileSize, SizeInt32 tileCounts)
        {
            CreateAndOpenCanvas(new NewCanvasResult(name, tileSize, tileCounts, null));

            // Update session state when new document is created
            UpdateSessionState();
        }

        private void DocsTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.RemovedItems.Count > 0 &&
                e.RemovedItems[0] is TabViewItem oldTab &&
                oldTab.Content is CanvasViewHost oldHost)
            {
                oldHost.CommitSelection();
                DetachHost(oldHost);
            }

            if (DocsTab.SelectedItem is TabViewItem newTab &&
                newTab.Content is CanvasViewHost newHost)
            {
                AttachHost(newHost);
            }
            else
            {
                CurrentHost = null;
                LayersPanel.Bind(null);
                TilePanel.Bind(null, null);
                AnimationPanel.Bind(null);
            }
            UpdateHistoryUI();

            // Update session state when active document changes
            UpdateSessionState();
        }

        private async void DocsTab_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Item is not TabViewItem tab || tab.Content is not CanvasViewHost host)
                return;

            var doc = host.Document;

            // Check for unsaved changes
            if (doc.IsDirty)
            {
                var result = await PromptSaveChangesAsync(doc);

                if (result == SaveChangesResult.Cancel)
                {
                    // User cancelled - don't close the tab
                    return;
                }

                if (result == SaveChangesResult.Save)
                {
                    // Try to save the document
                    var saved = await TrySaveDocumentAsync(doc);
                    if (!saved)
                    {
                        // Save failed or cancelled - don't close the tab
                        return;
                    }
                }
                // SaveChangesResult.DontSave - proceed with closing without saving
            }

            // Proceed with closing the tab
            _autoSave.UnregisterDocument(doc);
            _documentPaths.Remove(doc);
            _workspace.Close(doc);

            // Clean up auto-save tracking for session state
            OnDocumentClosed(doc);

            sender.TabItems.Remove(args.Item);

            if (DocsTab.TabItems.Count == 0)
            {
                LayersPanel.Bind(null);
                TilePanel.Bind(null, null);
                AnimationPanel.Bind(null);
            }
            UpdateAddButtonOffset(DocsTab);

            // Update session state when document is closed
            UpdateSessionState();
        }

        //////////////////////////////////////////////////////////////////
        // UNSAVED CHANGES PROMPTS
        //////////////////////////////////////////////////////////////////

        private enum SaveChangesResult
        {
            Save,
            DontSave,
            Cancel
        }

        /// <summary>
        /// Prompts the user to save changes for a single document.
        /// </summary>
        private async Task<SaveChangesResult> PromptSaveChangesAsync(CanvasDocument doc)
        {
            var dlg = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Unsaved Changes",
                Content = $"\"{doc.Name ?? "Untitled"}\" has unsaved changes.\n\nDo you want to save before closing?",
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Don't Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await ShowDialogGuardedAsync(dlg);

            return result switch
            {
                ContentDialogResult.Primary => SaveChangesResult.Save,
                ContentDialogResult.Secondary => SaveChangesResult.DontSave,
                _ => SaveChangesResult.Cancel
            };
        }

        /// <summary>
        /// Attempts to save a document, using Save As if no path exists.
        /// Returns true if save succeeded, false if cancelled or failed.
        /// </summary>
        private async Task<bool> TrySaveDocumentAsync(CanvasDocument doc)
        {
            var existingPath = _documentPaths.TryGetValue(doc, out var p) ? p : null;

            if (string.IsNullOrEmpty(existingPath))
            {
                // No existing path - need Save As dialog
                return await TrySaveDocumentAsAsync(doc);
            }

            // Save to existing path
            try
            {
                DocumentIO.Save(doc, existingPath);
                doc.MarkSaved();
                TrackRecent(existingPath);  // Track in recent documents
                return true;
            }
            catch
            {
                // Save failed
                return false;
            }
        }

        /// <summary>
        /// Shows Save As dialog for a document.
        /// Returns true if save succeeded, false if cancelled or failed.
        /// </summary>
        private async Task<bool> TrySaveDocumentAsAsync(CanvasDocument doc)
        {
            var savePicker = WindowHost.CreateFileSavePicker(this, doc.Name ?? "Untitled", ".pxp");

            var file = await savePicker.PickSaveFileAsync();
            if (file is null)
                return false;

            try
            {
                using var stream = await file.OpenStreamForWriteAsync();
                stream.SetLength(0);
                DocumentIO.Save(doc, stream);
                await stream.FlushAsync();

                _documentPaths[doc] = file.Path;
                doc.MarkSaved();
                doc.Name = System.IO.Path.GetFileNameWithoutExtension(file.Path);
                UpdateTabHeaderForDocument(doc);
                TrackRecent(file.Path);  // Track in recent documents
                return true;
            }
            catch
            {
                return false;
            }
        }

        //////////////////////////////////////////////////////////////////
        // CANVAS HOST ATTACH / DETACH
        //////////////////////////////////////////////////////////////////

        private void AttachHost(CanvasViewHost host)
        {
            if (CurrentHost != null)
                CurrentHost.ForegroundSampledLive -= OnHostForegroundSampledLive;

            CurrentHost = host;
            CurrentHost.BindToolState(_toolState, _palette);
            CurrentHost.ForegroundSampledLive += OnHostForegroundSampledLive;
            CurrentHost.BackgroundSampledLive += OnHostBackgroundSampledLive;
            CurrentHost.HistoryStateChanged += Host_HistoryStateChanged;

            PreviewControl?.Attach(CurrentHost);
            HistoryPanel.Bind(CurrentHost);
            LayersPanel.Bind(host.Document);
            TilePanel.Bind(host.Document, _toolState, _palette);
            AnimationPanel.Bind(host.Document);
            AnimationPanel.BindToolState(_toolState, _palette);
            AnimationPanel.BindCanvasHost(host);

            // Wire up text input focus events for shortcut suppression
            AnimationPanel.TextInputFocused -= OnAnimationPanelTextInputFocused;
            AnimationPanel.TextInputUnfocused -= OnAnimationPanelTextInputUnfocused;
            AnimationPanel.TextInputFocused += OnAnimationPanelTextInputFocused;
            AnimationPanel.TextInputUnfocused += OnAnimationPanelTextInputUnfocused;

            // Wire up animation panel interaction tracking (for keyboard shortcut routing)
            AnimationPanel.Interacted -= OnAnimationPanelInteracted;
            AnimationPanel.Interacted += OnAnimationPanelInteracted;

            // Wire up canvas host interaction tracking (clears animation panel focus)
            host.CanvasInteracted -= OnCanvasInteracted;
            host.CanvasInteracted += OnCanvasInteracted;

            // Wire up canvas animation frame change to refresh the main canvas
            AnimationPanel.CanvasAnimationFrameChanged -= OnCanvasAnimationFrameChanged;
            AnimationPanel.CanvasAnimationFrameChanged += OnCanvasAnimationFrameChanged;

            // Wire up canvas animation layer selection (bidirectional sync with LayersPanel)
            AnimationPanel.CanvasLayerSelected -= OnCanvasAnimationLayerSelected;
            AnimationPanel.CanvasLayerSelected += OnCanvasAnimationLayerSelected;

            // Wire up animation mode changes (to show/hide stage overlay)
            AnimationPanel.ModeChanged -= OnAnimationModeChanged;
            AnimationPanel.ModeChanged += OnAnimationModeChanged;

            // Wire up stage selection (for stage dragging on canvas)
            AnimationPanel.StageSelectionChanged -= OnStageSelectionChanged;
            AnimationPanel.StageSelectionChanged += OnStageSelectionChanged;

            // Set initial animation mode on host
            host.SetAnimationMode(AnimationPanel.CurrentMode);

            // Provide access to all open documents for tile import feature
            TilePanel.OpenDocumentsProvider = () => _workspace.Documents;

            // Unsubscribe previous handler to prevent accumulation
            if (_layersPanelSelectionHandler != null)
            {
                LayersPanel.SelectionChangedUiIndex -= _layersPanelSelectionHandler;
            }

            // Create and store new handler for this host
            _layersPanelSelectionHandler = internalIndex =>
            {
                host.Document.SetActiveLayer(internalIndex);
                // Sync selection to AnimationPanel
                AnimationPanel.OnActiveLayerChanged();
            };
            LayersPanel.SelectionChangedUiIndex += _layersPanelSelectionHandler;

            // Also sync when document's active layer changes (from any source)
            host.Document.ActiveLayerChanged -= OnDocumentActiveLayerChanged;
            host.Document.ActiveLayerChanged += OnDocumentActiveLayerChanged;

            UpdateHistoryUI();
        }

        private void DetachHost(CanvasViewHost host)
        {
            host.ForegroundSampledLive -= OnHostForegroundSampledLive;
            host.BackgroundSampledLive -= OnHostBackgroundSampledLive;
            host.HistoryStateChanged -= Host_HistoryStateChanged;
            host.Document.ActiveLayerChanged -= OnDocumentActiveLayerChanged;
            host.CanvasInteracted -= OnCanvasInteracted;

            // Unsubscribe layer selection handler to prevent memory leak
            if (_layersPanelSelectionHandler != null)
            {
                LayersPanel.SelectionChangedUiIndex -= _layersPanelSelectionHandler;
                _layersPanelSelectionHandler = null;
            }

            // Unsubscribe animation panel text input focus events
            AnimationPanel.TextInputFocused -= OnAnimationPanelTextInputFocused;
            AnimationPanel.TextInputUnfocused -= OnAnimationPanelTextInputUnfocused;

            // Unsubscribe animation panel interaction tracking
            AnimationPanel.Interacted -= OnAnimationPanelInteracted;

            // Unsubscribe canvas animation frame change event
            AnimationPanel.CanvasAnimationFrameChanged -= OnCanvasAnimationFrameChanged;

            // Unsubscribe canvas animation layer selection
            AnimationPanel.CanvasLayerSelected -= OnCanvasAnimationLayerSelected;

            // Unsubscribe animation mode and stage selection events
            AnimationPanel.ModeChanged -= OnAnimationModeChanged;
            AnimationPanel.StageSelectionChanged -= OnStageSelectionChanged;

            if (CurrentHost == host)
                CurrentHost = null;

            PreviewControl?.Detach();
            LayersPanel.Bind(null);
            TilePanel.Bind(null, null, null);
            AnimationPanel.Bind(null);
        }

        private void OnHostForegroundSampledLive(uint bgra) => PalettePanel.Service?.SetForeground(bgra);
        private void OnHostBackgroundSampledLive(uint bgra) => PalettePanel.Service?.SetBackground(bgra);

        /// <summary>
        /// Called when canvas animation frame changes - refreshes the main canvas to show the new frame.
        /// </summary>
        private void OnCanvasAnimationFrameChanged(int frameIndex)
        {
            // Recomposite and invalidate the canvas to show the updated layer states
            CurrentHost?.Document?.CompositeTo(CurrentHost.Document.Surface);
            CurrentHost?.InvalidateCanvas();
        }

        /// <summary>
        /// Called when a layer is selected in the canvas animation timeline.
        /// Syncs the selection to the document and LayersPanel.
        /// </summary>
        private void OnCanvasAnimationLayerSelected(Guid layerId)
        {
            if (CurrentHost?.Document == null) return;

            var doc = CurrentHost.Document;

            // Find the layer by ID and set it as active
            var flattenedLayers = doc.GetFlattenedLayers();
            for (int i = 0; i < flattenedLayers.Count; i++)
            {
                if (flattenedLayers[i].Id == layerId)
                {
                    // Use internal index (GetFlattenedLayers returns bottom-to-top order)
                    doc.SetActiveLayer(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Called when the document's active layer changes from any source.
        /// Syncs the selection to the AnimationPanel.
        /// </summary>
        private void OnDocumentActiveLayerChanged()
        {
            AnimationPanel.OnActiveLayerChanged();
        }

        /// <summary>
        /// Called when animation mode changes (Tile vs Canvas).
        /// Updates the canvas host to show/hide stage overlay.
        /// </summary>
        private void OnAnimationModeChanged(Animation.AnimationMode mode)
        {
            CurrentHost?.SetAnimationMode(mode);
        }

        /// <summary>
        /// Called when stage selection changes in the animation timeline.
        /// Updates the canvas host to enable/disable stage interaction.
        /// </summary>
        private void OnStageSelectionChanged(bool selected)
        {
            CurrentHost?.SetStageSelected(selected);
        }

        /// <summary>
        /// Suspends tool shortcuts when text input in animation panel gets focus.
        /// </summary>
        private void OnAnimationPanelTextInputFocused() => SuspendToolAccelerators(true);

        /// <summary>
        /// Restores tool shortcuts when text input in animation panel loses focus.
        /// </summary>
        private void OnAnimationPanelTextInputUnfocused() => SuspendToolAccelerators(false);

        /// <summary>
        /// Called when the user interacts with the animation panel.
        /// Sets focus mode so keyboard shortcuts control animation.
        /// </summary>
        private void OnAnimationPanelInteracted()
        {
            SetAnimationPanelFocus(true);
        }

        /// <summary>
        /// Called when the user interacts with the canvas.
        /// Clears animation panel focus so keyboard shortcuts control pan/selection.
        /// </summary>
        private void OnCanvasInteracted()
        {
            SetAnimationPanelFocus(false);
        }
    }
}
