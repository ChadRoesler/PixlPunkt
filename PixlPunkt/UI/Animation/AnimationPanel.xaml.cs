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
    /// <summary>
    /// Main animation panel that hosts both Tile and Canvas animation modes.
    /// Docked at the bottom of the main window.
    /// </summary>
    public sealed partial class AnimationPanel : UserControl
    {
        // ====================================================================
        // CONSTANTS
        // ====================================================================

        private const int CellWidth = 24;
        private const int CellHeight = 24;
        private const int KeyframeDiamondSize = 10;

        // ====================================================================
        // FIELDS
        // ====================================================================

        private CanvasDocument? _document;
        private TileAnimationState? _tileAnimationState;
        private CanvasAnimationState? _canvasAnimationState;
        private ToolState? _toolState;
        private PaletteService? _palette;
        private bool _suppressCanvasValueChanges;
        private bool _isDraggingPlayhead;
        private Guid _selectedLayerId;
        private bool _suppressLayerSelection;

        // Audio track dragging
        private bool _isDraggingAudioTrack;
        private int _audioDragStartFrame;
        private int _audioDragStartOffset;
        private int _audioDragTrackIndex = -1;

        // Canvas host reference for direct invalidation during scrubbing
        private CanvasHost.CanvasViewHost? _canvasHost;

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets the current animation mode.
        /// </summary>
        public AnimationMode CurrentMode { get; private set; } = AnimationMode.Tile;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when the user selects a frame and wants to navigate to it on the canvas.
        /// </summary>
        public event Action<int>? FrameSelected;

        /// <summary>
        /// Raised when animation mode changes.
        /// </summary>
        public event Action<AnimationMode>? ModeChanged;

        /// <summary>
        /// Raised when a tile has been modified via the frame editor.
        /// The main canvas should refresh to reflect the changes.
        /// </summary>
        public event Action? TileModified;

        /// <summary>
        /// Raised when a text input gains focus (to suppress tool shortcuts).
        /// </summary>
        public event Action? TextInputFocused;

        /// <summary>
        /// Raised when a text input loses focus (to restore tool shortcuts).
        /// </summary>
        public event Action? TextInputUnfocused;

        /// <summary>
        /// Raised when canvas animation frame changes (for layer state application).
        /// </summary>
        public event Action<int>? CanvasAnimationFrameChanged;

        /// <summary>
        /// Raised when a layer is selected in the canvas animation timeline.
        /// Parameter is the layer's Guid.
        /// </summary>
        public event Action<Guid>? CanvasLayerSelected;

        /// <summary>
        /// Raised when the Stage track is selected or deselected.
        /// Parameter is true when selected, false when deselected.
        /// </summary>
        public event Action<bool>? StageSelectionChanged;

        /// <summary>
        /// Raised when the user clicks the undock button for the animation preview.
        /// </summary>
        public event Action? AnimationPreviewUndockRequested;

        /// <summary>
        /// Raised when the user wants to dock the animation preview back.
        /// </summary>
        public event Action? AnimationPreviewDockRequested;

        /// <summary>
        /// Raised when the user interacts with the animation panel (clicks, drags, etc.).
        /// Used to track focus for keyboard shortcut routing.
        /// </summary>
        public event Action? Interacted;

        /// <summary>
        /// Gets whether the Stage track is currently selected.
        /// </summary>
        public bool IsStageSelected { get; private set; }

        /// <summary>
        /// Gets or sets whether the animation preview is currently floating (undocked).
        /// </summary>
        public bool IsAnimationPreviewFloating
        {
            get => _isAnimationPreviewFloating;
            set
            {
                if (_isAnimationPreviewFloating != value)
                {
                    _isAnimationPreviewFloating = value;
                    UpdateAnimationPreviewUndockButton();
                }
            }
        }
        private bool _isAnimationPreviewFloating;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public AnimationPanel()
        {
            InitializeComponent();

            // Wire up frame editor tile modification events
            FrameEditor.TileModified += OnFrameEditorTileModified;

            // Wire up text input focus events from child panels
            ReelList.TextInputFocused += () => TextInputFocused?.Invoke();
            ReelList.TextInputUnfocused += () => TextInputUnfocused?.Invoke();

            // Wire up stage preview expand toggle
            StagePreview.ExpandRequested += OnStagePreviewExpandRequested;
        }

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        /// <summary>
        /// Binds the animation panel to a document.
        /// </summary>
        /// <param name="document">The document to bind, or null to unbind.</param>
        public void Bind(CanvasDocument? document)
        {
            // Unbind previous tile animation state
            if (_tileAnimationState != null)
            {
                _tileAnimationState.SelectedReelChanged -= OnSelectedReelChanged;
                _tileAnimationState.CurrentFrameChanged -= OnTileCurrentFrameChanged;
            }

            // Unbind previous canvas animation state
            if (_canvasAnimationState != null)
            {
                _canvasAnimationState.CurrentFrameChanged -= OnCanvasCurrentFrameChanged;
                _canvasAnimationState.PlaybackStateChanged -= OnCanvasPlaybackStateChanged;
                _canvasAnimationState.TracksChanged -= OnCanvasTracksChanged;
                _canvasAnimationState.KeyframeChanged -= OnCanvasKeyframeChanged;
                _canvasAnimationState.FrameCountChanged -= OnCanvasFrameCountChanged;
                _canvasAnimationState.FpsChanged -= OnCanvasFpsChanged;
                _canvasAnimationState.StageSettingsChanged -= OnStageSettingsChanged;
                _canvasAnimationState.AudioTracksChanged -= OnAudioTracksChanged;
            }

            // Unbind previous document structure events
            if (_document != null)
            {
                _document.StructureChanged -= OnDocumentStructureChanged;
                _document.LayersChanged -= OnDocumentLayersChanged;
            }

            _document = document;
            _tileAnimationState = document?.TileAnimationState;
            _canvasAnimationState = document?.CanvasAnimationState;

            // Bind new tile animation state
            if (_tileAnimationState != null)
            {
                _tileAnimationState.SelectedReelChanged += OnSelectedReelChanged;
                _tileAnimationState.CurrentFrameChanged += OnTileCurrentFrameChanged;
            }

            // Bind new canvas animation state
            if (_canvasAnimationState != null)
            {
                _canvasAnimationState.CurrentFrameChanged += OnCanvasCurrentFrameChanged;
                _canvasAnimationState.PlaybackStateChanged += OnCanvasPlaybackStateChanged;
                _canvasAnimationState.TracksChanged += OnCanvasTracksChanged;
                _canvasAnimationState.KeyframeChanged += OnCanvasKeyframeChanged;
                _canvasAnimationState.FrameCountChanged += OnCanvasFrameCountChanged;
                _canvasAnimationState.FpsChanged += OnCanvasFpsChanged;
                _canvasAnimationState.StageSettingsChanged += OnStageSettingsChanged;
                _canvasAnimationState.AudioTracksChanged += OnAudioTracksChanged;
            }

            // Bind document structure events (for layer add/remove/reorder)
            if (_document != null)
            {
                _document.StructureChanged += OnDocumentStructureChanged;
                _document.LayersChanged += OnDocumentLayersChanged;
            }

            // Update child panels - Tile Animation
            ReelList.Bind(_tileAnimationState);
            FrameEditor.Bind(_tileAnimationState, _document);
            Playback.Bind(_tileAnimationState, _document);

            // Update child panels - Canvas Animation
            StagePreview.Bind(_document, _canvasAnimationState);

            // If we're currently in Canvas Animation mode, sync the tracks from the new document
            // This ensures layers appear immediately when switching documents while in Canvas mode
            if (CurrentMode == AnimationMode.Canvas && _document != null && _canvasAnimationState != null)
            {
                _canvasAnimationState.SyncTracksFromDocument(_document);
            }

            // Update canvas animation UI
            RefreshCanvasAnimationUI();
        }

        /// <summary>
        /// Binds the tool state and palette service for tool integration.
        /// </summary>
        public void BindToolState(ToolState? toolState, PaletteService? palette)
        {
            _toolState = toolState;
            _palette = palette;

            // Pass to frame editor for tool integration
            FrameEditor.BindToolState(_toolState, _palette);
        }

        /// <summary>
        /// Binds the canvas host for synchronization with the main canvas.
        /// </summary>
        public void BindCanvasHost(CanvasHost.CanvasViewHost? canvasHost)
        {
            _canvasHost = canvasHost;
            FrameEditor.BindCanvasHost(canvasHost);
        }

        /// <summary>
        /// Sets the foreground color for the frame editor.
        /// </summary>
        public void SetForegroundColor(uint bgra)
        {
            FrameEditor.SetForegroundColor(bgra);
        }

        /// <summary>
        /// Sets the background color for the frame editor.
        /// </summary>
        public void SetBackgroundColor(uint bgra)
        {
            FrameEditor.SetBackgroundColor(bgra);
        }

        /// <summary>
        /// Stops any active playback.
        /// </summary>
        public void StopPlayback()
        {
            _tileAnimationState?.Stop();
            _canvasAnimationState?.Stop();
        }

        /// <summary>
        /// Refreshes the frame editor to reflect tile changes.
        /// </summary>
        public void RefreshCurrentFrame()
        {
            FrameEditor.RefreshDisplay();
            Playback.RefreshDisplay();
        }

        /// <summary>
        /// Syncs canvas animation tracks with the document's layer structure.
        /// Call this after layer structure changes (add, remove, reorder).
        /// </summary>
        public void SyncCanvasAnimationTracks()
        {
            if (_document != null && _canvasAnimationState != null)
            {
                _canvasAnimationState.SyncTracksFromDocument(_document);
            }
            RefreshCanvasAnimationUI();
        }

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

        // ====================================================================
        // CANVAS ANIMATION UI REFRESH
        // ====================================================================

        private void RefreshCanvasAnimationUI()
        {
            RefreshCanvasToolbar();
            RefreshCanvasFrameNumbers();
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
            UpdateCanvasPlayhead();
        }

        private void RefreshCanvasToolbar()
        {
            if (_canvasAnimationState == null) return;

            _suppressCanvasValueChanges = true;
            CanvasFrameNumberBox.Value = _canvasAnimationState.CurrentFrameIndex;
            CanvasFrameNumberBox.Maximum = _canvasAnimationState.FrameCount - 1;
            CanvasTotalFramesBox.Value = _canvasAnimationState.FrameCount;
            CanvasFpsBox.Value = _canvasAnimationState.FramesPerSecond;
            CanvasLoopToggle.IsChecked = _canvasAnimationState.Loop;
            CanvasOnionSkinToggle.IsChecked = _canvasAnimationState.OnionSkinEnabled;
            StageEnabledToggle.IsChecked = _canvasAnimationState.Stage.Enabled;
            UpdateCanvasPlayPauseIcon();
            UpdateStageQuickButtonVisibility();
            _suppressCanvasValueChanges = false;
        }

        private void RefreshCanvasFrameNumbers()
        {
            CanvasFrameNumbersPanel.Children.Clear();
            if (_canvasAnimationState == null) return;

            for (int i = 0; i < _canvasAnimationState.FrameCount; i++)
            {
                var border = new Border
                {
                    Width = CellWidth,
                    Height = 28,
                    BorderBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.3 },
                    BorderThickness = new Thickness(0, 0, 1, 1)
                };
                var text = new TextBlock
                {
                    Text = i.ToString(),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.7
                };
                border.Child = text;
                CanvasFrameNumbersPanel.Children.Add(border);
            }
        }

        private void RefreshCanvasLayerNames()
        {
            CanvasLayerNamesPanel.Children.Clear();
            if (_canvasAnimationState == null) return;

            // Get the active layer's ID from the document for highlighting
            Guid activeLayerId = _document?.ActiveLayer?.Id ?? Guid.Empty;

            // Add Audio tracks section if there are any loaded tracks
            if (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0)
            {
                // Add collapsible header for audio tracks
                var audioHeaderBorder = new Border
                {
                    Height = CellHeight,
                    BorderBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.3 },
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(4, 0, 4, 0),
                    Background = new SolidColorBrush(Color.FromArgb(60, 0, 200, 200))
                };

                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

                // Collapse/Expand toggle
                var collapseIcon = new FluentIcons.WinUI.FluentIcon
                {
                    Icon = _canvasAnimationState.AudioTracks.IsCollapsed
                        ? FluentIcons.Common.Icon.ChevronRight
                        : FluentIcons.Common.Icon.ChevronDown,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var headerText = new TextBlock
                {
                    Text = $"🔊 Audio ({_canvasAnimationState.AudioTracks.LoadedCount})",
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 200, 200))
                };

                // Add track button
                var addButton = new Button
                {
                    Content = "+",
                    FontSize = 10,
                    Padding = new Thickness(4, 0, 4, 0),
                    Background = new SolidColorBrush(Colors.Transparent),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                addButton.Click += AudioAddTrack_Click;

                headerPanel.Children.Add(collapseIcon);
                headerPanel.Children.Add(headerText);
                headerPanel.Children.Add(addButton);

                audioHeaderBorder.Child = headerPanel;
                audioHeaderBorder.Tag = "AudioHeader";
                audioHeaderBorder.PointerPressed += AudioHeader_PointerPressed;
                CanvasLayerNamesPanel.Children.Add(audioHeaderBorder);

                // Add individual audio tracks if not collapsed
                if (!_canvasAnimationState.AudioTracks.IsCollapsed)
                {
                    int trackIndex = 0;
                    foreach (var audioTrack in _canvasAnimationState.AudioTracks)
                    {
                        if (audioTrack.IsLoaded)
                        {
                            var audioBorder = new Border
                            {
                                Height = CellHeight,
                                BorderBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.3 },
                                BorderThickness = new Thickness(0, 0, 1, 1),
                                Padding = new Thickness(20, 0, 4, 0), // Indent for hierarchy
                                Background = new SolidColorBrush(Color.FromArgb(30, 0, 200, 200))
                            };

                            var trackPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

                            var audioText = new TextBlock
                            {
                                Text = audioTrack.Settings.DisplayName,
                                FontSize = 11,
                                VerticalAlignment = VerticalAlignment.Center,
                                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 200, 200)),
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                MaxWidth = 100
                            };

                            // Mute button for this track
                            var muteButton = new Button
                            {
                                Content = new FluentIcons.WinUI.FluentIcon
                                {
                                    Icon = audioTrack.Settings.Muted
                                        ? FluentIcons.Common.Icon.SpeakerMute
                                        : FluentIcons.Common.Icon.Speaker2,
                                    FontSize = 10
                                },
                                Padding = new Thickness(2),
                                Background = new SolidColorBrush(Colors.Transparent),
                                VerticalAlignment = VerticalAlignment.Center,
                                Tag = trackIndex
                            };
                            muteButton.Click += AudioTrackMute_Click;

                            // Remove button
                            var removeButton = new Button
                            {
                                Content = new FluentIcons.WinUI.FluentIcon
                                {
                                    Icon = FluentIcons.Common.Icon.Delete,
                                    FontSize = 10
                                },
                                Padding = new Thickness(2),
                                Background = new SolidColorBrush(Colors.Transparent),
                                VerticalAlignment = VerticalAlignment.Center,
                                Tag = trackIndex
                            };
                            removeButton.Click += AudioTrackRemove_Click;

                            trackPanel.Children.Add(audioText);
                            trackPanel.Children.Add(muteButton);
                            trackPanel.Children.Add(removeButton);

                            audioBorder.Child = trackPanel;
                            audioBorder.Tag = $"AudioTrack:{trackIndex}";
                            audioBorder.PointerPressed += AudioTrack_PointerPressed;
                            CanvasLayerNamesPanel.Children.Add(audioBorder);
                        }
                        trackIndex++;
                    }
                }
            }

            // Add Stage track if enabled
            if (_canvasAnimationState.Stage.Enabled)
            {
                var stageBorder = new Border
                {
                    Height = CellHeight,
                    BorderBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.3 },
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(4, 0, 4, 0),
                    Background = IsStageSelected
                        ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                        : new SolidColorBrush(Color.FromArgb(40, 255, 165, 0)) // Orange tint for stage
                };

                var stageText = new TextBlock
                {
                    Text = "📷 Stage",
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = IsStageSelected
                        ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                        : new SolidColorBrush(Colors.Orange)
                };

                stageBorder.Child = stageText;
                stageBorder.Tag = "Stage";
                stageBorder.PointerPressed += StageTrack_PointerPressed;
                stageBorder.PointerEntered += StageTrack_PointerEntered;
                stageBorder.PointerExited += StageTrack_PointerExited;
                CanvasLayerNamesPanel.Children.Add(stageBorder);
            }

            foreach (var track in _canvasAnimationState.Tracks)
            {
                bool isSelected = track.LayerId == activeLayerId;

                var border = new Border
                {
                    Height = CellHeight,
                    BorderBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.3 },
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(4 + track.Depth * 16, 0, 4, 0),
                    Background = isSelected
                        ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                        : new SolidColorBrush(Colors.Transparent),
                    Tag = track.LayerId
                };

                var text = new TextBlock
                {
                    Text = track.LayerName,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isSelected
                        ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                        : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                };

                border.Child = text;
                border.PointerPressed += CanvasLayerName_PointerPressed;
                border.PointerEntered += CanvasLayerName_PointerEntered;
                border.PointerExited += CanvasLayerName_PointerExited;

                CanvasLayerNamesPanel.Children.Add(border);
            }
        }

        private void AudioHeader_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.AudioTracks.ToggleCollapsed();
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
            UpdateCanvasPlayhead();
        }

        private void AudioAddTrack_Click(object sender, RoutedEventArgs e)
        {
            // Load a new audio file into a new track
            _ = AddNewAudioTrackAsync();
        }

        private async System.Threading.Tasks.Task AddNewAudioTrackAsync()
        {
            if (_canvasAnimationState == null) return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".ogg");
            picker.FileTypeFilter.Add(".flac");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".wma");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.PixlPunktMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var newTrack = _canvasAnimationState.AudioTracks.AddTrack();
                bool success = await newTrack.LoadAsync(file.Path);
                if (!success)
                {
                    _canvasAnimationState.AudioTracks.RemoveTrack(newTrack);
                }
                else
                {
                    RefreshCanvasLayerNames();
                    RefreshCanvasKeyframeGrid();
                }
            }
        }

        private void AudioTrackMute_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            if (sender is Button btn && btn.Tag is int trackIndex)
            {
                if (trackIndex >= 0 && trackIndex < _canvasAnimationState.AudioTracks.Count)
                {
                    var track = _canvasAnimationState.AudioTracks[trackIndex];
                    track.Settings.Muted = !track.Settings.Muted;
                    RefreshCanvasLayerNames();
                }
            }
        }

        private void AudioTrackRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            if (sender is Button btn && btn.Tag is int trackIndex)
            {
                if (trackIndex >= 0 && trackIndex < _canvasAnimationState.AudioTracks.Count)
                {
                    _canvasAnimationState.AudioTracks.RemoveTrackAt(trackIndex);
                    RefreshCanvasLayerNames();
                    RefreshCanvasKeyframeGrid();
                }
            }
        }

        // ====================================================================
        // EVENT HANDLERS - TILE ANIMATION
        // ====================================================================

        private void OnSelectedReelChanged(TileAnimationReel? reel)
        {
            // Child panels handle their own updates
        }

        private void OnTileCurrentFrameChanged(int frameIndex)
        {
            // Notify parent to highlight tile on canvas
            var (tileX, tileY) = _tileAnimationState?.CurrentTilePosition ?? (-1, -1);
            if (tileX >= 0 && tileY >= 0)
            {
                FrameSelected?.Invoke(frameIndex);
            }
        }

        private void OnFrameEditorTileModified()
        {
            // Bubble up to parent so main canvas can refresh
            TileModified?.Invoke();
        }

        // ====================================================================
        // EVENT HANDLERS - DOCUMENT STRUCTURE
        // ====================================================================

        private void OnDocumentStructureChanged()
        {
            // Sync canvas animation tracks when document layer structure changes
            // This ensures new layers appear in the timeline automatically
            if (CurrentMode == AnimationMode.Canvas)
            {
                SyncCanvasAnimationTracks();
            }
        }

        private void OnDocumentLayersChanged()
        {
            // Update track names when layer names change
            // LayersChanged fires for name changes (from CanvasDocument.Layer_PropertyChanged)
            if (CurrentMode == AnimationMode.Canvas && _document != null && _canvasAnimationState != null)
            {
                // Sync tracks to pick up name changes
                _canvasAnimationState.SyncTracksFromDocument(_document);
                RefreshCanvasLayerNames();
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - AUDIO TRACK STATE
        // ════════════════════════════════════════════════════════════════════

        private void OnAudioLoadedChanged(bool loaded)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateAudioUI();
            });
        }

        private void OnAudioWaveformUpdated()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshCanvasKeyframeGrid();
            });
        }

        private void OnAudioTracksChanged()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshCanvasLayerNames();
                RefreshCanvasKeyframeGrid();
                UpdateCanvasPlayhead();
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - CANVAS ANIMATION STATE
        // ════════════════════════════════════════════════════════════════════

        private void OnCanvasCurrentFrameChanged(int frameIndex)
        {
            _suppressCanvasValueChanges = true;
            CanvasFrameNumberBox.Value = frameIndex;
            _suppressCanvasValueChanges = false;
            UpdateCanvasPlayhead();

            // Apply frame state to document layers
            if (_document != null && _canvasAnimationState != null)
            {
                _canvasAnimationState.ApplyFrameToDocument(_document, frameIndex);
                CanvasAnimationFrameChanged?.Invoke(frameIndex);
            }

            // Notify canvas host of frame change (clears pending edits if navigating away)
            _canvasHost?.OnAnimationFrameChanged(frameIndex);

            // Also refresh the stage preview panel
            StagePreview.RefreshPreview();
        }

        private void OnCanvasPlaybackStateChanged(PlaybackState state)
        {
            UpdateCanvasPlayPauseIcon();
        }

        private void OnCanvasTracksChanged()
        {
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();
        }

        private void OnCanvasKeyframeChanged(CanvasAnimationTrack track, int frameIndex)
        {
            RefreshCanvasKeyframeGrid();
        }

        private void OnCanvasFrameCountChanged(int count)
        {
            _suppressCanvasValueChanges = true;
            CanvasTotalFramesBox.Value = count;
            CanvasFrameNumberBox.Maximum = count - 1;
            _suppressCanvasValueChanges = false;
            RefreshCanvasFrameNumbers();
            RefreshCanvasKeyframeGrid();
        }

        private void OnCanvasFpsChanged(int fps)
        {
            _suppressCanvasValueChanges = true;
            CanvasFpsBox.Value = fps;
            _suppressCanvasValueChanges = false;
        }

        private void OnStageSettingsChanged()
        {
            // Refresh the timeline UI when stage settings change (including Enabled)
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshCanvasLayerNames();
                RefreshCanvasKeyframeGrid();
                UpdateCanvasPlayhead();
            });
        }

        // ====================================================================
        // EVENT HANDLERS - CANVAS ANIMATION TOOLBAR
        // ====================================================================

        private void CanvasFirstFrame_Click(object sender, RoutedEventArgs e) => _canvasAnimationState?.FirstFrame();
        private void CanvasPrevFrame_Click(object sender, RoutedEventArgs e) => _canvasAnimationState?.PreviousFrame();
        private void CanvasPlayPause_Click(object sender, RoutedEventArgs e) => _canvasAnimationState?.TogglePlayPause();
        private void CanvasStop_Click(object sender, RoutedEventArgs e) => _canvasAnimationState?.Stop();
        private void CanvasNextFrame_Click(object sender, RoutedEventArgs e) => _canvasAnimationState?.NextFrame();
        private void CanvasLastFrame_Click(object sender, RoutedEventArgs e) => _canvasAnimationState?.LastFrame();

        private void CanvasFrameNumber_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressCanvasValueChanges || _canvasAnimationState == null) return;
            if (double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.SetCurrentFrame((int)args.NewValue);
        }

        private void CanvasTotalFrames_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressCanvasValueChanges || _canvasAnimationState == null) return;
            if (double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.FrameCount = (int)args.NewValue;
        }

        private void CanvasFps_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressCanvasValueChanges || _canvasAnimationState == null) return;
            if (double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.FramesPerSecond = (int)args.NewValue;
        }

        private void CanvasAddKeyframe_Click(object sender, RoutedEventArgs e)
        {
            if (_document == null || _canvasAnimationState == null) return;

            var activeLayer = _document.ActiveLayer;
            if (activeLayer != null)
            {
                _canvasAnimationState.CaptureKeyframe(activeLayer, _canvasAnimationState.CurrentFrameIndex);
                RefreshCanvasKeyframeGrid();
            }
        }

        private void CanvasRemoveKeyframe_Click(object sender, RoutedEventArgs e)
        {
            if (_document == null || _canvasAnimationState == null) return;

            var activeLayer = _document.ActiveLayer;
            if (activeLayer != null)
            {
                _canvasAnimationState.RemoveKeyframe(activeLayer, _canvasAnimationState.CurrentFrameIndex);
                RefreshCanvasKeyframeGrid();
            }
        }

        private void CanvasLoop_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.Loop = CanvasLoopToggle.IsChecked ?? false;
        }

        private void CanvasOnionSkin_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.OnionSkinEnabled = CanvasOnionSkinToggle.IsChecked ?? false;
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - AUDIO CONTROLS
        // ════════════════════════════════════════════════════════════════════

        private bool _suppressAudioValueChanges;

        private async void AudioLoad_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".ogg");
            picker.FileTypeFilter.Add(".flac");
            picker.FileTypeFilter.Add(".m4a");
            picker.FileTypeFilter.Add(".wma");

            // Get the window handle for the picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.PixlPunktMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                bool success = await _canvasAnimationState.AudioTrack.LoadAsync(file.Path);
                if (success)
                {
                    UpdateAudioUI();
                }
            }
        }

        private void AudioMute_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            // Find the toggle button from sender
            if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton toggle)
            {
                _canvasAnimationState.AudioTrack.Settings.Muted = toggle.IsChecked ?? false;
                UpdateAudioMuteIcon();
            }
        }

        private void AudioVolume_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressAudioValueChanges || _canvasAnimationState == null) return;
            _canvasAnimationState.AudioTrack.Settings.Volume = (float)(e.NewValue / 100.0);
        }

        private void AudioRemove_Click(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;
            _canvasAnimationState.AudioTrack.Unload();
            UpdateAudioUI();
        }

        private void UpdateAudioUI()
        {
            if (_canvasAnimationState == null) return;

            var audioTrack = _canvasAnimationState.AudioTrack;

            _suppressAudioValueChanges = true;

            // Find and update UI controls by name
            if (FindName("AudioVolumeSlider") is Slider volumeSlider)
            {
                volumeSlider.Value = audioTrack.Settings.Volume * 100;
            }

            if (FindName("AudioMuteToggle") is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton muteToggle)
            {
                muteToggle.IsChecked = audioTrack.Settings.Muted;
            }

            UpdateAudioMuteIcon();

            if (FindName("AudioRemoveButton") is Button removeButton)
            {
                removeButton.Visibility = audioTrack.IsLoaded ? Visibility.Visible : Visibility.Collapsed;
            }

            // Refresh timeline to show/hide audio track row
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();

            _suppressAudioValueChanges = false;
        }

        private void UpdateAudioMuteIcon()
        {
            if (_canvasAnimationState == null) return;
            bool muted = _canvasAnimationState.AudioTrack.Settings.Muted;

            if (FindName("AudioMuteIcon") is FluentIcons.WinUI.FluentIcon icon)
            {
                icon.Icon = muted ? FluentIcons.Common.Icon.SpeakerMute : FluentIcons.Common.Icon.Speaker2;
            }
        }

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

        // ====================================================================
        // EVENT HANDLERS - CANVAS ANIMATION TIMELINE
        // ====================================================================

        private void CanvasKeyframeGrid_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            CanvasHeaderScrollViewer.ChangeView(CanvasKeyframeScrollViewer.HorizontalOffset, null, null, true);
            CanvasLayerNamesScrollViewer.ChangeView(null, CanvasKeyframeScrollViewer.VerticalOffset, null, true);

            // Update playhead position to account for scroll
            UpdateCanvasPlayhead();
        }

        private void CanvasKeyframeCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Notify that the animation panel was interacted with (for keyboard shortcut routing)
            Interacted?.Invoke();

            if (_canvasAnimationState == null) return;

            var pos = e.GetCurrentPoint(CanvasKeyframeCanvas).Position;
            int frameIndex = (int)(pos.X / CellWidth);
            frameIndex = Math.Clamp(frameIndex, 0, _canvasAnimationState.FrameCount - 1);

            // Determine which row was clicked
            int rowIndex = (int)(pos.Y / CellHeight);

            // Calculate row ranges
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int audioEndRow = audioHeaderRows + audioTrackRows;

            // Check if clicking on the audio header row (collapse/expand)
            if (audioHeaderRows > 0 && rowIndex == 0)
            {
                // Header click - toggle collapse (handled by layer names panel)
                _isDraggingPlayhead = true;
                _canvasAnimationState.SetCurrentFrame(frameIndex);
                CanvasKeyframeCanvas.CapturePointer(e.Pointer);
            }
            // Check if clicking on an audio track row (draggable waveform)
            else if (!_canvasAnimationState.AudioTracks.IsCollapsed && rowIndex >= audioHeaderRows && rowIndex < audioEndRow)
            {
                int audioRowOffset = rowIndex - audioHeaderRows;
                // Find which loaded track this corresponds to
                int loadedIndex = 0;
                _audioDragTrackIndex = -1;
                for (int i = 0; i < _canvasAnimationState.AudioTracks.Count; i++)
                {
                    if (_canvasAnimationState.AudioTracks[i].IsLoaded)
                    {
                        if (loadedIndex == audioRowOffset)
                        {
                            _audioDragTrackIndex = i;
                            break;
                        }
                        loadedIndex++;
                    }
                }

                if (_audioDragTrackIndex >= 0)
                {
                    _isDraggingAudioTrack = true;
                    _audioDragStartFrame = frameIndex;
                    _audioDragStartOffset = _canvasAnimationState.AudioTracks[_audioDragTrackIndex].Settings.StartFrameOffset;
                    CanvasKeyframeCanvas.CapturePointer(e.Pointer);
                    ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
                }
            }
            else
            {
                // Start playhead drag (existing behavior)
                _isDraggingPlayhead = true;
                _canvasAnimationState.SetCurrentFrame(frameIndex);
                CanvasKeyframeCanvas.CapturePointer(e.Pointer);
            }
        }

        private void CanvasKeyframeCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            var pos = e.GetCurrentPoint(CanvasKeyframeCanvas).Position;
            int frameIndex = (int)(pos.X / CellWidth);
            frameIndex = Math.Clamp(frameIndex, 0, _canvasAnimationState.FrameCount - 1);

            if (_isDraggingAudioTrack && _audioDragTrackIndex >= 0)
            {
                // Calculate new offset based on drag distance in frames
                int frameDelta = frameIndex - _audioDragStartFrame;
                int newOffset = _audioDragStartOffset + frameDelta;

                // Update audio track offset
                _canvasAnimationState.AudioTracks[_audioDragTrackIndex].Settings.StartFrameOffset = newOffset;

                // Refresh the waveform display
                RefreshCanvasKeyframeGrid();
            }
            else if (_isDraggingPlayhead)
            {
                _canvasAnimationState.SetCurrentFrame(frameIndex);
            }
            else
            {
                // Update cursor based on hover position
                int rowIndex = (int)(pos.Y / CellHeight);
                int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
                int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
                int audioEndRow = audioHeaderRows + audioTrackRows;

                // Show horizontal resize cursor when hovering over audio track rows (not header)
                if (!_canvasAnimationState.AudioTracks.IsCollapsed && rowIndex >= audioHeaderRows && rowIndex < audioEndRow)
                {
                    ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
                }
                else
                {
                    ProtectedCursor = null;
                }
            }
        }

        private void CanvasKeyframeCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingPlayhead = false;
            _isDraggingAudioTrack = false;
            _audioDragTrackIndex = -1;
            CanvasKeyframeCanvas.ReleasePointerCaptures();
            ProtectedCursor = null;
        }

        // ════════════════════════════════════════════════════════════════════
        // KEYFRAME CONTEXT MENU
        // ════════════════════════════════════════════════════════════════════

        private int _contextMenuFrameIndex;
        private CanvasAnimationTrack? _contextMenuTrack;
        private bool _isContextMenuForStage;

        private void CanvasKeyframeCanvas_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            var pos = e.GetPosition(CanvasKeyframeCanvas);
            _contextMenuFrameIndex = (int)(pos.X / CellWidth);
            _contextMenuFrameIndex = Math.Clamp(_contextMenuFrameIndex, 0, _canvasAnimationState.FrameCount - 1);

            // Determine which track row was clicked
            int rowIndex = (int)(pos.Y / CellHeight);

            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int specialRows = audioHeaderRows + audioTrackRows + stageRows;

            // Check if clicked on stage row
            int stageRowIndex = audioHeaderRows + audioTrackRows;
            _isContextMenuForStage = _canvasAnimationState.Stage.Enabled && rowIndex == stageRowIndex;

            int trackIndex = rowIndex - specialRows;
            if (!_isContextMenuForStage && trackIndex >= 0 && trackIndex < _canvasAnimationState.Tracks.Count)
            {
                _contextMenuTrack = _canvasAnimationState.Tracks[trackIndex];
            }
            else
            {
                _contextMenuTrack = null;
            }

            // Update menu item enabled states
            bool hasTrack = _contextMenuTrack != null || _isContextMenuForStage;
            bool hasKeyframe = _isContextMenuForStage 
                ? _canvasAnimationState.StageTrack.HasKeyframeAt(_contextMenuFrameIndex)
                : (_contextMenuTrack?.HasKeyframeAt(_contextMenuFrameIndex) ?? false);
            bool hasClipboard = KeyframeClipboard.Instance.HasContent;

            // Find and update menu items
            if (KeyframeContextMenu.Items.Count > 0)
            {
                foreach (var item in KeyframeContextMenu.Items)
                {
                    if (item is MenuFlyoutItem menuItem)
                    {
                        switch (menuItem.Name)
                        {
                            case "KeyframeContextAdd":
                                menuItem.IsEnabled = hasTrack && !hasKeyframe;
                                break;
                            case "KeyframeContextRemove":
                            case "KeyframeContextCopy":
                            case "KeyframeContextCut":
                                menuItem.IsEnabled = hasKeyframe;
                                break;
                            case "KeyframeContextPaste":
                                menuItem.IsEnabled = hasTrack && hasClipboard;
                                break;
                            case "KeyframeContextMoveLeft":
                                menuItem.IsEnabled = hasKeyframe && _contextMenuFrameIndex > 0;
                                break;
                            case "KeyframeContextMoveRight":
                                menuItem.IsEnabled = hasKeyframe && _contextMenuFrameIndex < _canvasAnimationState.FrameCount - 1;
                                break;
                        }
                    }
                }
            }
        }

        private void KeyframeContext_AddKeyframe(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            if (_isContextMenuForStage)
            {
                // Add stage keyframe
                _canvasAnimationState.CaptureStageKeyframe(_contextMenuFrameIndex);
                RefreshCanvasKeyframeGrid();
                
                // Clear pending edits if this is the current frame
                if (_contextMenuFrameIndex == _canvasAnimationState.CurrentFrameIndex)
                {
                    _canvasHost?.ClearStagePendingEdits();
                }
                _canvasHost?.InvalidateCanvas();
                StagePreview.RefreshPreview();
                return;
            }

            if (_document == null || _contextMenuTrack == null) return;

            // Find the layer for this track
            var layer = _document.Layers.FirstOrDefault(l => l.Id == _contextMenuTrack.LayerId);
            if (layer != null)
            {
                _canvasAnimationState.CaptureKeyframe(layer, _contextMenuFrameIndex);
                RefreshCanvasKeyframeGrid();
            }
        }

        private void KeyframeContext_RemoveKeyframe(object sender, RoutedEventArgs e)
        {
            if (_canvasAnimationState == null) return;

            if (_isContextMenuForStage)
            {
                // Remove stage keyframe
                _canvasAnimationState.RemoveStageKeyframe(_contextMenuFrameIndex);
                RefreshCanvasKeyframeGrid();
                _canvasHost?.InvalidateCanvas();
                StagePreview.RefreshPreview();
                return;
            }

            if (_contextMenuTrack == null) return;

            _contextMenuTrack.RemoveKeyframeAt(_contextMenuFrameIndex);
            RefreshCanvasKeyframeGrid();
        }

        private void KeyframeContext_CopyKeyframe(object sender, RoutedEventArgs e)
        {
            if (_isContextMenuForStage)
            {
                // Stage keyframe copy not yet supported
                return;
            }

            if (_contextMenuTrack == null) return;

            var keyframe = _contextMenuTrack.GetKeyframeAt(_contextMenuFrameIndex);
            if (keyframe != null)
            {
                KeyframeClipboard.Instance.CopyKeyframe(_contextMenuTrack.LayerId, keyframe);
            }
        }

        private void KeyframeContext_CutKeyframe(object sender, RoutedEventArgs e)
        {
            if (_isContextMenuForStage)
            {
                // Stage keyframe cut not yet supported
                return;
            }

            if (_canvasAnimationState == null || _contextMenuTrack == null) return;

            var keyframe = _contextMenuTrack.GetKeyframeAt(_contextMenuFrameIndex);
            if (keyframe != null)
            {
                KeyframeClipboard.Instance.CopyKeyframe(_contextMenuTrack.LayerId, keyframe);
                _contextMenuTrack.RemoveKeyframeAt(_contextMenuFrameIndex);
                RefreshCanvasKeyframeGrid();
            }
        }

        private void KeyframeContext_PasteKeyframe(object sender, RoutedEventArgs e)
        {
            if (_isContextMenuForStage)
            {
                // Stage keyframe paste not yet supported
                return;
            }

            if (_canvasAnimationState == null || _contextMenuTrack == null) return;

            var clipboard = KeyframeClipboard.Instance;
            if (!clipboard.HasContent) return;

            var keyframesToPaste = clipboard.GetKeyframesToPasteForLayer(_contextMenuTrack.LayerId, _contextMenuFrameIndex);
            foreach (var keyframe in keyframesToPaste)
            {
                _contextMenuTrack.SetKeyframe(keyframe);
            }
            RefreshCanvasKeyframeGrid();
        }

        private void KeyframeContext_MoveKeyframeLeft(object sender, RoutedEventArgs e)
        {
            if (_isContextMenuForStage)
            {
                // Stage keyframe move not yet supported
                return;
            }

            if (_canvasAnimationState == null || _contextMenuTrack == null) return;
            if (_contextMenuFrameIndex <= 0) return;

            _contextMenuTrack.MoveKeyframe(_contextMenuFrameIndex, _contextMenuFrameIndex - 1);
            RefreshCanvasKeyframeGrid();
        }

        private void KeyframeContext_MoveKeyframeRight(object sender, RoutedEventArgs e)
        {
            if (_isContextMenuForStage)
            {
                // Stage keyframe move not yet supported
                return;
            }

            if (_canvasAnimationState == null || _contextMenuTrack == null) return;
            if (_contextMenuFrameIndex >= _canvasAnimationState.FrameCount - 1) return;

            _contextMenuTrack.MoveKeyframe(_contextMenuFrameIndex, _contextMenuFrameIndex + 1);
            RefreshCanvasKeyframeGrid();
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - MODE SWITCHING
        // ════════════════════════════════════════════════════════════════════

        private void TileModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetMode(AnimationMode.Tile);
        }

        private void CanvasModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetMode(AnimationMode.Canvas);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Refresh settings UI when flyout opens
            RefreshAnimationSettingsUI();
        }

        // ════════════════════════════════════════════════════════════════════
        // EVENT HANDLERS - ANIMATION SETTINGS
        // ════════════════════════════════════════════════════════════════════

        private bool _suppressSettingsValueChanges;

        private void RefreshAnimationSettingsUI()
        {
            if (_canvasAnimationState == null) return;

            _suppressSettingsValueChanges = true;

            PingPongToggle.IsOn = _canvasAnimationState.PingPong;
            OnionSkinEnabledToggle.IsOn = _canvasAnimationState.OnionSkinEnabled;
            OnionSkinBeforeBox.Value = _canvasAnimationState.OnionSkinFramesBefore;
            OnionSkinAfterBox.Value = _canvasAnimationState.OnionSkinFramesAfter;
            OnionSkinOpacitySlider.Value = _canvasAnimationState.OnionSkinOpacity * 100;

            _suppressSettingsValueChanges = false;
        }

        private void PingPong_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingsValueChanges || _canvasAnimationState == null) return;
            _canvasAnimationState.PingPong = PingPongToggle.IsOn;
        }

        private void OnionSkinEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingsValueChanges || _canvasAnimationState == null) return;
            _canvasAnimationState.OnionSkinEnabled = OnionSkinEnabledToggle.IsOn;
            
            // Also update the toolbar toggle to stay in sync
            CanvasOnionSkinToggle.IsChecked = OnionSkinEnabledToggle.IsOn;
        }

        private void OnionSkinBefore_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressSettingsValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.OnionSkinFramesBefore = (int)args.NewValue;
        }

        private void OnionSkinAfter_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressSettingsValueChanges || _canvasAnimationState == null || double.IsNaN(args.NewValue)) return;
            _canvasAnimationState.OnionSkinFramesAfter = (int)args.NewValue;
        }

        private void OnionSkinOpacity_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_suppressSettingsValueChanges || _canvasAnimationState == null) return;
            _canvasAnimationState.OnionSkinOpacity = (float)(e.NewValue / 100.0);
        }

        private void AutoScrollPlayhead_Toggled(object sender, RoutedEventArgs e)
        {
            // TODO: Implement auto-scroll playhead feature
            // This would scroll the timeline to keep the playhead visible during playback
        }

        // ====================================================================
        // PRIVATE METHODS
        // ====================================================================

        private void SetMode(AnimationMode mode)
        {
            if (CurrentMode == mode) return;

            CurrentMode = mode;

            // Stop playback when switching modes
            StopPlayback();

            // Update visibility
            TileAnimationContent.Visibility = mode == AnimationMode.Tile
                ? Visibility.Visible
                : Visibility.Collapsed;

            CanvasAnimationContent.Visibility = mode == AnimationMode.Canvas
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Sync tracks when entering canvas mode
            if (mode == AnimationMode.Canvas && _document != null)
            {
                SyncCanvasAnimationTracks();
            }

            ModeChanged?.Invoke(mode);
        }

        private void SelectCanvasLayerById(Guid layerId)
        {
            if (_suppressLayerSelection) return;

            _selectedLayerId = layerId;

            // Deselect stage when selecting a layer
            if (IsStageSelected)
            {
                IsStageSelected = false;
                StageSelectionChanged?.Invoke(false);
            }

            // Update visual selection
            UpdateCanvasLayerSelection();

            // Notify parent to update document active layer
            CanvasLayerSelected?.Invoke(layerId);
        }

        private void UpdateCanvasLayerSelection()
        {
            if (_canvasAnimationState == null) return;

            Guid activeLayerId = _document?.ActiveLayer?.Id ?? Guid.Empty;

            foreach (var child in CanvasLayerNamesPanel.Children)
            {
                if (child is Border border && border.Tag is Guid layerId)
                {
                    bool isSelected = layerId == activeLayerId;
                    border.Background = isSelected
                        ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                        : new SolidColorBrush(Colors.Transparent);

                    if (border.Child is TextBlock text)
                    {
                        text.Foreground = isSelected
                            ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                    }
                }
            }
        }

        /// <summary>
        /// Called when the active layer changes externally (e.g., from LayersPanel).
        /// Updates the selection highlight in the animation timeline.
        /// </summary>
        public void OnActiveLayerChanged()
        {
            if (CurrentMode == AnimationMode.Canvas)
            {
                _suppressLayerSelection = true;
                UpdateCanvasLayerSelection();
                _suppressLayerSelection = false;
            }
        }

        private void RefreshCanvasKeyframeGrid()
        {
            CanvasKeyframeCanvas.Children.Clear();
            if (_canvasAnimationState == null)
            {
                CanvasKeyframeCanvas.Width = 100;
                CanvasKeyframeCanvas.Height = 100;
                return;
            }

            int frameCount = _canvasAnimationState.FrameCount;
            int trackCount = _canvasAnimationState.Tracks.Count;

            // Calculate row counts for special tracks
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int totalRows = audioHeaderRows + audioTrackRows + stageRows + trackCount;

            CanvasKeyframeCanvas.Width = frameCount * CellWidth;
            CanvasKeyframeCanvas.Height = Math.Max(totalRows * CellHeight, 24);

            DrawCanvasKeyframeGrid();
        }

        private void DrawCanvasKeyframeGrid()
        {
            if (_canvasAnimationState == null) return;

            int frameCount = _canvasAnimationState.FrameCount;
            int trackCount = _canvasAnimationState.Tracks.Count;

            // Calculate row counts
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int totalRows = audioHeaderRows + audioTrackRows + stageRows + trackCount;

            var gridBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.2 };

            // Vertical lines
            for (int f = 0; f <= frameCount; f++)
            {
                var line = new Line
                {
                    X1 = f * CellWidth,
                    Y1 = 0,
                    X2 = f * CellWidth,
                    Y2 = totalRows * CellHeight,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                };
                CanvasKeyframeCanvas.Children.Add(line);
            }

            // Horizontal lines
            for (int t = 0; t <= totalRows; t++)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = t * CellHeight,
                    X2 = frameCount * CellWidth,
                    Y2 = t * CellHeight,
                    Stroke = gridBrush,
                    StrokeThickness = 1
                };
                CanvasKeyframeCanvas.Children.Add(line);
            }

            int currentRow = 0;

            // Draw audio header row if there are audio tracks
            if (audioHeaderRows > 0)
            {
                DrawAudioHeaderRow(currentRow, frameCount);
                currentRow++;
            }

            // Draw audio track waveforms if not collapsed
            if (!_canvasAnimationState.AudioTracks.IsCollapsed)
            {
                int trackIndex = 0;
                foreach (var audioTrack in _canvasAnimationState.AudioTracks)
                {
                    if (audioTrack.IsLoaded)
                    {
                        DrawAudioTrackWaveform(currentRow, audioTrack, trackIndex);
                        currentRow++;
                    }
                    trackIndex++;
                }
            }

            // Draw stage keyframes if enabled
            if (_canvasAnimationState.Stage.Enabled)
            {
                DrawStageTrackKeyframes(currentRow);
                currentRow++;
            }

            // Draw layer keyframes
            for (int trackIdx = 0; trackIdx < trackCount; trackIdx++)
            {
                var track = _canvasAnimationState.Tracks[trackIdx];
                DrawCanvasTrackKeyframes(track, currentRow + trackIdx);
            }
        }

        /// <summary>
        /// Draws the audio section header row (shows collapsed/expanded indicator).
        /// </summary>
        private void DrawAudioHeaderRow(int rowIndex, int frameCount)
        {
            // Background for audio header
            var bgRect = new Rectangle
            {
                Width = frameCount * CellWidth,
                Height = CellHeight,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 200, 200))
            };
            Canvas.SetLeft(bgRect, 0);
            Canvas.SetTop(bgRect, rowIndex * CellHeight);
            CanvasKeyframeCanvas.Children.Add(bgRect);
        }

        /// <summary>
        /// Draws a simplified waveform representation in the audio track row.
        /// Respects the audio start frame offset so the waveform appears at the correct timeline position.
        /// The waveform can be dragged left/right to adjust the offset.
        /// </summary>
        private void DrawAudioTrackWaveform(int trackIndex, AudioTrackState audioTrack, int audioTrackCollectionIndex)
        {
            if (_canvasAnimationState == null) return;

            int frameCount = _canvasAnimationState.FrameCount;
            int fps = _canvasAnimationState.FramesPerSecond;
            double audioDurationMs = audioTrack.DurationMs;
            int startFrameOffset = audioTrack.Settings.StartFrameOffset;

            // Calculate audio duration in frames
            double audioDurationFrames = (audioDurationMs * fps) / 1000.0;

            // Background for audio track (full row)
            var bgRect = new Rectangle
            {
                Width = frameCount * CellWidth,
                Height = CellHeight,
                Fill = new SolidColorBrush(Color.FromArgb(20, 0, 200, 200))
            };
            Canvas.SetLeft(bgRect, 0);
            Canvas.SetTop(bgRect, trackIndex * CellHeight);
            CanvasKeyframeCanvas.Children.Add(bgRect);

            // Draw waveform points
            float centerY = trackIndex * CellHeight + CellHeight / 2f;
            float maxAmplitude = (CellHeight / 2f) - 2;

            var waveformBrush = new SolidColorBrush(Color.FromArgb(150, 0, 200, 200));

            // Calculate where audio is visible in the timeline
            int audioStartFrame = startFrameOffset;
            int audioEndFrame = startFrameOffset + (int)Math.Ceiling(audioDurationFrames);

            // Draw waveform region background (highlight where audio actually plays)
            if (audioEndFrame > 0 && audioStartFrame < frameCount)
            {
                int visibleStartFrame = Math.Max(0, audioStartFrame);
                int visibleEndFrame = Math.Min(frameCount, audioEndFrame);

                var waveformBgRect = new Rectangle
                {
                    Width = (visibleEndFrame - visibleStartFrame) * CellWidth,
                    Height = CellHeight - 4,
                    Fill = new SolidColorBrush(Color.FromArgb(50, 0, 200, 200)),
                    RadiusX = 2,
                    RadiusY = 2
                };
                Canvas.SetLeft(waveformBgRect, visibleStartFrame * CellWidth);
                Canvas.SetTop(waveformBgRect, trackIndex * CellHeight + 2);
                CanvasKeyframeCanvas.Children.Add(waveformBgRect);
            }

            // Draw waveform points
            foreach (var point in audioTrack.WaveformData)
            {
                double audioFrame = (point.TimeMs * fps) / 1000.0;
                double animationFrame = audioFrame + startFrameOffset;

                if (animationFrame < 0 || animationFrame >= frameCount) continue;

                float x = (float)(animationFrame * CellWidth);
                float amplitude = point.AveragePeak * maxAmplitude;
                amplitude = Math.Max(amplitude, 1f);

                var bar = new Rectangle
                {
                    Width = 1.5,
                    Height = amplitude * 2,
                    Fill = waveformBrush
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, centerY - amplitude);
                CanvasKeyframeCanvas.Children.Add(bar);
            }

            // Draw cutoff indicator if audio extends past animation end
            if (audioEndFrame > frameCount)
            {
                float cutoffX = frameCount * CellWidth - 2;
                var cutoffLine = new Line
                {
                    X1 = cutoffX,
                    Y1 = trackIndex * CellHeight + 2,
                    X2 = cutoffX,
                    Y2 = trackIndex * CellHeight + CellHeight - 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 100, 100)),
                    StrokeThickness = 2,
                    StrokeDashArray = [2, 2]
                };
                CanvasKeyframeCanvas.Children.Add(cutoffLine);
            }

            // Draw start indicator (handle) at the beginning of the audio
            if (audioStartFrame >= 0 && audioStartFrame < frameCount)
            {
                float handleX = audioStartFrame * CellWidth;
                var handleLine = new Line
                {
                    X1 = handleX,
                    Y1 = trackIndex * CellHeight + 2,
                    X2 = handleX,
                    Y2 = trackIndex * CellHeight + CellHeight - 2,
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 200, 200)),
                    StrokeThickness = 2
                };
                CanvasKeyframeCanvas.Children.Add(handleLine);

                var triangle = new Polygon
                {
                    Points =
                    [
                        new Point(handleX, trackIndex * CellHeight + 2),
                        new Point(handleX + 6, trackIndex * CellHeight + 2),
                        new Point(handleX, trackIndex * CellHeight + 8)
                    ],
                    Fill = new SolidColorBrush(Color.FromArgb(255, 0, 200, 200))
                };
                CanvasKeyframeCanvas.Children.Add(triangle);
            }

            // Center line
            var centerLine = new Line
            {
                X1 = 0,
                Y1 = centerY,
                X2 = frameCount * CellWidth,
                Y2 = centerY,
                Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                StrokeThickness = 0.5
            };
            CanvasKeyframeCanvas.Children.Add(centerLine);
        }

        private void UpdateCanvasPlayPauseIcon()
        {
            if (_canvasAnimationState == null) return;
            CanvasPlayPauseIcon.Icon = _canvasAnimationState.IsPlaying ? Icon.Pause : Icon.Play;
        }

        private void AudioTrack_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Audio track click - could show audio settings in the future
            // For now, just deselect stage and layers
            DeselectStage();
            _selectedLayerId = Guid.Empty;
            UpdateCanvasLayerSelection();
        }

        private void CanvasLayerName_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is Guid layerId)
            {
                SelectCanvasLayerById(layerId);
            }
        }

        private void CanvasLayerName_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is Guid layerId)
            {
                Guid activeLayerId = _document?.ActiveLayer?.Id ?? Guid.Empty;
                if (layerId != activeLayerId)
                {
                    border.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
                }
            }
        }

        private void CanvasLayerName_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border border && border.Tag is Guid layerId)
            {
                Guid activeLayerId = _document?.ActiveLayer?.Id ?? Guid.Empty;
                if (layerId != activeLayerId)
                {
                    border.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
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

        /// <summary>
        /// Draws keyframes for a layer track.
        /// </summary>
        private void DrawCanvasTrackKeyframes(CanvasAnimationTrack track, int trackIndex)
        {
            if (_canvasAnimationState == null) return;

            var keyframeIndices = track.GetKeyframeIndices().ToList();

            for (int i = 0; i < keyframeIndices.Count; i++)
            {
                int frameIndex = keyframeIndices[i];
                int nextFrameIndex = (i + 1 < keyframeIndices.Count)
                    ? keyframeIndices[i + 1]
                    : _canvasAnimationState.FrameCount;

                // Draw hold region (layer keyframes hold values, not interpolate)
                if (nextFrameIndex > frameIndex + 1)
                {
                    var holdRect = new Rectangle
                    {
                        Width = (nextFrameIndex - frameIndex - 1) * CellWidth,
                        Height = CellHeight - 4,
                        Fill = new SolidColorBrush(Colors.CornflowerBlue) { Opacity = 0.3 },
                        RadiusX = 2,
                        RadiusY = 2
                    };
                    Canvas.SetLeft(holdRect, (frameIndex + 1) * CellWidth + 2);
                    Canvas.SetTop(holdRect, trackIndex * CellHeight + 2);
                    CanvasKeyframeCanvas.Children.Add(holdRect);
                }

                // Draw keyframe diamond
                DrawLayerKeyframeDiamond(frameIndex, trackIndex);
            }
        }

        /// <summary>
        /// Draws a layer keyframe diamond (blue color).
        /// </summary>
        private void DrawLayerKeyframeDiamond(int frameIndex, int trackIndex)
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
                Fill = new SolidColorBrush(Colors.CornflowerBlue),
                Stroke = new SolidColorBrush(Colors.DarkBlue),
                StrokeThickness = 1
            };

            double x = frameIndex * CellWidth + (CellWidth - KeyframeDiamondSize) / 2;
            double y = trackIndex * CellHeight + (CellHeight - KeyframeDiamondSize) / 2;
            Canvas.SetLeft(diamond, x);
            Canvas.SetTop(diamond, y);
            CanvasKeyframeCanvas.Children.Add(diamond);
        }

        private void UpdateCanvasPlayhead()
        {
            if (_canvasAnimationState == null)
            {
                CanvasPlayheadLine.Visibility = Visibility.Collapsed;
                return;
            }

            CanvasPlayheadLine.Visibility = Visibility.Visible;

            // Calculate playhead position (center of the cell) relative to content
            double playheadX = _canvasAnimationState.CurrentFrameIndex * CellWidth + CellWidth / 2 - 1;

            // Position the playhead in the overlay canvas, accounting for scroll offset
            double visibleX = playheadX - CanvasKeyframeScrollViewer.HorizontalOffset;

            // Hide playhead if it's scrolled out of view
            double viewportWidth = CanvasKeyframeScrollViewer.ViewportWidth;
            if (visibleX < 0 || visibleX > viewportWidth)
            {
                CanvasPlayheadLine.Visibility = Visibility.Collapsed;
            }
            else
            {
                CanvasPlayheadLine.Visibility = Visibility.Visible;
                Canvas.SetLeft(CanvasPlayheadLine, visibleX);
            }

            // Calculate height including audio header, audio tracks, stage row, and layer rows
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int totalRows = audioHeaderRows + audioTrackRows + stageRows + _canvasAnimationState.Tracks.Count;
            CanvasPlayheadLine.Height = Math.Max(24, totalRows * CellHeight);
        }
    }

    /// <summary>
    /// Animation mode selection.
    /// </summary>
    public enum AnimationMode
    {
        /// <summary>Tile-based animation (Pyxel Edit style).</summary>
        Tile,

        /// <summary>Canvas/layer-based animation (Aseprite style).</summary>
        Canvas
    }
}
