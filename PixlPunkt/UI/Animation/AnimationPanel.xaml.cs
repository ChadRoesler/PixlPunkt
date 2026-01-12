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
                _canvasAnimationState.SubRoutinesChanged += OnSubRoutinesChanged;
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
            // Unsubscribe from previous canvas host events
            if (_canvasHost != null)
            {
                _canvasHost.SubRoutineSelected -= OnCanvasSubRoutineSelected;
            }

            _canvasHost = canvasHost;
            FrameEditor.BindCanvasHost(canvasHost);

            // Subscribe to canvas host events
            if (_canvasHost != null)
            {
                _canvasHost.SubRoutineSelected += OnCanvasSubRoutineSelected;
            }
        }

        /// <summary>
        /// Called when a sub-routine is selected on the canvas.
        /// Syncs the selection back to the animation panel.
        /// </summary>
        private void OnCanvasSubRoutineSelected(AnimationSubRoutine? subRoutine)
        {
            // Avoid infinite loops - only sync if it's a different selection
            if (SelectedSubRoutine == subRoutine) return;

            SelectedSubRoutine = subRoutine;

            // Deselect layer and stage when sub-routine is selected
            if (subRoutine != null)
            {
                _selectedLayerId = Guid.Empty;
                DeselectStage();
            }

            // Update visual state
            RefreshCanvasLayerNames();
            RefreshCanvasKeyframeGrid();

            // Notify listeners
            SubRoutineSelectionChanged?.Invoke(subRoutine);
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

            if (_canvasAnimationState == null)
                return;

            // ================================================================
            // ORDER: Stage (always on top) → Audio → Layers + Sub-routines
            // ================================================================

            // 1. STAGE (always first if enabled)
            if (_canvasAnimationState.Stage.Enabled)
            {
                var stageHeader = new Border
                {
                    Background = IsStageSelected 
                        ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                        : new SolidColorBrush(Color.FromArgb(100, 100, 150, 200)),
                    Height = CellHeight,
                    Child = new TextBlock
                    {
                        Text = "Stage",
                        Foreground = IsStageSelected
                            ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                            : new SolidColorBrush(Colors.White),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontSize = 11
                    }
                };
                stageHeader.PointerPressed += StageTrack_PointerPressed;
                stageHeader.PointerEntered += StageTrack_PointerEntered;
                stageHeader.PointerExited += StageTrack_PointerExited;
                CanvasLayerNamesPanel.Children.Add(stageHeader);
            }

            // 2. AUDIO TRACKS (after stage)
            if (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0)
            {
                var audioHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(100, 80, 80, 80)),
                    Height = CellHeight,
                    Child = new TextBlock
                    {
                        Text = "Audio",
                        Foreground = new SolidColorBrush(Colors.White),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontSize = 11
                    }
                };
                CanvasLayerNamesPanel.Children.Add(audioHeader);
            }

            // Add audio track rows
            if (!_canvasAnimationState.AudioTracks.IsCollapsed)
            {
                for (int i = 0; i < _canvasAnimationState.AudioTracks.LoadedCount; i++)
                {
                    var trackHeader = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(80, 100, 100, 100)),
                        Height = CellHeight,
                        Child = new TextBlock
                        {
                            Text = $"Audio {i + 1}",
                            Foreground = new SolidColorBrush(Colors.LightGray),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(4, 0, 0, 0),
                            FontSize = 10
                        }
                    };
                    CanvasLayerNamesPanel.Children.Add(trackHeader);
                }
            }

            // 3. LAYERS AND SUB-ROUTINES (interleaved by ZOrder)
            // Build a merged list of items to display
            var displayItems = BuildOrderedTrackList();

            foreach (var item in displayItems)
            {
                if (item.IsSubRoutine && item.SubRoutine != null)
                {
                    // Sub-routine track header
                    var subRoutine = item.SubRoutine;
                    bool isEnabled = subRoutine.IsEnabled;
                    bool isSelected = subRoutine == SelectedSubRoutine;

                    // Create a grid to hold the eye icon and text
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // Eye icon button
                    var eyeButton = new Button
                    {
                        Width = 18,
                        Height = 18,
                        Padding = new Thickness(0),
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Tag = subRoutine,
                        Content = new FluentIcons.WinUI.SymbolIcon
                        {
                            Symbol = (FluentIcons.Common.Symbol)(isEnabled ? Icon.Eye : Icon.EyeOff),
                            FontSize = 12,
                            Foreground = new SolidColorBrush(isEnabled ? Colors.White : Colors.Gray)
                        }
                    };
                    eyeButton.Click += SubRoutineEyeButton_Click;
                    Grid.SetColumn(eyeButton, 0);
                    grid.Children.Add(eyeButton);

                    // Track name text
                    var nameText = new TextBlock
                    {
                        Text = subRoutine.DisplayName,
                        Foreground = new SolidColorBrush(isEnabled ? Colors.White : Colors.Gray),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        FontSize = 10,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(nameText, 1);
                    grid.Children.Add(nameText);

                    // Highlight if selected
                    Color bgColor;
                    if (isSelected)
                        bgColor = Color.FromArgb(255, 200, 140, 60); // Selected orange
                    else if (isEnabled)
                        bgColor = Color.FromArgb(80, 180, 120, 60); // Normal tan
                    else
                        bgColor = Color.FromArgb(40, 180, 120, 60); // Disabled dim

                    var trackHeader = new Border
                    {
                        Background = new SolidColorBrush(bgColor),
                        Height = CellHeight,
                        Child = grid,
                        Tag = subRoutine
                    };
                    trackHeader.PointerPressed += SubRoutineTrackHeader_PointerPressed;
                    trackHeader.RightTapped += SubRoutineTrackHeader_RightTapped;
                    CanvasLayerNamesPanel.Children.Add(trackHeader);
                }
                else if (item.Track != null)
                {
                    // Layer track header
                    var track = item.Track;
                    Guid activeLayerId = _document?.ActiveLayer?.Id ?? Guid.Empty;
                    bool isSelected = track.LayerId == activeLayerId;

                    var border = new Border
                    {
                        Background = isSelected
                            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                            : new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
                        Height = CellHeight,
                        Tag = track.LayerId,
                        Child = new TextBlock
                        {
                            Text = new string(' ', track.Depth * 2) + track.LayerName,
                            Foreground = isSelected
                                ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
                                : new SolidColorBrush(Colors.White),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Margin = new Thickness(4, 0, 0, 0),
                            FontSize = 10,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }
                    };
                    border.PointerPressed += CanvasLayerName_PointerPressed;
                    border.PointerEntered += CanvasLayerName_PointerEntered;
                    border.PointerExited += CanvasLayerName_PointerExited;
                    CanvasLayerNamesPanel.Children.Add(border);
                }
            }
        }

        /// <summary>
        /// Represents an item in the ordered track list (either a layer or sub-routine).
        /// </summary>
        private record struct OrderedTrackItem(
            int DisplayIndex,
            bool IsSubRoutine,
            CanvasAnimationTrack? Track,
            AnimationSubRoutine? SubRoutine);

        /// <summary>
        /// Builds an ordered list of layers and sub-routines for display.
        /// 
        /// Tracks in CanvasAnimationState are stored in UI order (top-to-bottom, matching the Layers panel).
        /// Track 0 = top layer (e.g., Foreground), last track = bottom layer (e.g., Background).
        /// 
        /// For Z-ordering: higher Z = renders on top = should appear at TOP of timeline list.
        /// So we invert the track indices: last track (Background) gets Z=0, first track (Foreground) gets Z=N-1.
        /// Sub-routines use their explicit ZOrder property.
        /// 
        /// Final sort is DESCENDING by Z-order so highest Z appears first in the list.
        /// </summary>
        private List<OrderedTrackItem> BuildOrderedTrackList()
        {
            var items = new List<OrderedTrackItem>();

            if (_canvasAnimationState == null)
                return items;

            int trackCount = _canvasAnimationState.Tracks.Count;

            // Add layers with inverted index as Z-order
            // Track 0 (Foreground/top in UI) should have highest Z, track N-1 (Background) should have Z=0
            for (int i = 0; i < trackCount; i++)
            {
                // Invert: track at index 0 gets Z = trackCount-1, track at index N-1 gets Z = 0
                int zOrder = trackCount - 1 - i;
                items.Add(new OrderedTrackItem(zOrder, false, _canvasAnimationState.Tracks[i], null));
            }

            // Add sub-routines with their ZOrder as display order
            foreach (var subRoutine in _canvasAnimationState.SubRoutines.SubRoutines)
            {
                items.Add(new OrderedTrackItem(subRoutine.ZOrder, true, null, subRoutine));
            }

            // Sort by Z-order in DESCENDING order
            // Higher Z-order = appears at TOP of UI list = rendered LAST (on top)
            items.Sort((a, b) => b.DisplayIndex.CompareTo(a.DisplayIndex));

            return items;
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

            // Calculate row ranges in new order: Stage → Audio → SubRoutines → Layers
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int subRoutineEndRow = audioHeaderRows + audioTrackRows + _canvasAnimationState.SubRoutines.SubRoutines.Count;

            // Get keyboard modifier state
            var ctrlDown = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) 
                & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            // If Ctrl is held down, check if we're over a sub-routine track
            // If so, skip playhead scrubbing and let the sub-routine handler take over
            if (ctrlDown && rowIndex >= audioHeaderRows + audioTrackRows && rowIndex < subRoutineEndRow)
            {
                // Over sub-routine track with Ctrl held - let the sub-routine bar handler take over
                e.Handled = true;
                return;
            }

            // Check if clicking on stage row
            if (stageRows > 0 && rowIndex < stageRows)
            {
                // Stage row - start playhead drag
                _isDraggingPlayhead = true;
                _canvasAnimationState.SetCurrentFrame(frameIndex);
                CanvasKeyframeCanvas.CapturePointer(e.Pointer);
            }
            // Check if clicking on the audio header row (collapse/expand)
            else if (audioHeaderRows > 0 && rowIndex >= stageRows && rowIndex < stageRows + audioHeaderRows)
            {
                // Header click - toggle collapse (handled by layer names panel)
                _isDraggingPlayhead = true;
                _canvasAnimationState.SetCurrentFrame(frameIndex);
                CanvasKeyframeCanvas.CapturePointer(e.Pointer);
            }
            // Check if clicking on an audio track row (draggable waveform)
            else if (!_canvasAnimationState.AudioTracks.IsCollapsed && rowIndex >= stageRows + audioHeaderRows && rowIndex < subRoutineEndRow)
            {
                int audioRowOffset = rowIndex - (stageRows + audioHeaderRows);
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

            // Handle sub-routine resize (captured from handle press)
            if (_isSubRoutineInteracting && _resizingSubRoutine != null)
            {
                // Check if Ctrl is still held - cancel operation if released
                var ctrlDown = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) 
                    & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

                if (!ctrlDown)
                {
                    EndSubRoutineInteraction();
                    return;
                }

                double currentX = pos.X;
                double deltaX = currentX - _resizeStartX;
                int framesDelta = (int)Math.Round(deltaX / CellWidth);

                if (_resizeDirection == "left")
                {
                    // Extend left: move start frame earlier, increase duration
                    int newStartFrame = Math.Max(0, _resizeStartFrame + framesDelta);
                    int newDuration = _resizeStartDuration + (_resizeStartFrame - newStartFrame);
                    
                    if (newDuration >= 1)
                    {
                        _resizingSubRoutine.StartFrame = newStartFrame;
                        _resizingSubRoutine.DurationFrames = newDuration;
                    }
                }
                else if (_resizeDirection == "right")
                {
                    // Extend right: increase duration (no maximum limit)
                    int newDuration = Math.Max(1, _resizeStartDuration + framesDelta);
                    _resizingSubRoutine.DurationFrames = newDuration;
                }
                
                // Don't refresh during drag - visual updates on release
                return;
            }

            // Handle sub-routine drag/move (captured from bar press)
            if (_isSubRoutineInteracting && _draggingSubRoutine != null)
            {
                var ctrlDown = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control) 
                    & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

                if (!ctrlDown)
                {
                    EndSubRoutineInteraction();
                    return;
                }

                double currentX = pos.X;
                double deltaX = currentX - _dragStartX;
                int framesDelta = (int)Math.Round(deltaX / CellWidth);
                int newStartFrame = Math.Max(0, _dragStartFrame + framesDelta);

                _draggingSubRoutine.StartFrame = newStartFrame;
                return;
            }

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
            // Handle sub-routine interaction release
            if (_isSubRoutineInteracting)
            {
                EndSubRoutineInteraction();
            }

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

            AutoKeyframeToggle.IsOn = _canvasAnimationState.AutoKeyframe;
            PingPongToggle.IsOn = _canvasAnimationState.PingPong;
            OnionSkinEnabledToggle.IsOn = _canvasAnimationState.OnionSkinEnabled;
            OnionSkinBeforeBox.Value = _canvasAnimationState.OnionSkinFramesBefore;
            OnionSkinAfterBox.Value = _canvasAnimationState.OnionSkinFramesAfter;
            OnionSkinOpacitySlider.Value = _canvasAnimationState.OnionSkinOpacity * 100;

            _suppressSettingsValueChanges = false;
        }

        private void AutoKeyframe_Toggled(object sender, RoutedEventArgs e)
        {
            if (_suppressSettingsValueChanges || _canvasAnimationState == null) return;
            _canvasAnimationState.AutoKeyframe = AutoKeyframeToggle.IsOn;
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

            // Calculate row counts in new order: Stage → Audio → SubRoutines → Layers
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int subRoutineRows = _canvasAnimationState.SubRoutines.SubRoutines.Count;
            
            int totalRows = stageRows + audioHeaderRows + audioTrackRows + subRoutineRows + trackCount;

            CanvasKeyframeCanvas.Width = frameCount * CellWidth;
            CanvasKeyframeCanvas.Height = Math.Max(totalRows * CellHeight, 24);

            DrawCanvasKeyframeGrid();
        }

        private void DrawCanvasKeyframeGrid()
        {
            if (_canvasAnimationState == null) return;

            int frameCount = _canvasAnimationState.FrameCount;
            int trackCount = _canvasAnimationState.Tracks.Count;

            // Calculate row counts in new order: Stage → Audio → SubRoutines → Layers
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int subRoutineRows = _canvasAnimationState.SubRoutines.SubRoutines.Count;
            int totalRows = stageRows + audioHeaderRows + audioTrackRows + subRoutineRows + trackCount;

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

            // 1. STAGE (always first if enabled)
            if (_canvasAnimationState.Stage.Enabled)
            {
                DrawStageTrackKeyframes(currentRow);
                currentRow++;
            }

            // 2. AUDIO (header + tracks)
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

            // 3. LAYERS AND SUB-ROUTINES (interleaved by ZOrder)
            // Build a merged list of items to display
            var displayItems = BuildOrderedTrackList();
            foreach (var item in displayItems)
            {
                if (item.IsSubRoutine && item.SubRoutine != null)
                {
                    DrawSubRoutineTrackRow(currentRow, item.SubRoutine);
                }
                else if (item.Track != null)
                {
                    DrawCanvasTrackKeyframes(item.Track, currentRow);
                }
                currentRow++;
            }
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

            // Calculate height in new order: Stage → Audio → SubRoutines → Layers
            int stageRows = _canvasAnimationState.Stage.Enabled ? 1 : 0;
            int audioHeaderRows = (_canvasAnimationState.AudioTracks.HasLoadedTracks || _canvasAnimationState.AudioTracks.Count > 0) ? 1 : 0;
            int audioTrackRows = _canvasAnimationState.AudioTracks.IsCollapsed ? 0 : _canvasAnimationState.AudioTracks.LoadedCount;
            int subRoutineTrackRows = _canvasAnimationState.SubRoutines.SubRoutines.Count;
            CanvasPlayheadLine.Height = Math.Max(24, stageRows + audioHeaderRows + audioTrackRows + subRoutineTrackRows + _canvasAnimationState.Tracks.Count) * CellHeight;
        }

        // ====================================================================
        // CANVAS LAYER INTERACTION
        // ====================================================================

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

        // ====================================================================
        // CANVAS PLAYBACK UI
        // ====================================================================

        private void UpdateCanvasPlayPauseIcon()
        {
            if (_canvasAnimationState == null) return;
            CanvasPlayPauseIcon.Icon = _canvasAnimationState.IsPlaying ? Icon.Pause : Icon.Play;
        }

        // ====================================================================
        // LAYER TRACK KEYFRAME DRAWING
        // ====================================================================

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
    }
}