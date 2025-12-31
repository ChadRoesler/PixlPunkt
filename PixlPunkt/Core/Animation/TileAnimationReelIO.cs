using System;
using System.Collections.Generic;
using System.IO;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Animation
{
    /// <summary>
    /// Handles reading and writing tile animation reel files (.pxpr).
    /// Allows tile animations to be exported and imported for use as animation sub-routines.
    /// </summary>
    public static class TileAnimationReelIO
    {
        /// <summary>
        /// File extension for tile animation reel files.
        /// </summary>
        public const string ReelExtension = ".pxpr";

        /// <summary>
        /// Gets the tile animation reels directory path.
        /// </summary>
        public static string GetReelsDirectory()
        {
            return Path.Combine(AppPaths.RootDirectory, "Reels");
        }

        /// <summary>
        /// Ensures the tile animation reels directory exists.
        /// </summary>
        public static void EnsureDirectoryExists()
        {
            var dir = GetReelsDirectory();
            AppPaths.EnsureDirectoryExists(dir);
        }

        /// <summary>
        /// Saves a tile animation reel to a file.
        /// </summary>
        /// <param name="reel">The reel to save.</param>
        /// <param name="filePath">The path where to save the reel file.</param>
        /// <returns>True if save succeeded.</returns>
        public static bool Save(TileAnimationReel reel, string filePath)
        {
            if (reel == null || string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = File.OpenWrite(filePath))
                {
                    stream.SetLength(0); // Truncate file
                    Save(reel, stream);
                    stream.Flush();
                }

                LoggingService.Info("Saved tile animation reel: {Name} -> {Path}", reel.Name, filePath);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to save tile animation reel to {Path}", ex, filePath);
                return false;
            }
        }

        /// <summary>
        /// Saves a tile animation reel to a stream.
        /// </summary>
        public static void Save(TileAnimationReel reel, Stream stream)
        {
            if (reel == null)
                throw new ArgumentNullException(nameof(reel));

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write("PXPR"); // Magic number
                writer.Write(1); // Format version

                // Write reel metadata
                writer.Write(reel.Name);
                writer.Write(reel.DefaultFrameTimeMs);
                writer.Write(reel.Loop);
                writer.Write(reel.PingPong);

                // Write frames
                writer.Write(reel.Frames.Count);
                foreach (var frame in reel.Frames)
                {
                    writer.Write(frame.TileX);
                    writer.Write(frame.TileY);

                    if (frame.DurationMs.HasValue)
                    {
                        writer.Write(true); // Has custom duration
                        writer.Write(frame.DurationMs.Value);
                    }
                    else
                    {
                        writer.Write(false); // Use default duration
                    }
                }

                LoggingService.Debug("Serialized tile animation reel: {Name} with {FrameCount} frames",
                    reel.Name, reel.Frames.Count);
            }
        }

        /// <summary>
        /// Loads a tile animation reel from a file.
        /// </summary>
        /// <param name="filePath">The path to the reel file.</param>
        /// <returns>The loaded reel, or null if load failed.</returns>
        public static TileAnimationReel? Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                LoggingService.Warning("Tile animation reel file not found: {Path}", filePath);
                return null;
            }

            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var reel = Load(stream);
                    LoggingService.Info("Loaded tile animation reel: {Name} from {Path}", reel?.Name, filePath);
                    return reel;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to load tile animation reel from {Path}", ex, filePath);
                return null;
            }
        }

        /// <summary>
        /// Loads a tile animation reel from a stream.
        /// </summary>
        public static TileAnimationReel Load(Stream stream)
        {
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                // Verify magic number and version
                string magic = reader.ReadString();
                if (magic != "PXPR")
                    throw new InvalidDataException("Invalid reel file format (bad magic)");

                int version = reader.ReadInt32();
                if (version != 1)
                    throw new InvalidDataException($"Unsupported reel format version: {version}");

                // Read reel metadata
                string name = reader.ReadString();
                int defaultFrameTime = reader.ReadInt32();
                bool loop = reader.ReadBoolean();
                bool pingPong = reader.ReadBoolean();

                var reel = new TileAnimationReel(name)
                {
                    DefaultFrameTimeMs = defaultFrameTime,
                    Loop = loop,
                    PingPong = pingPong
                };

                // Read frames
                int frameCount = reader.ReadInt32();
                for (int i = 0; i < frameCount; i++)
                {
                    int tileX = reader.ReadInt32();
                    int tileY = reader.ReadInt32();

                    bool hasCustomDuration = reader.ReadBoolean();
                    int? durationMs = null;
                    if (hasCustomDuration)
                    {
                        durationMs = reader.ReadInt32();
                    }

                    reel.Frames.Add(new ReelFrame(tileX, tileY, durationMs));
                }

                LoggingService.Debug("Deserialized tile animation reel: {Name} with {FrameCount} frames",
                    reel.Name, reel.Frames.Count);

                return reel;
            }
        }

        /// <summary>
        /// Exports a tile animation reel as a .pxpr file to a user-selected location.
        /// </summary>
        /// <param name="reel">The reel to export.</param>
        /// <param name="suggestedFileName">Suggested file name (without extension).</param>
        /// <returns>The full path to the saved reel, or empty string if cancelled/failed.</returns>
        public static string ExportReel(TileAnimationReel reel, string suggestedFileName)
        {
            if (reel == null || string.IsNullOrWhiteSpace(suggestedFileName))
                return string.Empty;

            EnsureDirectoryExists();

            // Generate a default path in the reels directory
            var defaultDir = GetReelsDirectory();
            string basePath = Path.Combine(defaultDir, suggestedFileName);

            // Ensure unique filename
            string finalPath = basePath + ReelExtension;
            int counter = 1;
            while (File.Exists(finalPath))
            {
                finalPath = Path.Combine(defaultDir, $"{suggestedFileName}_{counter}{ReelExtension}");
                counter++;
            }

            // Save the reel
            if (Save(reel, finalPath))
            {
                LoggingService.Info("Exported tile animation reel to: {Path}", finalPath);
                return finalPath;
            }

            return string.Empty;
        }

        /// <summary>
        /// Imports a tile animation reel from a .pxpr file.
        /// </summary>
        /// <param name="filePath">The path to the reel file to import.</param>
        /// <returns>The imported reel, or null if import failed.</returns>
        public static TileAnimationReel? ImportReel(string filePath)
        {
            var reel = Load(filePath);
            if (reel != null)
            {
                LoggingService.Info("Imported tile animation reel from: {Path}", filePath);
            }
            return reel;
        }

        /// <summary>
        /// Enumerates all saved reel files in the reels directory.
        /// </summary>
        public static IEnumerable<string> EnumerateReels()
        {
            var dir = GetReelsDirectory();
            if (!Directory.Exists(dir))
                yield break;

            foreach (var path in Directory.GetFiles(dir, $"*{ReelExtension}", SearchOption.TopDirectoryOnly))
            {
                yield return path;
            }
        }
    }
}
