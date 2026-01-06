using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Compositing.Serialization;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Tile;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Binary serializer for PixlPunkt native document format (.pxp).
    /// </summary>
    /// <remarks>
    /// <para>
    /// DocumentIO provides save/load functionality for the PixlPunkt native file format (.pxp), which
    /// uses a custom binary structure for efficient storage and fast loading.
    /// </para>
    /// <para><strong>File Format Structure:</strong></para>
    /// <list type="number">
    /// <item><strong>Header</strong>:
    ///   <list type="bullet">
    ///   <item>Magic number: 'PXP1' (0x31505850) for format identification</item>
    ///   <item>Version: Int32 = 3</item>
    ///   <item>Document name: Length-prefixed UTF-8 string</item>
    ///   </list>
    /// </item>
    /// <item><strong>Canvas Geometry</strong>:
    ///   <list type="bullet">
    ///   <item>Pixel dimensions: width, height (Int32)</item>
    ///   <item>Tile size: width, height (Int32)</item>
    ///   <item>Tile counts: columns, rows (Int32)</item>
    ///   </list>
    /// </item>
    /// <item><strong>Tile Set</strong>:
    ///   <list type="bullet">
    ///   <item>Tile dimensions (Int32, Int32) - 0,0 indicates no tiles</item>
    ///   <item>Tile count (Int32)</item>
    ///   <item>For each tile: ID, pixel data length, pixel data (width * height * 4 bytes)</item>
    ///   </list>
    /// </item>
    /// <item><strong>Tile Animation State</strong> (Version 2+):
    ///   <list type="bullet">
    ///   <item>Reel count (Int32)</item>
    ///   <item>For each reel: GUID, name, settings, frame count, frames</item>
    ///   <item>Onion skin settings</item>
    ///   </list>
    /// </item>
    /// <item><strong>Canvas Animation State</strong> (Version 3+):
    ///   <list type="bullet">
    ///   <item>Frame count, FPS, loop (Int32, Int32, bool)</item>
    ///   <item>Onion skin settings</item>
    ///   <item>Track count (Int32)</item>
    ///   <item>For each track: GUID, layer GUID, name, isFolder, depth, keyframe count, keyframes</item>
    ///   <item>Pixel data storage count and entries</item>
    ///   </list>
    /// </item>
    /// <item><strong>Layer Structure</strong> (recursive):
    ///   <list type="bullet">
    ///   <item>Root item count (Int32)</item>
    ///   <item>For each item: node type (1=RasterLayer, 2=Folder), properties, effects, tile mapping, then children if folder</item>
    ///   </list>
    /// </item>
    /// </list>
    /// </remarks>
    /// <seealso cref="CanvasDocument"/>
    /// <seealso cref="RasterLayer"/>
    /// <seealso cref="LayerFolder"/>
    public static class DocumentIO
    {
        /// <summary>
        /// Magic number identifying PixlPunkt document format: 'PXP1' in ASCII (0x31505850).
        /// </summary>
        private const int Magic = 0x31505850;

        /// <summary>
        /// Current file format version.
        /// Version 1: Initial format
        /// Version 2: Added tile animation state
        /// Version 3: Added canvas animation state
        /// Version 4: Added layer IDs for animation track binding
        /// Version 5: Added stage (camera) settings and keyframes
        /// Version 6: Added effect keyframes for layer effects animation
        /// Version 7: Added audio track settings
        /// Version 8: Added multiple audio tracks support
        /// Version 9: Added layer masks
        /// Version 10: Added animation sub-routines
        /// </summary>
        private const int CurrentVersion = 10;

        private const int NodeType_RasterLayer = 1;
        private const int NodeType_Folder = 2;

        // ═══════════════════════════════════════════════════════════════
        // SAVE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Saves a document to the specified file path in native .pxp format.
        /// </summary>
        /// <param name="doc">The document to save.</param>
        /// <param name="filePath">The target file path (.pxp extension recommended).</param>
        public static void Save(CanvasDocument doc, string filePath)
        {
            try
            {
                LoggingService.Info("Saving document {DocumentName} to {FilePath}", doc.Name ?? "(unnamed)", filePath);
                using var fs = File.Create(filePath);
                Save(doc, fs);
                LoggingService.Info("Document saved {DocumentName} -> {FilePath}", doc.Name ?? "(unnamed)", filePath);
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to save document to {filePath}", ex);
                throw;
            }
        }

        /// <summary>
        /// Saves a document to the specified stream in native .pxp format.
        /// </summary>
        /// <param name="doc">The document to save.</param>
        /// <param name="stream">The target stream (must support writing).</param>
        public static void Save(CanvasDocument doc, Stream stream)
        {
            using var bw = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            // Header
            bw.Write(Magic);
            bw.Write(CurrentVersion);
            bw.Write(doc.Name ?? string.Empty);

            // Canvas geometry
            bw.Write(doc.PixelWidth);
            bw.Write(doc.PixelHeight);
            bw.Write(doc.TileSize.Width);
            bw.Write(doc.TileSize.Height);
            bw.Write(doc.TileCounts.Width);
            bw.Write(doc.TileCounts.Height);

            // Tile set
            WriteTileSet(bw, doc.TileSet);

            // Tile animation state (Version 2+)
            WriteTileAnimationState(bw, doc.TileAnimationState);

            // Canvas animation state (Version 3+)
            WriteCanvasAnimationState(bw, doc.CanvasAnimationState);

            // Layer structure
            var rootItems = doc.RootItems;
            bw.Write(rootItems.Count);
            
            // DEBUG: Log what we're saving
            LoggingService.Info("Saving layer structure: {RootCount} root items", rootItems.Count);
            for (int i = 0; i < rootItems.Count; i++)
            {
                var item = rootItems[i];
                if (item is LayerFolder folder)
                    LoggingService.Info("  [{Index}] Folder '{Name}' children={ChildCount}", i, folder.Name, folder.Children.Count);
                else if (item is RasterLayer layer)
                    LoggingService.Info("  [{Index}] Layer '{Name}' Parent={Parent}", i, layer.Name, layer.Parent?.Name ?? "null");
            }
            
            foreach (var item in rootItems)
            {
                WriteLayerItem(bw, item);
            }

            bw.Flush();
        }

        /// <summary>
        /// Writes canvas animation state to the stream.
        /// </summary>
        private static void WriteCanvasAnimationState(BinaryWriter bw, CanvasAnimationState animState)
        {
            // Timeline settings
            bw.Write(animState.FrameCount);
            bw.Write(animState.FramesPerSecond);
            bw.Write(animState.Loop);

            // Onion skin settings
            bw.Write(animState.OnionSkinEnabled);
            bw.Write(animState.OnionSkinFramesBefore);
            bw.Write(animState.OnionSkinFramesAfter);
            bw.Write(animState.OnionSkinOpacity);

            // Tracks
            bw.Write(animState.Tracks.Count);
            LoggingService.Debug("Writing canvas animation state: {TrackCount} tracks, {FrameCount} frames",
                animState.Tracks.Count, animState.FrameCount);

            foreach (var track in animState.Tracks)
            {
                WriteCanvasAnimationTrack(bw, track);
            }

            // Pixel data storage
            bw.Write(animState.PixelDataStorage.Count);
            foreach (var kvp in animState.PixelDataStorage)
            {
                bw.Write(kvp.Key);
                bw.Write(kvp.Value.Length);
                bw.Write(kvp.Value);
            }

            // Stage settings (Version 5+)
            WriteStageSettings(bw, animState.Stage);
            WriteStageAnimationTrack(bw, animState.StageTrack);

            // Audio tracks (Version 8+) - multiple tracks with collapsed state
            WriteAudioTracksCollection(bw, animState.AudioTracks);

            // Sub-routines (Version 10+)
            WriteSubRoutineTrack(bw, animState.SubRoutines);
        }

        /// <summary>
        /// Writers the audio tracks collection to the stream.
        /// </summary>
        private static void WriteAudioTracksCollection(BinaryWriter bw, AudioTracksCollection audioTracks)
        {
            bw.Write(audioTracks.IsCollapsed);
            bw.Write(audioTracks.Count);

            foreach (var track in audioTracks)
            {
                WriteAudioTrackSettings(bw, track.Settings);
            }
        }

        /// <summary>
        /// Writes the animation sub-routine track to the stream.
        /// </summary>
        private static void WriteSubRoutineTrack(BinaryWriter bw, AnimationSubRoutineTrack subRoutines)
        {
            bw.Write(subRoutines.SubRoutines.Count);
            
            foreach (var subRoutine in subRoutines.SubRoutines)
            {
                WriteAnimationSubRoutine(bw, subRoutine);
            }
        }

        /// <summary>
        /// Writes a single animation sub-routine to the stream.
        /// </summary>
        private static void WriteAnimationSubRoutine(BinaryWriter bw, AnimationSubRoutine subRoutine)
        {
            // Identity
            bw.Write(subRoutine.Id.ToByteArray());
            
            // File reference (path to the .pxpr reel file)
            bw.Write(subRoutine.ReelFilePath ?? string.Empty);
            
            // Timing
            bw.Write(subRoutine.StartFrame);
            bw.Write(subRoutine.DurationFrames);
            
            // State
            bw.Write(subRoutine.IsEnabled);
            bw.Write(subRoutine.ZOrder);
            
            // Interpolation modes
            bw.Write((int)subRoutine.PositionInterpolation);
            bw.Write((int)subRoutine.ScaleInterpolation);
            bw.Write((int)subRoutine.RotationInterpolation);
            
            // Snapshot keyframes to prevent "collection was modified" during save
            // if another thread is editing the animation while saving
            var positionKeyframes = subRoutine.PositionKeyframes.ToArray();
            var scaleKeyframes = subRoutine.ScaleKeyframes.ToArray();
            var rotationKeyframes = subRoutine.RotationKeyframes.ToArray();
            
            // Position keyframes
            bw.Write(positionKeyframes.Length);
            foreach (var kvp in positionKeyframes)
            {
                bw.Write(kvp.Key);      // float - normalized time
                bw.Write(kvp.Value.X);  // double - X position
                bw.Write(kvp.Value.Y);  // double - Y position
            }
            
            // Scale keyframes
            bw.Write(scaleKeyframes.Length);
            foreach (var kvp in scaleKeyframes)
            {
                bw.Write(kvp.Key);    // float - normalized time
                bw.Write(kvp.Value);  // float - scale factor
            }
            
            // Rotation keyframes
            bw.Write(rotationKeyframes.Length);
            foreach (var kvp in rotationKeyframes)
            {
                bw.Write(kvp.Key);    // float - normalized time
                bw.Write(kvp.Value);  // float - rotation degrees
            }
        }

        private static void WriteTileSet(BinaryWriter bw, TileSet? tileSet)
        {
            if (tileSet == null || tileSet.Count == 0)
            {
                bw.Write(0); // tileW = 0 signals "no tiles"
                bw.Write(0);
                return;
            }

            bw.Write(tileSet.TileWidth);
            bw.Write(tileSet.TileHeight);
            bw.Write(tileSet.Count);

            LoggingService.Debug("Writing tile set: {TileCount} tiles ({TileW}x{TileH})",
                tileSet.Count, tileSet.TileWidth, tileSet.TileHeight);

            foreach (var tile in tileSet.Tiles)
            {
                bw.Write(tile.Id);
                bw.Write(tile.Pixels.Length);
                bw.Write(tile.Pixels);
            }
        }

        /// <summary>
        /// Writes tile animation state to the stream.
        /// </summary>
        private static void WriteTileAnimationState(BinaryWriter bw, TileAnimationState animState)
        {
            // Write reel count
            bw.Write(animState.Reels.Count);

            LoggingService.Debug("Writing tile animation state: {ReelCount} reels", animState.Reels.Count);

            foreach (var reel in animState.Reels)
            {
                WriteAnimationReel(bw, reel);
            }

            // Write onion skin settings
            bw.Write(animState.OnionSkinEnabled);
            bw.Write(animState.OnionSkinFramesBefore);
            bw.Write(animState.OnionSkinFramesAfter);
            bw.Write(animState.OnionSkinOpacity);
        }

        /// <summary>
        /// Writes a single animation reel to the stream.
        /// </summary>
        private static void WriteAnimationReel(BinaryWriter bw, TileAnimationReel reel)
        {
            // Identity
            bw.Write(reel.Id.ToByteArray());
            bw.Write(reel.Name ?? string.Empty);

            // Timing settings
            bw.Write(reel.DefaultFrameTimeMs);
            bw.Write(reel.Loop);
            bw.Write(reel.PingPong);

            // Frames
            bw.Write(reel.Frames.Count);
            foreach (var frame in reel.Frames)
            {
                bw.Write(frame.TileX);
                bw.Write(frame.TileY);
                bw.Write(frame.DurationMs.HasValue);
                if (frame.DurationMs.HasValue)
                {
                    bw.Write(frame.DurationMs.Value);
                }
            }

            LoggingService.Debug("Wrote animation reel '{ReelName}' with {FrameCount} frames",
                reel.Name, reel.Frames.Count);
        }

        private static void WriteLayerItem(BinaryWriter bw, LayerBase item)
        {
            if (item is RasterLayer rl)
            {
                bw.Write(NodeType_RasterLayer);
                bw.Write(rl.Id.ToByteArray()); // Save layer ID for animation track binding
                bw.Write(rl.Name ?? string.Empty);
                bw.Write(rl.Visible);
                bw.Write(rl.Locked);
                bw.Write((int)rl.Blend);
                bw.Write(rl.Opacity);

                var surf = rl.Surface;
                bw.Write(surf.Width);
                bw.Write(surf.Height);
                bw.Write(surf.Pixels.Length);
                bw.Write(surf.Pixels);

                // Effects (binary serialization)
                EffectSerializer.SerializeEffects(bw, new List<LayerEffectBase>(rl.Effects));

                // Tile mapping
                WriteTileMapping(bw, rl.TileMapping);

                // Layer mask (Version 9+)
                WriteLayerMask(bw, rl.Mask);
            }
            else if (item is LayerFolder folder)
            {
                bw.Write(NodeType_Folder);
                bw.Write(folder.Id.ToByteArray()); // Save folder ID for animation track binding
                bw.Write(folder.Name ?? string.Empty);
                bw.Write(folder.Visible);
                bw.Write(folder.Locked);
                bw.Write(folder.IsExpanded);

                // DEBUG: Log folder children details
                LoggingService.Info("    Writing folder '{FolderName}' with {ChildCount} children:", folder.Name ?? "unnamed", folder.Children.Count);
                foreach (var child in folder.Children)
                {
                    LoggingService.Info("      - Child: '{ChildName}' (Parent={ParentName})", 
                        child.Name ?? "unnamed", 
                        child.Parent?.Name ?? "null");
                }

                bw.Write(folder.Children.Count);
                foreach (var child in folder.Children)
                {
                    WriteLayerItem(bw, child);
                }
            }
        }

        /// <summary>
        /// Writes a layer mask to the stream.
        /// </summary>
        private static void WriteLayerMask(BinaryWriter bw, LayerMask? mask)
        {
            if (mask == null)
            {
                bw.Write(false); // No mask
                return;
            }

            bw.Write(true); // Has mask
            bw.Write(mask.Width);
            bw.Write(mask.Height);
            bw.Write(mask.IsEnabled);
            bw.Write(mask.IsInverted);
            bw.Write(mask.IsLinked);
            bw.Write(mask.Density);
            bw.Write(mask.FeatherRadius);

            // Write mask data as grayscale (one byte per pixel)
            var grayscaleData = mask.ExportGrayscale();
            bw.Write(grayscaleData.Length);
            bw.Write(grayscaleData);
        }

        private static void WriteTileMapping(BinaryWriter bw, TileMapping? mapping)
        {
            if (mapping == null)
            {
                bw.Write(false);
                return;
            }

            bw.Write(true);
            bw.Write(mapping.Width);
            bw.Write(mapping.Height);

            // Sparse encoding: only write non-empty mappings
            var nonEmpty = new List<(int x, int y, int id)>();
            for (int y = 0; y < mapping.Height; y++)
            {
                for (int x = 0; x < mapping.Width; x++)
                {
                    int id = mapping.GetTileId(x, y);
                    if (id >= 0)
                        nonEmpty.Add((x, y, id));
                }
            }

            bw.Write(nonEmpty.Count);
            foreach (var (x, y, id) in nonEmpty)
            {
                bw.Write(x);
                bw.Write(y);
                bw.Write(id);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LOAD
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads a document from the specified file path.
        /// </summary>
        /// <param name="filePath">The .pxp file to load.</param>
        /// <returns>A fully initialized <see cref="CanvasDocument"/>.</returns>
        public static CanvasDocument Load(string filePath)
        {
            return Load(filePath, out _);
        }

        /// <summary>
        /// Loads a document from the specified file path with warnings.
        /// </summary>
        /// <param name="filePath">The .pxp file to load.</param>
        /// <param name="warnings">Receives any warnings generated during load (e.g., missing plugins).</param>
        /// <returns>A fully initialized <see cref="CanvasDocument"/>.</returns>
        public static CanvasDocument Load(string filePath, out List<EffectLoadWarning> warnings)
        {
            using var fs = File.OpenRead(filePath);
            var nameFromPath = Path.GetFileNameWithoutExtension(filePath);
            LoggingService.Info("Loading document from {FilePath}", filePath);
            var doc = Load(fs, nameFromPath, out warnings);
            LoggingService.Info("Loaded document {DocumentName} from {FilePath}", doc.Name ?? nameFromPath, filePath);
            return doc;
        }

        /// <summary>
        /// Loads a document from the specified stream.
        /// </summary>
        /// <param name="stream">The stream containing .pxp document data.</param>
        /// <param name="displayNameOverride">Optional name to use instead of the stored document name.</param>
        /// <returns>A fully initialized <see cref="CanvasDocument"/>.</returns>
        public static CanvasDocument Load(Stream stream, string? displayNameOverride = null)
        {
            return Load(stream, displayNameOverride, out _);
        }

        /// <summary>
        /// Loads a document from the specified stream with warnings.
        /// </summary>
        /// <param name="stream">The stream containing .pxp document data.</param>
        /// <param name="displayNameOverride">Optional name to use instead of the stored document name.</param>
        /// <param name="warnings">Receives any warnings generated during load (e.g., missing plugins).</param>
        /// <returns>A fully initialized <see cref="CanvasDocument"/>.</returns>
        public static CanvasDocument Load(Stream stream, string? displayNameOverride, out List<EffectLoadWarning> warnings)
        {
            warnings = [];

            using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Header
            var magic = br.ReadInt32();
            if (magic != Magic)
                throw new InvalidDataException("Not a PixlPunkt (.pxp) document.");

            var version = br.ReadInt32();
            if (version < 1 || version > CurrentVersion)
                throw new InvalidDataException($"Unsupported document version: {version}. Expected: 1-{CurrentVersion}");

            var storedName = br.ReadString();

            // Canvas geometry
            int pixelWidth = br.ReadInt32();
            int pixelHeight = br.ReadInt32();
            int tileW = br.ReadInt32();
            int tileH = br.ReadInt32();
            int tilesX = br.ReadInt32();
            int tilesY = br.ReadInt32();

            var name = displayNameOverride ?? storedName ?? "Untitled";
            var doc = new CanvasDocument(name, pixelWidth, pixelHeight, CreateSize(tileW, tileH), CreateSize(tilesX, tilesY));

            // Tile set
            ReadTileSet(br, doc);

            // Tile animation state (Version 2+)
            if (version >= 2)
            {
                ReadTileAnimationState(br, doc.TileAnimationState);
            }

            // Canvas animation state (Version 3+)
            if (version >= 3)
            {
                ReadCanvasAnimationState(br, doc.CanvasAnimationState, version);
            }

            // Layer structure
            int rootItemCount = br.ReadInt32();
            bool removeDefaultLayer = rootItemCount > 0 && doc.Layers.Count == 1;
            bool replacedDefault = false;

            if (removeDefaultLayer && doc.RootItems.Count == 1 && doc.RootItems[0] is RasterLayer placeholder)
            {
                doc.RemoveLayerWithoutHistory(placeholder, allowRemoveLast: true);
                replacedDefault = true;
            }

            // Version 4+ includes layer IDs for animation track binding
            bool hasLayerIds = version >= 4;
            bool hasMasks = version >= 9;

            for (int i = 0; i < rootItemCount; i++)
            {
                ReadLayerItem(br, doc, pixelWidth, pixelHeight, null, removeDefaultLayer && !replacedDefault, ref replacedDefault, warnings, hasLayerIds, hasMasks);
            }

            if (doc.Layers.Count > 0)
                doc.SetActiveLayer(0);

            doc.CompositeTo(doc.Surface);
            return doc;
        }

        /// <summary>
        /// Reads canvas animation state from the stream.
        /// </summary>
        private static void ReadCanvasAnimationState(BinaryReader br, CanvasAnimationState animState, int version)
        {
            // Timeline settings
            animState.FrameCount = br.ReadInt32();
            animState.FramesPerSecond = br.ReadInt32();
            animState.Loop = br.ReadBoolean();

            // Onion skin settings
            animState.OnionSkinEnabled = br.ReadBoolean();
            animState.OnionSkinFramesBefore = br.ReadInt32();
            animState.OnionSkinFramesAfter = br.ReadInt32();
            animState.OnionSkinOpacity = br.ReadSingle();

            // Tracks
            int trackCount = br.ReadInt32();
            LoggingService.Debug("Reading canvas animation state: {TrackCount} tracks, {FrameCount} frames",
                trackCount, animState.FrameCount);

            for (int i = 0; i < trackCount; i++)
            {
                var track = ReadCanvasAnimationTrack(br, version);
                animState.Tracks.Add(track);
            }

            // Pixel data storage
            int pixelDataCount = br.ReadInt32();
            int maxPixelDataId = -1;
            for (int i = 0; i < pixelDataCount; i++)
            {
                int id = br.ReadInt32();
                int length = br.ReadInt32();
                var data = br.ReadBytes(length);
                animState.PixelDataStorage[id] = data;

                // Track the maximum ID so we can resume from there
                if (id > maxPixelDataId)
                    maxPixelDataId = id;
            }

            // Restore the next pixel data ID to avoid collisions
            animState.RestoreNextPixelDataId(maxPixelDataId + 1);

            // Stage settings (Version 5+)
            if (version >= 5)
            {
                ReadStageSettings(br, animState.Stage);
                ReadStageAnimationTrack(br, animState.StageTrack);
            }

            // Audio tracks (Version 7+)
            if (version >= 8)
            {
                // Version 8+: Multiple audio tracks
                ReadAudioTracksCollection(br, animState.AudioTracks);
            }
            else if (version >= 7)
            {
                // Version 7: Single audio track (backward compatibility)
                // Create a track and read settings into it
                var track = animState.AudioTracks.AddTrack();
                ReadAudioTrackSettings(br, track.Settings);
            }

            // Sub-routine tracks (Version 10+)
            if (version >= 10)
            {
                ReadSubRoutineTrack(br, animState.SubRoutines);
            }
        }

        /// <summary>
        /// Reads the audio tracks collection from the stream.
        /// </summary>
        private static void ReadAudioTracksCollection(BinaryReader br, AudioTracksCollection audioTracks)
        {
            audioTracks.Clear();
            audioTracks.SetCollapsed(br.ReadBoolean());
            int trackCount = br.ReadInt32();

            for (int i = 0; i < trackCount; i++)
            {
                var track = audioTracks.AddTrack();
                ReadAudioTrackSettings(br, track.Settings);
            }
        }

        /// <summary>
        /// Reads audio track settings from the stream.
        /// </summary>
        private static void ReadAudioTrackSettings(BinaryReader br, AudioTrackSettings settings)
        {
            settings.FilePath = br.ReadString();
            settings.Volume = br.ReadSingle();
            settings.Muted = br.ReadBoolean();
            settings.LoopWithAnimation = br.ReadBoolean();
            settings.StartFrameOffset = br.ReadInt32();
            settings.ShowWaveform = br.ReadBoolean();
            settings.WaveformColorMode = (WaveformColorMode)br.ReadInt32();
        }

        /// <summary>
        /// Reads the animation sub-routine track from the stream.
        /// </summary>
        private static void ReadSubRoutineTrack(BinaryReader br, AnimationSubRoutineTrack subRoutines)
        {
            subRoutines.Clear();
            
            int count = br.ReadInt32();
            
            for (int i = 0; i < count; i++)
            {
                var subRoutine = ReadAnimationSubRoutine(br);
                subRoutines.Add(subRoutine);
            }
        }

        /// <summary>
        /// Reads a single animation sub-routine from the stream.
        /// </summary>
        private static AnimationSubRoutine ReadAnimationSubRoutine(BinaryReader br)
        {
            var subRoutine = new AnimationSubRoutine();
            
            // Identity
            subRoutine.Id = new Guid(br.ReadBytes(16));
            
            // File reference
            subRoutine.ReelFilePath = br.ReadString();
            
            // Timing
            subRoutine.StartFrame = br.ReadInt32();
            subRoutine.DurationFrames = br.ReadInt32();
            
            // State
            subRoutine.IsEnabled = br.ReadBoolean();
            subRoutine.ZOrder = br.ReadInt32();
            
            // Interpolation modes
            subRoutine.PositionInterpolation = (InterpolationMode)br.ReadInt32();
            subRoutine.ScaleInterpolation = (InterpolationMode)br.ReadInt32();
            subRoutine.RotationInterpolation = (InterpolationMode)br.ReadInt32();
            
            // Position keyframes
            int positionKeyframeCount = br.ReadInt32();
            for (int i = 0; i < positionKeyframeCount; i++)
            {
                float time = br.ReadSingle();
                double x = br.ReadDouble();
                double y = br.ReadDouble();
                subRoutine.PositionKeyframes[time] = (x, y);
            }
            
            // Scale keyframes
            int scaleKeyframeCount = br.ReadInt32();
            for (int i = 0; i < scaleKeyframeCount; i++)
            {
                float time = br.ReadSingle();
                float scale = br.ReadSingle();
                subRoutine.ScaleKeyframes[time] = scale;
            }
            
            // Rotation keyframes
            int rotationKeyframeCount = br.ReadInt32();
            for (int i = 0; i < rotationKeyframeCount; i++)
            {
                float time = br.ReadSingle();
                float rotation = br.ReadSingle();
                subRoutine.RotationKeyframes[time] = rotation;
            }
            
            return subRoutine;
        }

        /// <summary>
        /// Writes a canvas animation track to the stream.
        /// </summary>
        private static void WriteCanvasAnimationTrack(BinaryWriter bw, CanvasAnimationTrack track
        )
        {
            // Identity
            bw.Write(track.Id.ToByteArray());
            bw.Write(track.LayerId.ToByteArray());
            bw.Write(track.LayerName ?? string.Empty);
            bw.Write(track.IsFolder);
            bw.Write(track.Depth);

            // Keyframes
            bw.Write(track.Keyframes.Count);
            foreach (var kf in track.Keyframes)
            {
                WriteLayerKeyframe(bw, kf);
            }
        }

        /// <summary>
        /// Writes a layer keyframe to the stream.
        /// </summary>
        private static void WriteLayerKeyframe(BinaryWriter bw, LayerKeyframeData kf)
        {
            bw.Write(kf.FrameIndex);
            bw.Write(kf.Visible);
            bw.Write(kf.Opacity);
            bw.Write((int)kf.BlendMode);
            bw.Write(kf.PixelDataId);

            // Effect states (Version 6+)
            bw.Write(kf.EffectStates.Count);
            foreach (var effectState in kf.EffectStates)
            {
                WriteEffectKeyframe(bw, effectState);
            }
        }

        /// <summary>
        /// Writes an effect keyframe to the stream.
        /// </summary>
        private static void WriteEffectKeyframe(BinaryWriter bw, Animation.EffectKeyframeData effectState)
        {
            bw.Write(effectState.EffectId ?? string.Empty);
            bw.Write(effectState.IsEnabled);

            // Write property values
            bw.Write(effectState.PropertyValues.Count);
            foreach (var kvp in effectState.PropertyValues)
            {
                bw.Write(kvp.Key);
                WritePropertyValue(bw, kvp.Value);
            }
        }

        /// <summary>
        /// Writes a property value to the stream with type information.
        /// </summary>
        private static void WritePropertyValue(BinaryWriter bw, object? value)
        {
            if (value == null)
            {
                bw.Write((byte)0); // null marker
                return;
            }

            var type = value.GetType();

            if (type == typeof(bool))
            {
                bw.Write((byte)1);
                bw.Write((bool)value);
            }
            else if (type == typeof(byte))
            {
                bw.Write((byte)2);
                bw.Write((byte)value);
            }
            else if (type == typeof(int))
            {
                bw.Write((byte)3);
                bw.Write((int)value);
            }
            else if (type == typeof(float))
            {
                bw.Write((byte)4);
                bw.Write((float)value);
            }
            else if (type == typeof(double))
            {
                bw.Write((byte)5);
                bw.Write((double)value);
            }
            else if (type == typeof(string))
            {
                bw.Write((byte)6);
                bw.Write((string)value);
            }
            else if (type.IsEnum)
            {
                bw.Write((byte)7);
                bw.Write(type.AssemblyQualifiedName ?? type.FullName ?? type.Name);
                bw.Write(value.ToString() ?? string.Empty);
            }
            else if (type == typeof(uint))
            {
                bw.Write((byte)8);
                bw.Write((uint)value);
            }
            else
            {
                // Unsupported type - write as null
                bw.Write((byte)0);
            }
        }

        /// <summary>
        /// Reads a canvas animation track from the stream.
        /// </summary>
        private static CanvasAnimationTrack ReadCanvasAnimationTrack(BinaryReader br, int version = CurrentVersion)
        {
            // Identity
            var id = new Guid(br.ReadBytes(16));
            var layerId = new Guid(br.ReadBytes(16));
            var layerName = br.ReadString();
            var isFolder = br.ReadBoolean();
            var depth = br.ReadInt32();

            var track = new CanvasAnimationTrack
            {
                Id = id,
                LayerId = layerId,
                LayerName = layerName,
                IsFolder = isFolder,
                Depth = depth
            };

            // Keyframes
            int keyframeCount = br.ReadInt32();
            for (int i = 0; i < keyframeCount; i++)
            {
                var kf = ReadLayerKeyframe(br, version);
                track.Keyframes.Add(kf);
            }

            return track;
        }

        /// <summary>
        /// Reads a layer keyframe from the stream.
        /// </summary>
        private static LayerKeyframeData ReadLayerKeyframe(BinaryReader br, int version = CurrentVersion)
        {
            var kf = new LayerKeyframeData
            {
                FrameIndex = br.ReadInt32(),
                Visible = br.ReadBoolean(),
                Opacity = br.ReadByte(),
                BlendMode = (BlendMode)br.ReadInt32(),
                PixelDataId = br.ReadInt32()
            };

            // Effect states (Version 6+)
            if (version >= 6)
            {
                int effectCount = br.ReadInt32();
                for (int i = 0; i < effectCount; i++)
                {
                    var effectState = ReadEffectKeyframe(br);
                    kf.EffectStates.Add(effectState);
                }
            }

            return kf;
        }

        /// <summary>
        /// Reads an effect keyframe from the stream.
        /// </summary>
        private static Animation.EffectKeyframeData ReadEffectKeyframe(BinaryReader br)
        {
            var effectState = new Animation.EffectKeyframeData
            {
                EffectId = br.ReadString(),
                IsEnabled = br.ReadBoolean()
            };

            // Read property values
            int propCount = br.ReadInt32();
            for (int i = 0; i < propCount; i++)
            {
                string propName = br.ReadString();
                var value = ReadPropertyValue(br);
                effectState.PropertyValues[propName] = value;
            }

            return effectState;
        }

        /// <summary>
        /// Reads a property value from the stream with type information.
        /// </summary>
        private static object? ReadPropertyValue(BinaryReader br)
        {
            byte typeCode = br.ReadByte();

            return typeCode switch
            {
                0 => null, // null marker
                1 => br.ReadBoolean(),
                2 => br.ReadByte(),
                3 => br.ReadInt32(),
                4 => br.ReadSingle(),
                5 => br.ReadDouble(),
                6 => br.ReadString(),
                7 => ReadEnumValue(br),
                8 => br.ReadUInt32(),
                _ => null
            };
        }

        /// <summary>
        /// Reads an enum value from the stream.
        /// </summary>
        private static object? ReadEnumValue(BinaryReader br)
        {
            string typeName = br.ReadString();
            string valueName = br.ReadString();

            try
            {
                var type = Type.GetType(typeName);
                if (type != null && type.IsEnum)
                {
                    return Enum.Parse(type, valueName);
                }
            }
            catch
            {
                // Fall back to string representation if type not found
            }

            return valueName;
        }

        private static void ReadTileSet(BinaryReader br, CanvasDocument doc)
        {
            int tileW = br.ReadInt32();
            int tileH = br.ReadInt32();

            if (tileW == 0)
                return; // No tiles

            int tileCount = br.ReadInt32();
            if (tileCount == 0)
                return;

            var tileSet = doc.TileSet ?? new TileSet(tileW, tileH);

            for (int i = 0; i < tileCount; i++)
            {
                int id = br.ReadInt32();
                int pixelLen = br.ReadInt32();
                var pixels = br.ReadBytes(pixelLen);
                tileSet.AddTileInternal(new TileDefinition(id, tileW, tileH, pixels));
            }

            if (doc.TileSet == null)
                doc.SetTileSet(tileSet);
        }

        /// <summary>
        /// Reads tile animation state from the stream.
        /// </summary>
        private static void ReadTileAnimationState(BinaryReader br, TileAnimationState animState)
        {
            // Read reel count
            int reelCount = br.ReadInt32();

            LoggingService.Debug("Reading tile animation state: {ReelCount} reels", reelCount);

            for (int i = 0; i < reelCount; i++)
            {
                var reel = ReadAnimationReel(br);
                animState.Reels.Add(reel);
            }

            // Read onion skin settings
            animState.OnionSkinEnabled = br.ReadBoolean();
            animState.OnionSkinFramesBefore = br.ReadInt32();
            animState.OnionSkinFramesAfter = br.ReadInt32();
            animState.OnionSkinOpacity = br.ReadSingle();

            // Auto-select first reel if any exist
            if (animState.Reels.Count > 0)
            {
                animState.SelectReel(animState.Reels[0]);
            }
        }

        /// <summary>
        /// Reads a single animation reel from the stream.
        /// </summary>
        private static TileAnimationReel ReadAnimationReel(BinaryReader br)
        {
            // Identity
            var idBytes = br.ReadBytes(16);
            var id = new Guid(idBytes);
            var name = br.ReadString();

            var reel = new TileAnimationReel(name)
            {
                Id = id
            };

            // Timing settings
            reel.DefaultFrameTimeMs = br.ReadInt32();
            reel.Loop = br.ReadBoolean();
            reel.PingPong = br.ReadBoolean();

            // Frames
            int frameCount = br.ReadInt32();
            for (int i = 0; i < frameCount; i++)
            {
                int tileX = br.ReadInt32();
                int tileY = br.ReadInt32();
                bool hasDuration = br.ReadBoolean();
                int? durationMs = hasDuration ? br.ReadInt32() : null;

                reel.Frames.Add(new ReelFrame(tileX, tileY, durationMs));
            }

            LoggingService.Debug("Read animation reel '{ReelName}' with {FrameCount} frames",
                reel.Name, reel.Frames.Count);

            return reel;
        }

        private static LayerBase? ReadLayerItem(BinaryReader br, CanvasDocument doc, int pixelWidth, int pixelHeight, LayerFolder? parent, bool canReuseDefault, ref bool replacedDefault, List<EffectLoadWarning> warnings, bool hasLayerIds, bool hasMasks)
        {
            int nodeType = br.ReadInt32();

            if (nodeType == NodeType_RasterLayer)
            {
                // Version 4+ includes layer ID for animation track binding
                Guid layerId = hasLayerIds ? new Guid(br.ReadBytes(16)) : Guid.Empty;

                string layerName = br.ReadString();
                bool visible = br.ReadBoolean();
                bool locked = br.ReadBoolean();
                var blend = (BlendMode)br.ReadInt32();
                byte opacity = br.ReadByte();

                int w = br.ReadInt32();
                int h = br.ReadInt32();
                int dataLen = br.ReadInt32();
                var data = br.ReadBytes(dataLen);

                var effects = EffectSerializer.DeserializeEffects(br, warnings);
                var mapping = ReadTileMapping(br);

                // Read layer mask (Version 9+)
                LayerMask? mask = hasMasks ? ReadLayerMask(br) : null;

                RasterLayer rl;
                if (canReuseDefault && !replacedDefault && parent == null)
                {
                    rl = doc.Layers[0];
                    rl.Name = layerName;
                    replacedDefault = true;
                }
                else if (parent != null)
                {
                    rl = new RasterLayer(pixelWidth, pixelHeight, layerName);
                    parent.AddChild(rl);
                }
                else
                {
                    int idx = doc.AddLayer(layerName, insertAt: doc.Layers.Count);
                    rl = doc.Layers[idx];
                }

                // Restore layer ID for animation track binding (only if we have saved IDs)
                if (hasLayerIds && layerId != Guid.Empty)
                {
                    rl.RestoreId(layerId);
                }

                rl.Effects.Clear();
                foreach (var eff in effects)
                    rl.Effects.Add(eff);

                rl.TileMapping = mapping;
                rl.Mask = mask;

                if (rl.Surface.Width != w || rl.Surface.Height != h)
                    throw new InvalidDataException("Layer surface size mismatch.");

                Buffer.BlockCopy(data, 0, rl.Surface.Pixels, 0, dataLen);

                rl.Visible = visible;
                rl.Locked = locked;
                rl.Blend = blend;
                rl.Opacity = opacity;
                rl.UpdatePreview();

                return rl;
            }
            else if (nodeType == NodeType_Folder)
            {
                // Version 4+ includes folder ID for animation track binding
                Guid folderId = hasLayerIds ? new Guid(br.ReadBytes(16)) : Guid.Empty;

                string folderName = br.ReadString();
                bool visible = br.ReadBoolean();
                bool locked = br.ReadBoolean();
                bool isExpanded = br.ReadBoolean();
                int childCount = br.ReadInt32();

                LayerFolder folder;
                if (parent != null)
                {
                    // Nested folder - create and add to parent
                    folder = new LayerFolder(folderName);
                    parent.AddChild(folder);
                }
                else
                {
                    // Root-level folder - use direct insertion to avoid active layer context issues
                    folder = doc.AddFolderAtRootWithoutHistory(folderName, doc.RootItems.Count);
                }

                // Restore folder ID for animation track binding (only if we have saved IDs)
                if (hasLayerIds && folderId != Guid.Empty)
                {
                    folder.RestoreId(folderId);
                }

                folder.Visible = visible;
                folder.Locked = locked;
                folder.IsExpanded = isExpanded;

                for (int i = 0; i < childCount; i++)
                {
                    ReadLayerItem(br, doc, pixelWidth, pixelHeight, folder, false, ref replacedDefault, warnings, hasLayerIds, hasMasks);
                }

                return folder;
            }
            else
            {
                throw new InvalidDataException($"Unknown layer node type: {nodeType}");
            }
        }

        private static TileMapping? ReadTileMapping(BinaryReader br)
        {
            if (!br.ReadBoolean())
                return null;

            int width = br.ReadInt32();
            int height = br.ReadInt32();
            var mapping = new TileMapping(width, height);

            int entryCount = br.ReadInt32();
            for (int i = 0; i < entryCount; i++)
            {
                int x = br.ReadInt32();
                int y = br.ReadInt32();
                int id = br.ReadInt32();
                mapping.SetTileId(x, y, id);
            }

            return mapping;
        }

        /// <summary>
        /// Reads a layer mask from the stream.
        /// </summary>
        private static LayerMask? ReadLayerMask(BinaryReader br)
        {
            if (!br.ReadBoolean()) // No mask
                return null;

            int width = br.ReadInt32();
            int height = br.ReadInt32();
            bool isEnabled = br.ReadBoolean();
            bool isInverted = br.ReadBoolean();
            bool isLinked = br.ReadBoolean();
            byte density = br.ReadByte();
            int featherRadius = br.ReadInt32();

            int dataLength = br.ReadInt32();
            var grayscaleData = br.ReadBytes(dataLength);

            var mask = new LayerMask(width, height, grayscaleData)
            {
                IsEnabled = isEnabled,
                IsInverted = isInverted,
                IsLinked = isLinked,
                Density = density,
                FeatherRadius = featherRadius
            };

            return mask;
        }

        /// <summary>
        /// Writes audio track settings to the stream.
        /// </summary>
        private static void WriteAudioTrackSettings(BinaryWriter bw, AudioTrackSettings settings)
        {
            bw.Write(settings.FilePath ?? string.Empty);
            bw.Write(settings.Volume);
            bw.Write(settings.Muted);
            bw.Write(settings.LoopWithAnimation);
            bw.Write(settings.StartFrameOffset);
            bw.Write(settings.ShowWaveform);
            bw.Write((int)settings.WaveformColorMode);
        }

        /// <summary>
        /// Writes stage settings to the stream.
        /// </summary>
        private static void WriteStageSettings(BinaryWriter bw, StageSettings stage)
        {
            bw.Write(stage.Enabled);
            bw.Write(stage.OutputWidth);
            bw.Write(stage.OutputHeight);
            bw.Write(stage.StageX);
            bw.Write(stage.StageY);
            bw.Write(stage.StageWidth);
            bw.Write(stage.StageHeight);
            bw.Write((int)stage.ScalingAlgorithm);
            bw.Write((int)stage.RotationInterpolation);
            bw.Write((int)stage.BoundsMode);
        }

        /// <summary>
        /// Writes stage animation track to the stream.
        /// </summary>
        private static void WriteStageAnimationTrack(BinaryWriter bw, StageAnimationTrack track)
        {
            bw.Write(track.Id.ToByteArray());
            bw.Write(track.Name ?? string.Empty);

            // Keyframes
            bw.Write(track.Keyframes.Count);
            foreach (var kf in track.Keyframes)
            {
                WriteStageKeyframe(bw, kf);
            }
        }

        /// <summary>
        /// Writes a stage keyframe to the stream.
        /// </summary>
        private static void WriteStageKeyframe(BinaryWriter bw, StageKeyframeData kf)
        {
            bw.Write(kf.FrameIndex);
            bw.Write(kf.PositionX);
            bw.Write(kf.PositionY);
            bw.Write(kf.ScaleX);
            bw.Write(kf.ScaleY);
            bw.Write(kf.UniformScale);
            bw.Write(kf.Rotation);
            bw.Write((int)kf.PositionEasing);
            bw.Write((int)kf.ScaleEasing);
            bw.Write((int)kf.RotationEasing);
        }

        /// <summary>
        /// Reads stage settings from the stream.
        /// </summary>
        private static void ReadStageSettings(BinaryReader br, StageSettings stage)
        {
            stage.Enabled = br.ReadBoolean();
            stage.OutputWidth = br.ReadInt32();
            stage.OutputHeight = br.ReadInt32();
            stage.StageX = br.ReadInt32();
            stage.StageY = br.ReadInt32();
            stage.StageWidth = br.ReadInt32();
            stage.StageHeight = br.ReadInt32();
            stage.ScalingAlgorithm = (StageScalingAlgorithm)br.ReadInt32();
            stage.RotationInterpolation = (StageRotationInterpolation)br.ReadInt32();
            stage.BoundsMode = (StageBoundsMode)br.ReadInt32();
        }

        /// <summary>
        /// Reads stage animation track from the stream.
        /// </summary>
        private static void ReadStageAnimationTrack(BinaryReader br, StageAnimationTrack track)
        {
            track.Id = new Guid(br.ReadBytes(16));
            track.Name = br.ReadString();

            // Clear existing keyframes and load new ones
            track.ClearKeyframes();

            int keyframeCount = br.ReadInt32();
            for (int i = 0; i < keyframeCount; i++)
            {
                var kf = ReadStageKeyframe(br);
                track.Keyframes.Add(kf);
            }
        }

        /// <summary>
        /// Reads a stage keyframe from the stream.
        /// </summary>
        private static StageKeyframeData ReadStageKeyframe(BinaryReader br)
        {
            return new StageKeyframeData
            {
                FrameIndex = br.ReadInt32(),
                PositionX = br.ReadSingle(),
                PositionY = br.ReadSingle(),
                ScaleX = br.ReadSingle(),
                ScaleY = br.ReadSingle(),
                UniformScale = br.ReadBoolean(),
                Rotation = br.ReadSingle(),
                PositionEasing = (EasingType)br.ReadInt32(),
                ScaleEasing = (EasingType)br.ReadInt32(),
                RotationEasing = (EasingType)br.ReadInt32()
            };
        }
    }
}
