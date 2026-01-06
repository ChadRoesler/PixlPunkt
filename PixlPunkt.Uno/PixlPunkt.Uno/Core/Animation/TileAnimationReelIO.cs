using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.Core.Settings;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Handles reading and writing tile animation reel files (.pxpr).
    /// Allows tile animations to be exported and imported for use as animation sub-routines.
    /// </summary>
    /// <remarks>
    /// <para><strong>File Format Versions:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Version 1:</strong> Stores tile coordinates only (requires original document)</item>
    /// <item><strong>Version 2:</strong> Stores embedded pixel data (portable, self-contained)</item>
    /// </list>
    /// </remarks>
    public static class TileAnimationReelIO
    {
        /// <summary>
        /// File extension for tile animation reel files.
        /// </summary>
        public const string ReelExtension = ".pxpr";

        /// <summary>
        /// Current file format version for new saves.
        /// </summary>
        private const int CurrentFormatVersion = 2;

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
        /// Saves a tile animation reel to a file with embedded pixel data.
        /// </summary>
        /// <param name="reel">The reel to save.</param>
        /// <param name="filePath">The path where to save the reel file.</param>
        /// <param name="document">The document to extract pixel data from.</param>
        /// <returns>True if save succeeded.</returns>
        public static bool Save(TileAnimationReel reel, string filePath, CanvasDocument document)
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
                    Save(reel, stream, document);
                    stream.Flush();
                }

                LoggingService.Info("Saved tile animation reel: {Name} -> {Path} (with embedded pixels)", reel.Name, filePath);
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to save tile animation reel to {Path}", ex, filePath);
                return false;
            }
        }

        /// <summary>
        /// Saves a tile animation reel to a file (legacy format without pixels).
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
        /// Saves a tile animation reel to a stream with embedded pixel data (v2 format).
        /// </summary>
        /// <param name="reel">The reel to save.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="document">The document to extract pixel data from.</param>
        public static void Save(TileAnimationReel reel, Stream stream, CanvasDocument document)
        {
            if (reel == null)
                throw new ArgumentNullException(nameof(reel));
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            // Get tile dimensions from document
            int tileWidth = document.TileSize.Width;
            int tileHeight = document.TileSize.Height;

            // Composite the document to get pixel data
            var composite = new PixelSurface(document.PixelWidth, document.PixelHeight);
            document.CompositeTo(composite);

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write("PXPR"); // Magic number
                writer.Write(CurrentFormatVersion); // Format version 2

                // Write reel metadata
                writer.Write(reel.Name);
                writer.Write(reel.DefaultFrameTimeMs);
                writer.Write(reel.Loop);
                writer.Write(reel.PingPong);

                // Write frame dimensions (new in v2)
                writer.Write(tileWidth);
                writer.Write(tileHeight);

                // Write frames with embedded pixel data
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

                    // Extract and write pixel data for this frame
                    byte[] framePixels = ExtractTilePixels(composite, frame.TileX, frame.TileY, tileWidth, tileHeight);
                    
                    // Compress the pixel data
                    byte[] compressedPixels = CompressPixels(framePixels);
                    writer.Write(compressedPixels.Length);
                    writer.Write(compressedPixels);
                }

                LoggingService.Debug("Serialized tile animation reel: {Name} with {FrameCount} frames ({TileW}x{TileH} pixels each)",
                    reel.Name, reel.Frames.Count, tileWidth, tileHeight);
            }
        }

        /// <summary>
        /// Saves a tile animation reel to a stream (legacy v1 format without pixels).
        /// </summary>
        public static void Save(TileAnimationReel reel, Stream stream)
        {
            if (reel == null)
                throw new ArgumentNullException(nameof(reel));

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write("PXPR"); // Magic number
                writer.Write(1); // Format version 1 (legacy)

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

                LoggingService.Debug("Serialized tile animation reel (v1): {Name} with {FrameCount} frames",
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
                    LoggingService.Info("Loaded tile animation reel: {Name} from {Path} (v{Version}, embedded={HasPixels})", 
                        reel?.Name, filePath, reel?.HasEmbeddedPixels == true ? "2" : "1", reel?.HasEmbeddedPixels);
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
                if (version < 1 || version > CurrentFormatVersion)
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

                // Read frame dimensions (v2+)
                int frameWidth = 0;
                int frameHeight = 0;
                if (version >= 2)
                {
                    frameWidth = reader.ReadInt32();
                    frameHeight = reader.ReadInt32();
                    reel.FrameWidth = frameWidth;
                    reel.FrameHeight = frameHeight;
                }

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

                    var frame = new ReelFrame(tileX, tileY, durationMs);

                    // Read embedded pixel data (v2+)
                    if (version >= 2)
                    {
                        int compressedLength = reader.ReadInt32();
                        byte[] compressedPixels = reader.ReadBytes(compressedLength);
                        frame.EmbeddedPixels = DecompressPixels(compressedPixels, frameWidth * frameHeight * 4);
                    }

                    reel.Frames.Add(frame);
                }

                LoggingService.Debug("Deserialized tile animation reel: {Name} with {FrameCount} frames (v{Version})",
                    reel.Name, reel.Frames.Count, version);

                return reel;
            }
        }

        /// <summary>
        /// Exports a tile animation reel as a .pxpr file with embedded pixel data.
        /// </summary>
        /// <param name="reel">The reel to export.</param>
        /// <param name="targetPath">The target file path.</param>
        /// <param name="document">The document to extract pixel data from.</param>
        /// <returns>The full path to the saved reel, or empty string if failed.</returns>
        public static string ExportReel(TileAnimationReel reel, string targetPath, CanvasDocument document)
        {
            if (reel == null || string.IsNullOrWhiteSpace(targetPath))
                return string.Empty;

            // Determine the final path - append extension only if not already present
            string finalPath;
            if (targetPath.EndsWith(ReelExtension, StringComparison.OrdinalIgnoreCase))
            {
                finalPath = targetPath;
            }
            else
            {
                finalPath = targetPath + ReelExtension;
            }

            // Save the reel with embedded pixels
            if (Save(reel, finalPath, document))
            {
                LoggingService.Info("Exported tile animation reel to: {Path}", finalPath);
                return finalPath;
            }

            return string.Empty;
        }

        /// <summary>
        /// Exports a tile animation reel as a .pxpr file (legacy format without pixels).
        /// </summary>
        /// <param name="reel">The reel to export.</param>
        /// <param name="targetPath">The target file path.</param>
        /// <returns>The full path to the saved reel, or empty string if failed.</returns>
        public static string ExportReel(TileAnimationReel reel, string targetPath)
        {
            if (reel == null || string.IsNullOrWhiteSpace(targetPath))
                return string.Empty;

            // Determine the final path - append extension only if not already present
            string finalPath;
            if (targetPath.EndsWith(ReelExtension, StringComparison.OrdinalIgnoreCase))
            {
                finalPath = targetPath;
            }
            else
            {
                finalPath = targetPath + ReelExtension;
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

        // ====================================================================
        // PRIVATE HELPER METHODS
        // ====================================================================

        /// <summary>
        /// Extracts pixel data for a single tile from the composite surface.
        /// </summary>
        private static byte[] ExtractTilePixels(PixelSurface composite, int tileX, int tileY, int tileWidth, int tileHeight)
        {
            byte[] pixels = new byte[tileWidth * tileHeight * 4];
            int srcX = tileX * tileWidth;
            int srcY = tileY * tileHeight;

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    int sx = srcX + x;
                    int sy = srcY + y;
                    int dstIdx = (y * tileWidth + x) * 4;

                    if (sx >= 0 && sx < composite.Width && sy >= 0 && sy < composite.Height)
                    {
                        int srcIdx = (sy * composite.Width + sx) * 4;
                        pixels[dstIdx + 0] = composite.Pixels[srcIdx + 0]; // B
                        pixels[dstIdx + 1] = composite.Pixels[srcIdx + 1]; // G
                        pixels[dstIdx + 2] = composite.Pixels[srcIdx + 2]; // R
                        pixels[dstIdx + 3] = composite.Pixels[srcIdx + 3]; // A
                    }
                    else
                    {
                        // Transparent if outside bounds
                        pixels[dstIdx + 0] = 0;
                        pixels[dstIdx + 1] = 0;
                        pixels[dstIdx + 2] = 0;
                        pixels[dstIdx + 3] = 0;
                    }
                }
            }

            return pixels;
        }

        /// <summary>
        /// Compresses pixel data using DEFLATE.
        /// </summary>
        private static byte[] CompressPixels(byte[] pixels)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(pixels, 0, pixels.Length);
            }
            return output.ToArray();
        }

        /// <summary>
        /// Decompresses pixel data from DEFLATE format.
        /// </summary>
        private static byte[] DecompressPixels(byte[] compressed, int expectedSize)
        {
            byte[] pixels = new byte[expectedSize];
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            int totalRead = 0;
            while (totalRead < expectedSize)
            {
                int read = deflate.Read(pixels, totalRead, expectedSize - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            return pixels;
        }
    }
}
