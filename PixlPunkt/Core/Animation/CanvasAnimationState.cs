using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Manages canvas-based animation state for a document (Aseprite-style animation).
    /// Handles timeline, tracks, keyframes, and playback.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CanvasAnimationState provides layer-based animation where each layer can have keyframes
    /// that define its state (visibility, opacity, blend mode, pixel data) at specific frames.
    /// Values are held between keyframes (no interpolation).
    /// </para>
    /// <para>
    /// Each <see cref="CanvasDocument"/> has its own CanvasAnimationState that stores
    /// all tracks and frame data. This is serialized with the .pxp file.
    /// </para>
    /// </remarks>
    public sealed class CanvasAnimationState
    {
        // ====================================================================
        // TIMELINE SETTINGS
        // ====================================================================

        private int _frameCount = 24;

        /// <summary>
        /// Gets or sets the total number of frames in the animation.
        /// </summary>
        public int FrameCount
        {
            get => _frameCount;
            set
            {
                int newValue = Math.Max(1, value);
                if (_frameCount != newValue)
                {
                    _frameCount = newValue;
                    FrameCountChanged?.Invoke(_frameCount);
                }
            }
        }

        private int _framesPerSecond = 12;

        /// <summary>
        /// Gets or sets the playback speed in frames per second.
        /// </summary>
        public int FramesPerSecond
        {
            get => _framesPerSecond;
            set
            {
                int newValue = Math.Clamp(value, 1, 60);
                if (_framesPerSecond != newValue)
                {
                    _framesPerSecond = newValue;
                    FpsChanged?.Invoke(_framesPerSecond);
                }
            }
        }

        /// <summary>
        /// Gets the duration of a single frame in milliseconds.
        /// </summary>
        [JsonIgnore]
        public int FrameDurationMs => 1000 / _framesPerSecond;

        /// <summary>
        /// Gets or sets whether the animation loops.
        /// </summary>
        public bool Loop { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the animation uses ping-pong (reverse at end) playback.
        /// </summary>
        public bool PingPong { get; set; } = false;

        // ====================================================================
        // AUDIO TRACKS
        // ====================================================================

        /// <summary>
        /// Gets the collection of audio tracks for reference audio in the timeline.
        /// </summary>
        public AudioTracksCollection AudioTracks { get; } = new();

        /// <summary>
        /// Gets the first (legacy) audio track for backward compatibility.
        /// Creates a track if none exist.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public AudioTrackState AudioTrack
        {
            get
            {
                if (AudioTracks.Count == 0)
                {
                    AudioTracks.AddTrack();
                }
                return AudioTracks[0];
            }
        }

        /// <summary>
        /// Reloads all audio tracks from their stored file paths.
        /// Called after loading a document from file.
        /// </summary>
        /// <returns>Task that completes when all audio tracks are reloaded.</returns>
        public async System.Threading.Tasks.Task ReloadAudioTracksAsync()
        {
            await AudioTracks.ReloadAllFromSettingsAsync();
        }

        // ====================================================================
        // TRACKS
        // ====================================================================

        /// <summary>
        /// Gets the collection of animation tracks (one per layer/folder).
        /// </summary>
        public ObservableCollection<CanvasAnimationTrack> Tracks { get; } = [];

        // ====================================================================
        // STAGE (CAMERA)
        // ====================================================================

        /// <summary>
        /// Gets the stage (camera/viewport) settings.
        /// </summary>
        public StageSettings Stage { get; } = new();

        /// <summary>
        /// Gets the stage animation track for camera keyframes.
        /// </summary>
        public StageAnimationTrack StageTrack { get; } = new();

        /// <summary>
        /// Gets the interpolated stage transform at the current frame.
        /// </summary>
        /// <param name="frameIndex">The frame to query.</param>
        /// <returns>The interpolated stage transform, or null if stage is disabled or has no keyframes.</returns>
        public StageKeyframeData? GetStageTransformAt(int frameIndex)
        {
            if (!Stage.Enabled)
                return null;

            return StageTrack.GetInterpolatedStateAt(frameIndex);
        }

        /// <summary>
        /// Renders a frame through the stage viewport, applying camera transforms.
        /// Returns the rendered pixel data at the stage's output dimensions.
        /// </summary>
        /// <param name="document">The document to render.</param>
        /// <param name="frameIndex">The frame index to render.</param>
        /// <returns>Pixel data array (BGRA) at output dimensions, or null if stage is disabled.</returns>
        public byte[]? RenderFrameThroughStage(Document.CanvasDocument document, int frameIndex)
        {
            if (!Stage.Enabled || document == null)
                return null;

            // Apply frame state to document
            ApplyFrameToDocument(document, frameIndex);

            // Get stage transform at this frame
            var stageTransform = GetStageTransformAt(frameIndex);

            // Calculate source rect in canvas coordinates
            float stageX, stageY, stageW, stageH;
            float rotation = 0f;
            float scaleX = 1f, scaleY = 1f;

            if (stageTransform != null)
            {
                float centerX = stageTransform.PositionX;
                float centerY = stageTransform.PositionY;
                scaleX = stageTransform.ScaleX;
                scaleY = stageTransform.ScaleY;
                rotation = stageTransform.Rotation;

                // Calculate stage dimensions based on inverse scale (zooming in = smaller source area)
                stageW = Stage.StageWidth / scaleX;
                stageH = Stage.StageHeight / scaleY;
                stageX = centerX - stageW / 2;
                stageY = centerY - stageH / 2;
            }
            else
            {
                stageX = Stage.StageX;
                stageY = Stage.StageY;
                stageW = Stage.StageWidth;
                stageH = Stage.StageHeight;
            }

            // Composite the document
            document.CompositeTo(document.Surface);

            // Create output buffer
            int outW = Stage.OutputWidth;
            int outH = Stage.OutputHeight;
            var output = new byte[outW * outH * 4];

            // Sample from composite surface with transform
            // For simplicity, we'll do nearest neighbor sampling here
            // Bilinear/bicubic would require more complex interpolation
            var srcPixels = document.Surface.Pixels;
            int srcW = document.Surface.Width;
            int srcH = document.Surface.Height;

            // Handle rotation
            float radians = rotation * MathF.PI / 180f;
            float cos = MathF.Cos(-radians); // Negative for inverse transform
            float sin = MathF.Sin(-radians);
            float centerSrcX = stageX + stageW / 2f;
            float centerSrcY = stageY + stageH / 2f;

            for (int outY = 0; outY < outH; outY++)
            {
                for (int outX = 0; outX < outW; outX++)
                {
                    // Map output pixel to source pixel
                    float u = (float)outX / outW;
                    float v = (float)outY / outH;

                    // Position in stage rect (before rotation)
                    float localX = (u - 0.5f) * stageW;
                    float localY = (v - 0.5f) * stageH;

                    // Apply rotation
                    float rotX = localX * cos - localY * sin;
                    float rotY = localX * sin + localY * cos;

                    // Final source position
                    float srcX = centerSrcX + rotX;
                    float srcY = centerSrcY + rotY;

                    // Sample from source
                    int srcXi = (int)srcX;
                    int srcYi = (int)srcY;

                    int outIdx = (outY * outW + outX) * 4;

                    if (srcXi >= 0 && srcXi < srcW && srcYi >= 0 && srcYi < srcH)
                    {
                        int srcIdx = (srcYi * srcW + srcXi) * 4;
                        output[outIdx] = srcPixels[srcIdx];
                        output[outIdx + 1] = srcPixels[srcIdx + 1];
                        output[outIdx + 2] = srcPixels[srcIdx + 2];
                        output[outIdx + 3] = srcPixels[srcIdx + 3];
                    }
                    else
                    {
                        // Outside canvas - transparent
                        output[outIdx] = 0;
                        output[outIdx + 1] = 0;
                        output[outIdx + 2] = 0;
                        output[outIdx + 3] = 0;
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Captures the current stage state as a keyframe.
        /// </summary>
        /// <param name="frameIndex">The frame index for the keyframe.</param>
        public void CaptureStageKeyframe(int frameIndex)
        {
            StageTrack.CaptureKeyframe(Stage, frameIndex);
            StageKeyframeChanged?.Invoke(frameIndex);
        }

        /// <summary>
        /// Sets a stage keyframe with specific transform values.
        /// </summary>
        public void SetStageKeyframe(StageKeyframeData keyframe)
        {
            StageTrack.SetKeyframe(keyframe);
            StageKeyframeChanged?.Invoke(keyframe.FrameIndex);
        }

        /// <summary>
        /// Removes a stage keyframe at the specified frame.
        /// </summary>
        public bool RemoveStageKeyframe(int frameIndex)
        {
            bool removed = StageTrack.RemoveKeyframeAt(frameIndex);
            if (removed)
            {
                StageKeyframeChanged?.Invoke(frameIndex);
            }
            return removed;
        }

        // ====================================================================
        // PIXEL DATA STORAGE
        // ====================================================================

        /// <summary>
        /// Storage for pixel data snapshots referenced by keyframes.
        /// Key = PixelDataId, Value = pixel data (BGRA bytes).
        /// </summary>
        public Dictionary<int, byte[]> PixelDataStorage { get; } = [];

        private int _nextPixelDataId = 0;

        /// <summary>
        /// Stores pixel data and returns a unique ID for referencing it.
        /// </summary>
        public int StorePixelData(byte[] pixels)
        {
            int id = _nextPixelDataId++;
            PixelDataStorage[id] = (byte[])pixels.Clone();
            return id;
        }

        /// <summary>
        /// Retrieves pixel data by ID.
        /// </summary>
        public byte[]? GetPixelData(int pixelDataId)
        {
            return PixelDataStorage.TryGetValue(pixelDataId, out var data) ? data : null;
        }

        /// <summary>
        /// Restores the next pixel data ID counter after loading from file.
        /// This ensures new keyframes get unique IDs that don't collide with loaded data.
        /// </summary>
        internal void RestoreNextPixelDataId(int nextId)
        {
            _nextPixelDataId = Math.Max(_nextPixelDataId, nextId);
        }

        /// <summary>
        /// Removes unreferenced pixel data to free memory.
        /// </summary>
        public void CleanupUnusedPixelData()
        {
            var usedIds = new HashSet<int>();
            foreach (var track in Tracks)
            {
                foreach (var kf in track.Keyframes)
                {
                    if (kf.HasPixelData)
                        usedIds.Add(kf.PixelDataId);
                    if (kf.HasMaskPixelData)
                        usedIds.Add(kf.MaskPixelDataId);
                }
            }

            var toRemove = PixelDataStorage.Keys.Where(id => !usedIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                PixelDataStorage.Remove(id);
            }
        }

        // ====================================================================
        // PLAYBACK STATE
        // ====================================================================

        [JsonIgnore]
        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        [JsonIgnore]
        public int CurrentFrameIndex { get; private set; } = 0;

        [JsonIgnore]
        public bool IsPlaying => PlaybackState == PlaybackState.Playing;

        /// <summary>
        /// Gets the current playback direction (for ping-pong mode).
        /// </summary>
        [JsonIgnore]
        public PlaybackDirection Direction { get; private set; } = PlaybackDirection.Forward;

        private DispatcherTimer? _playbackTimer;
        private DateTime _frameStartTime;

        // ====================================================================
        // ONION SKINNING
        // ====================================================================

        /// <summary>
        /// Gets or sets whether onion skinning is enabled.
        /// </summary>
        public bool OnionSkinEnabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of frames to show before the current frame.
        /// </summary>
        public int OnionSkinFramesBefore { get; set; } = 2;

        /// <summary>
        /// Gets or sets the number of frames to show after the current frame.
        /// </summary>
        public int OnionSkinFramesAfter { get; set; } = 1;

        /// <summary>
        /// Gets or sets the opacity for onion skin frames (0-1).
        /// </summary>
        public float OnionSkinOpacity { get; set; } = 0.3f;

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Raised when the current frame changes.
        /// </summary>
        public event Action<int>? CurrentFrameChanged;

        /// <summary>
        /// Raised when playback state changes.
        /// </summary>
        public event Action<PlaybackState>? PlaybackStateChanged;

        /// <summary>
        /// Raised when tracks are added, removed, or reordered.
        /// </summary>
        public event Action? TracksChanged;

        /// <summary>
        /// Raised when a keyframe is added, removed, or modified.
        /// </summary>
        public event Action<CanvasAnimationTrack, int>? KeyframeChanged;

        /// <summary>
        /// Raised when frame count changes.
        /// </summary>
        public event Action<int>? FrameCountChanged;

        /// <summary>
        /// Raised when FPS changes.
        /// </summary>
        public event Action<int>? FpsChanged;

        /// <summary>
        /// Raised when onion skin settings change.
        /// </summary>
        public event Action? OnionSkinSettingsChanged;

        /// <summary>
        /// Raised when a stage keyframe is added, removed, or modified.
        /// </summary>
        public event Action<int>? StageKeyframeChanged;

        /// <summary>
        /// Raised when stage settings change.
        /// </summary>
        public event Action? StageSettingsChanged;

        /// <summary>
        /// Raised when audio tracks collection changes.
        /// </summary>
        public event Action? AudioTracksChanged;

        // ====================================================================
        // CONSTRUCTOR
        // ====================================================================

        public CanvasAnimationState()
        {
            Tracks.CollectionChanged += (_, __) => TracksChanged?.Invoke();
            Stage.PropertyChanged += (_, __) => StageSettingsChanged?.Invoke();
            StageTrack.KeyframesChanged += () => StageKeyframeChanged?.Invoke(-1);

            // Wire up audio tracks collection events
            AudioTracks.CollectionChanged += () => AudioTracksChanged?.Invoke();
            AudioTracks.TrackLoadedChanged += (_, __) => AudioTracksChanged?.Invoke();
            AudioTracks.CollapsedStateChanged += (_) => AudioTracksChanged?.Invoke();
        }

        // ====================================================================
        // TRACK MANAGEMENT
        // ====================================================================

        /// <summary>
        /// Creates tracks for all layers in the document.
        /// Tracks are stored in UI order (top-to-bottom, reverse of internal order).
        /// Reference layers are excluded from canvas animation tracks.
        /// </summary>
        public void SyncTracksFromDocument(CanvasDocument document)
        {
            if (document == null) return;

            var flattenedLayers = document.GetFlattenedLayers();
            var existingTracksByLayerId = Tracks.ToDictionary(t => t.LayerId);

            // Create tracks in reverse order (top-to-bottom for UI display, like the Layers panel)
            var newTracks = new List<CanvasAnimationTrack>();

            // Iterate in reverse to match Layers panel order (top-to-bottom)
            for (int i = flattenedLayers.Count - 1; i >= 0; i--)
            {
                var layer = flattenedLayers[i];
                
                // Skip reference layers - they are not included in canvas animation
                if (layer is ReferenceLayer)
                    continue;
                
                var layerId = GetLayerGuid(layer);

                if (existingTracksByLayerId.TryGetValue(layerId, out var existingTrack))
                {
                    // Update track info
                    existingTrack.LayerName = layer.Name;
                    existingTrack.IsFolder = layer is LayerFolder;
                    existingTrack.Depth = layer.Depth;
                    newTracks.Add(existingTrack);
                }
                else
                {
                    // Create new track
                    var track = new CanvasAnimationTrack(layerId, layer.Name, layer is LayerFolder, layer.Depth);
                    newTracks.Add(track);
                }
            }

            // Replace tracks collection
            Tracks.Clear();
            foreach (var track in newTracks)
            {
                Tracks.Add(track);
            }
        }

        /// <summary>
        /// Gets a track by layer ID.
        /// </summary>
        public CanvasAnimationTrack? GetTrackByLayerId(Guid layerId)
        {
            return Tracks.FirstOrDefault(t => t.LayerId == layerId);
        }

        /// <summary>
        /// Gets a track by layer reference.
        /// </summary>
        public CanvasAnimationTrack? GetTrackForLayer(LayerBase layer)
        {
            var layerId = GetLayerGuid(layer);
            return GetTrackByLayerId(layerId);
        }

        /// <summary>
        /// Gets a consistent GUID for a layer.
        /// </summary>
        private static Guid GetLayerGuid(LayerBase layer)
        {
            return layer.Id;
        }

        // ====================================================================
        // KEYFRAME OPERATIONS
        // ====================================================================

        /// <summary>
        /// Adds or updates a keyframe for a layer at the current frame.
        /// </summary>
        public void SetKeyframe(LayerBase layer, LayerKeyframeData keyframe)
        {
            var track = GetTrackForLayer(layer);
            if (track == null)
            {
                // Create track if it doesn't exist
                var layerId = GetLayerGuid(layer);
                track = new CanvasAnimationTrack(layerId, layer.Name, layer is LayerFolder, layer.Depth);
                Tracks.Add(track);
            }

            track.SetKeyframe(keyframe);
            KeyframeChanged?.Invoke(track, keyframe.FrameIndex);
        }

        /// <summary>
        /// Removes a keyframe for a layer at a specific frame.
        /// </summary>
        public bool RemoveKeyframe(LayerBase layer, int frameIndex)
        {
            var track = GetTrackForLayer(layer);
            if (track == null) return false;

            bool removed = track.RemoveKeyframeAt(frameIndex);
            if (removed)
            {
                KeyframeChanged?.Invoke(track, frameIndex);
            }
            return removed;
        }

        /// <summary>
        /// Checks if a layer has a keyframe at a specific frame.
        /// </summary>
        public bool HasKeyframe(LayerBase layer, int frameIndex)
        {
            var track = GetTrackForLayer(layer);
            return track?.HasKeyframeAt(frameIndex) ?? false;
        }

        /// <summary>
        /// Captures the current state of a layer as a keyframe at the current frame.
        /// Includes visibility, opacity, blend mode, pixel data, mask state (including mask pixels), and all effect states.
        /// </summary>
        public void CaptureKeyframe(RasterLayer layer, int frameIndex)
        {
            // Store pixel data
            int pixelDataId = StorePixelData(layer.Surface.Pixels);

            // Capture mask state (defaults if no mask)
            bool maskEnabled = layer.Mask?.IsEnabled ?? true;
            bool maskInverted = layer.Mask?.IsInverted ?? false;
            
            // Store mask pixel data if the layer has a mask
            int maskPixelDataId = -1;
            if (layer.Mask != null)
            {
                maskPixelDataId = StorePixelData(layer.Mask.Surface.Pixels);
            }

            var keyframe = new LayerKeyframeData(
                frameIndex,
                layer.Visible,
                layer.Opacity,
                layer.Blend,
                pixelDataId,
                maskEnabled,
                maskInverted,
                maskPixelDataId);

            // Capture effect states
            foreach (var effect in layer.Effects)
            {
                var effectState = new EffectKeyframeData(effect);
                keyframe.EffectStates.Add(effectState);
            }

            SetKeyframe(layer, keyframe);
        }

        /// <summary>
        /// Applies the animation state at a specific frame to the document layers.
        /// Includes visibility, opacity, blend mode, pixel data, mask state (including mask pixels), and effect states.
        /// </summary>
        public void ApplyFrameToDocument(CanvasDocument document, int frameIndex)
        {
            if (document == null) return;

            // Use index-based for loop to avoid collection modification issues
            int trackCount = Tracks.Count;
            for (int trackIdx = 0; trackIdx < trackCount; trackIdx++)
            {
                var track = Tracks[trackIdx];
                var state = track.GetEffectiveStateAt(frameIndex);
                if (state == null) continue;

                // Find the layer in the document
                var layer = FindLayerByGuid(document, track.LayerId);
                if (layer == null) continue;

                // Apply non-pixel properties
                layer.Visible = state.Visible;

                if (layer is RasterLayer raster)
                {
                    raster.Opacity = state.Opacity;
                    raster.Blend = state.BlendMode;

                    // Apply mask state if layer has a mask
                    if (raster.Mask != null)
                    {
                        raster.Mask.IsEnabled = state.MaskEnabled;
                        raster.Mask.IsInverted = state.MaskInverted;
                        
                        // Apply mask pixel data if present
                        if (state.HasMaskPixelData)
                        {
                            var maskPixelData = GetPixelData(state.MaskPixelDataId);
                            if (maskPixelData != null && maskPixelData.Length == raster.Mask.Surface.Pixels.Length)
                            {
                                Buffer.BlockCopy(maskPixelData, 0, raster.Mask.Surface.Pixels, 0, maskPixelData.Length);
                                raster.Mask.UpdatePreview();
                            }
                        }
                    }

                    // Apply pixel data if present
                    if (state.HasPixelData)
                    {
                        var pixelData = GetPixelData(state.PixelDataId);
                        if (pixelData != null && pixelData.Length == raster.Surface.Pixels.Length)
                        {
                            Buffer.BlockCopy(pixelData, 0, raster.Surface.Pixels, 0, pixelData.Length);
                        }
                    }

                    // Apply effect states using index-based loop to avoid collection modification issues
                    int effectStateCount = state.EffectStates.Count;
                    for (int i = 0; i < effectStateCount; i++)
                    {
                        var effectState = state.EffectStates[i];

                        // Find matching effect in the layer's effects collection
                        LayerEffectBase? effect = null;
                        int effectCount = raster.Effects.Count;
                        for (int j = 0; j < effectCount; j++)
                        {
                            if (raster.Effects[j].EffectId == effectState.EffectId)
                            {
                                effect = raster.Effects[j];
                                break;
                            }
                        }

                        if (effect != null)
                        {
                            effectState.ApplyTo(effect);
                        }
                    }
                }
            }

            // Recomposite
            document.CompositeTo(document.Surface);
        }

        /// <summary>
        /// Finds a layer in the document by its GUID.
        /// </summary>
        private static LayerBase? FindLayerByGuid(CanvasDocument document, Guid layerId)
        {
            foreach (var layer in document.GetFlattenedLayers())
            {
                if (GetLayerGuid(layer) == layerId)
                    return layer;
            }
            return null;
        }

        // ====================================================================
        // FRAME NAVIGATION
        // ====================================================================

        /// <summary>
        /// Sets the current frame index.
        /// </summary>
        public void SetCurrentFrame(int index)
        {
            int newIndex = Math.Clamp(index, 0, Math.Max(0, FrameCount - 1));
            if (CurrentFrameIndex != newIndex)
            {
                CurrentFrameIndex = newIndex;
                CurrentFrameChanged?.Invoke(CurrentFrameIndex);

                // Scrub all audio tracks to match frame position (only when not playing)
                if (PlaybackState != PlaybackState.Playing && AudioTracks.HasLoadedTracks)
                {
                    AudioTracks.SeekAllToFrame(newIndex, FramesPerSecond);
                }
            }
        }

        /// <summary>
        /// Advances to the next frame.
        /// </summary>
        public void NextFrame()
        {
            int nextIndex = CurrentFrameIndex + 1;
            if (nextIndex >= FrameCount)
            {
                if (Loop)
                    nextIndex = 0;
                else
                    return;
            }
            SetCurrentFrame(nextIndex);
        }

        /// <summary>
        /// Goes to the previous frame.
        /// </summary>
        public void PreviousFrame()
        {
            int prevIndex = CurrentFrameIndex - 1;
            if (prevIndex < 0)
            {
                if (Loop)
                    prevIndex = FrameCount - 1;
                else
                    return;
            }
            SetCurrentFrame(prevIndex);
        }

        /// <summary>
        /// Goes to the first frame.
        /// </summary>
        public void FirstFrame() => SetCurrentFrame(0);

        /// <summary>
        /// Goes to the last frame.
        /// </summary>
        public void LastFrame() => SetCurrentFrame(FrameCount - 1);

        // ====================================================================
        // PLAYBACK CONTROL
        // ====================================================================

        /// <summary>
        /// Starts or resumes playback.
        /// </summary>
        public void Play()
        {
            if (FrameCount == 0) return;
            if (PlaybackState == PlaybackState.Playing) return;

            PlaybackState = PlaybackState.Playing;
            _frameStartTime = DateTime.Now;

            EnsureTimer();
            _playbackTimer!.Start();

            // Sync audio playback for all tracks
            AudioTracks.UpdatePlaybackStateForFrame(CurrentFrameIndex, FramesPerSecond, FrameCount, true);

            PlaybackStateChanged?.Invoke(PlaybackState);
        }

        /// <summary>
        /// Pauses playback at current frame.
        /// </summary>
        public void Pause()
        {
            if (PlaybackState != PlaybackState.Playing) return;

            PlaybackState = PlaybackState.Paused;
            _playbackTimer?.Stop();

            // Pause all audio tracks
            AudioTracks.PauseAll();

            PlaybackStateChanged?.Invoke(PlaybackState);
        }

        /// <summary>
        /// Stops playback and returns to frame 0.
        /// </summary>
        public void Stop()
        {
            _playbackTimer?.Stop();

            PlaybackState = PlaybackState.Stopped;
            Direction = PlaybackDirection.Forward;
            CurrentFrameIndex = 0;

            // Stop all audio tracks
            AudioTracks.StopAll();

            PlaybackStateChanged?.Invoke(PlaybackState);
            CurrentFrameChanged?.Invoke(CurrentFrameIndex);
        }

        /// <summary>
        /// Toggles between play and pause.
        /// </summary>
        public void TogglePlayPause()
        {
            if (PlaybackState == PlaybackState.Playing)
                Pause();
            else
                Play();
        }

        private void EnsureTimer()
        {
            if (_playbackTimer == null)
            {
                _playbackTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS check rate
                };
                _playbackTimer.Tick += OnPlaybackTick;
            }
        }

        private void OnPlaybackTick(object? sender, object e)
        {
            if (PlaybackState != PlaybackState.Playing) return;

            var elapsed = (DateTime.Now - _frameStartTime).TotalMilliseconds;
            if (elapsed >= FrameDurationMs)
            {
                int framesToAdvance = (int)(elapsed / FrameDurationMs);
                for (int i = 0; i < framesToAdvance; i++)
                {
                    AdvancePlayback();
                    if (PlaybackState != PlaybackState.Playing)
                        return; // Stop was called during advance
                }
                _frameStartTime = DateTime.Now;
            }
        }

        /// <summary>
        /// Advances playback by one frame, handling loop and ping-pong modes.
        /// </summary>
        private void AdvancePlayback()
        {
            int frameCount = FrameCount;
            if (frameCount == 0) return;

            if (PingPong)
            {
                // Ping-pong mode: reverse direction at boundaries
                if (Direction == PlaybackDirection.Forward)
                {
                    if (CurrentFrameIndex >= frameCount - 1)
                    {
                        // Reached end, reverse direction
                        Direction = PlaybackDirection.Backward;
                        SetCurrentFrame(CurrentFrameIndex - 1);
                    }
                    else
                    {
                        SetCurrentFrame(CurrentFrameIndex + 1);
                    }
                }
                else // Backward
                {
                    if (CurrentFrameIndex <= 0)
                    {
                        // Reached beginning
                        if (!Loop)
                        {
                            Stop();
                            return;
                        }
                        // Loop: reverse direction and continue
                        Direction = PlaybackDirection.Forward;
                        SetCurrentFrame(1);
                    }
                    else
                    {
                        SetCurrentFrame(CurrentFrameIndex - 1);
                    }
                }
            }
            else
            {
                // Normal mode: advance forward, loop or stop at end
                int nextFrame = CurrentFrameIndex + 1;
                if (nextFrame >= frameCount)
                {
                    if (Loop)
                    {
                        nextFrame = 0;
                        // When looping, sync all audio tracks
                        AudioTracks.SeekAllToFrame(nextFrame, FramesPerSecond);
                    }
                    else
                    {
                        Stop();
                        return;
                    }
                }
                SetCurrentFrame(nextFrame);
            }

            // Update all audio tracks' playback state
            AudioTracks.UpdatePlaybackStateForFrame(CurrentFrameIndex, FramesPerSecond, FrameCount, true);
        }

        /// <summary>
        /// Updates audio playback state based on the current animation frame.
        /// Handles starting, stopping, and boundary conditions for audio.
        /// </summary>
        private void UpdateAudioPlaybackState(int frameIndex)
        {
            if (!AudioTrack.IsLoaded || AudioTrack.Settings.Muted) return;

            bool shouldPlay = AudioTrack.ShouldPlayAtFrame(frameIndex, FramesPerSecond, FrameCount);
            bool isPlaying = AudioTrack.IsPlaying;

            if (shouldPlay && !isPlaying)
            {
                // Audio should start playing now (we've reached the offset point)
                AudioTrack.SeekToFrame(frameIndex, FramesPerSecond);
                AudioTrack.Play();
            }
            else if (!shouldPlay && isPlaying)
            {
                // Audio should stop (we've passed the audio end or animation end)
                AudioTrack.Pause();
            }
        }

        /// <summary>
        /// Syncs audio playback to a specific frame (used for looping and seeking).
        /// </summary>
        private void SyncAudioToFrame(int frameIndex)
        {
            if (!AudioTrack.IsLoaded || AudioTrack.Settings.Muted) return;

            bool shouldPlay = AudioTrack.ShouldPlayAtFrame(frameIndex, FramesPerSecond, FrameCount);

            if (shouldPlay)
            {
                AudioTrack.SeekToFrame(frameIndex, FramesPerSecond);
                if (PlaybackState == PlaybackState.Playing)
                {
                    AudioTrack.Play();
                }
            }
            else
            {
                AudioTrack.Pause();
                AudioTrack.SeekToFrame(frameIndex, FramesPerSecond);
            }
        }

        // ====================================================================
        // ONION SKIN
        // ====================================================================

        /// <summary>
        /// Sets onion skin settings.
        /// </summary>
        public void SetOnionSkin(bool enabled, int framesBefore = 2, int framesAfter = 1, float opacity = 0.3f)
        {
            OnionSkinEnabled = enabled;
            OnionSkinFramesBefore = framesBefore;
            OnionSkinFramesAfter = framesAfter;
            OnionSkinOpacity = opacity;
            OnionSkinSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Gets frame indices for onion skin display with their opacities.
        /// </summary>
        public List<(int frameIndex, float opacity)> GetOnionSkinFrames()
        {
            var result = new List<(int, float)>();

            if (!OnionSkinEnabled) return result;

            // Frames before
            for (int i = 1; i <= OnionSkinFramesBefore; i++)
            {
                int idx = CurrentFrameIndex - i;
                if (idx >= 0)
                {
                    float opacity = OnionSkinOpacity * (1f - (float)i / (OnionSkinFramesBefore + 1));
                    result.Add((idx, opacity));
                }
            }

            // Frames after
            for (int i = 1; i <= OnionSkinFramesAfter; i++)
            {
                int idx = CurrentFrameIndex + i;
                if (idx < FrameCount)
                {
                    float opacity = OnionSkinOpacity * (1f - (float)i / (OnionSkinFramesAfter + 1));
                    result.Add((idx, opacity));
                }
            }

            return result;
        }

        // ====================================================================
        // CLEANUP
        // ====================================================================

        /// <summary>
        /// Stops playback and releases resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
            _playbackTimer?.Stop();
            _playbackTimer = null;

            // Dispose all audio tracks
            AudioTracks.Dispose();
        }
    }
}
