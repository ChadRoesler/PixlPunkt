using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Helpers;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.Core.Logging;
using Windows.Graphics;
using static PixlPunkt.Uno.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Uno.Core.Document
{
    /// <summary>
    /// Importers for foreign pixel art and icon file formats into native PixlPunkt documents.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ForeignDocumentImporter provides conversion utilities to load documents from other pixel art
    /// tools and image formats into PixlPunkt's native <see cref="CanvasDocument"/> structure.
    /// All importers produce fully-formed documents with proper layer structure, blend modes, and
    /// canvas geometry.
    /// </para>
    /// <para><strong>Supported Formats:</strong></para>
    /// <list type="bullet">
    /// <item><strong>.pyxel (PyxelEdit)</strong>: Full layer stack import with blend modes, visibility,
    /// opacity, and tileset configuration. Handles ZIP-based file structure with JSON metadata.</item>
    /// <item><strong>.ase / .aseprite (Aseprite)</strong>: Scaffolded but not yet implemented.
    /// Planned support for frame 0 layer import with full property mapping.</item>
    /// <item><strong>.ico (Windows Icon)</strong>: Imports largest available icon frame as a single-layer
    /// document with automatic size detection.</item>
    /// <item><strong>.cur (Windows Cursor)</strong>: Imports largest available cursor frame as a single-layer
    /// document with automatic size detection.</item>
    /// </list>
    /// <para><strong>Design Philosophy:</strong></para>
    /// <para>
    /// All importers follow a common pattern: detect format from extension, parse metadata, create
    /// appropriately-sized <see cref="CanvasDocument"/>, populate layers with pixel data and properties,
    /// and regenerate the composite surface. Tile dimensions are either derived from format-specific
    /// tilesets or use sensible defaults (16×16 for icons, format-specific for pixel art tools).
    /// </para>
    /// </remarks>
    /// <seealso cref="CanvasDocument"/>
    /// <seealso cref="DocumentIO"/>
    public static class ForeignDocumentImporter
    {
        // ──────────────────────────────────────────────────────────────────
        // PUBLIC DISPATCH
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Imports a foreign file into a new CanvasDocument, auto-detecting format by extension.
        /// </summary>
        /// <param name="filePath">Path to the foreign document file.</param>
        /// <returns>A fully initialized <see cref="CanvasDocument"/> with layers and composite surface.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
        /// <exception cref="NotSupportedException">Thrown if the file extension is not supported.</exception>
        /// <exception cref="InvalidDataException">Thrown if the file format is invalid or corrupted.</exception>
        /// <remarks>
        /// <para><strong>Currently Supported Extensions:</strong></para>
        /// <list type="bullet">
        /// <item>.pyxel → <see cref="ImportPyxel"/></item>
        /// <item>.ase, .aseprite → <see cref="ImportAseprite"/></item>
        /// <item>.ico → <see cref="ImportIconAsDocument"/></item>
        /// <item>.cur → <see cref="ImportCursorAsDocument"/></item>
        /// <item>.tmx → <see cref="ImportTmx"/></item>
        /// <item>.tsx → <see cref="ImportTsx"/></item>
        /// </list>
        /// </remarks>
        public static CanvasDocument ImportFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            LoggingService.Info("Importing foreign document {FilePath} (ext={Ext})", filePath, ext);

            try
            {
                return ext switch
                {
                    ".pyxel" => ImportPyxel(filePath),
                    ".ase" or ".aseprite" => ImportAseprite(filePath),
                    ".ico" => ImportIconAsDocument(filePath),
                    ".cur" => ImportCursorAsDocument(filePath),
                    ".tmx" => ImportTmx(filePath),
                    ".tsx" => ImportTsx(filePath),
                    _ => throw new NotSupportedException(
                        $"Unsupported foreign file extension '{ext}'. Supported: .pyxel, .ase/.aseprite, .ico, .cur, .tmx, .tsx")
                };
            }
            catch (Exception ex)
            {
                LoggingService.Error($"Failed to import {filePath}", ex);
                throw;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // P Y X E L E D I T   (.pyxel)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Imports a PyxelEdit .pyxel document with full layer support and property mapping.
        /// </summary>
        /// <param name="filePath">Path to the .pyxel file.</param>
        /// <returns>A <see cref="CanvasDocument"/> with all layers, blend modes, and tileset configuration.</returns>
        /// <exception cref="InvalidDataException">
        /// Thrown if the file is not a valid PyxelEdit document or is missing required metadata.
        /// </exception>
        /// <remarks>
        /// <para><strong>Import Process:</strong></para>
        /// <list type="number">
        /// <item>Open ZIP archive and read docData.json metadata</item>
        /// <item>Extract canvas dimensions (width, height) and tileset configuration (tileWidth, tileHeight)</item>
        /// <item>Create <see cref="CanvasDocument"/> with computed tile counts = canvasSize / tileSize</item>
        /// <item>Sort layers by numeric key ("0", "1", "2", ...) and reverse for bottom-to-top order</item>
        /// <item>For each layer:
        ///   <list type="bullet">
        ///   <item>Load pixel data from layerN.png embedded in ZIP</item>
        ///   <item>Create <see cref="RasterLayer"/> and copy pixels</item>
        ///   <item>Map properties: visible (inverted from 'hidden'), opacity (alpha 0-255), blend mode</item>
        ///   <item>Update layer preview thumbnail</item>
        ///   </list>
        /// </item>
        /// <item>Regenerate composite surface</item>
        /// </list>
        /// <para><strong>PyxelEdit Blend Mode Mapping:</strong></para>
        /// <para>
        /// Maps PyxelEdit string blend modes to <see cref="BlendMode"/> enum:
        /// "normal" → Normal, "multiply" → Multiply, "screen" → Screen, "overlay" → Overlay,
        /// "add"/"addition"/"linear_dodge" → Add, "subtract" → Subtract.
        /// Unknown modes default to Normal.
        /// </para>
        /// <para><strong>Current Limitations:</strong></para>
        /// <list type="bullet">
        /// <item>Layer groups/folders are ignored (only flat layer list)</item>
        /// <item>Tileset mapping and animation data not imported</item>
        /// <item>All layers must match canvas dimensions (no offset/partial layers)</item>
        /// </list>
        /// </remarks>
        public static CanvasDocument ImportPyxel(string filePath)
        {
            using var zip = ZipFile.OpenRead(filePath);

            // ── 1) Read docData.json ──────────────────────────────────────
            var docEntry = zip.GetEntry("docData.json")
                ?? throw new InvalidDataException("PyxelEdit file is missing docData.json.");

            PyxelDocDto docJson;
            using (var s = docEntry.Open())
            using (var sr = new StreamReader(s))
            {
                string json = sr.ReadToEnd();
                docJson = JsonSerializer.Deserialize<PyxelDocDto>(json, PyxelJsonOptions)
                    ?? throw new InvalidDataException("Failed to parse PyxelEdit docData.json.");
            }

            if (docJson.Canvas == null)
                throw new InvalidDataException("PyxelEdit docData.json: missing 'canvas'.");
            if (docJson.Tileset == null)
                throw new InvalidDataException("PyxelEdit docData.json: missing 'tileset'.");

            int pixelWidth = docJson.Canvas.Width;
            int pixelHeight = docJson.Canvas.Height;

            int tileW = docJson.Tileset.TileWidth <= 0 ? 1 : docJson.Tileset.TileWidth;
            int tileH = docJson.Tileset.TileHeight <= 0 ? 1 : docJson.Tileset.TileHeight;

            // Derive tile counts from canvas size – first pass assumption.
            int tilesX = Math.Max(1, pixelWidth / tileW);
            int tilesY = Math.Max(1, pixelHeight / tileH);

            var tileSize = CreateSize(tileW, tileH);
            var tileCounts = CreateSize(tilesX, tilesY);

            string docName = Path.GetFileNameWithoutExtension(filePath);

            LoggingService.Info("Pyxel import {FilePath}: size={W}x{H}, tile={TW}x{TH}, tiles={TX}x{TY}",
                filePath, pixelWidth, pixelHeight, tileW, tileH, tilesX, tilesY);

            var doc = new CanvasDocument(
                docName,
                pixelWidth,
                pixelHeight,
                tileSize,
                tileCounts);
            // ── 2) Sort layers by numeric key: "0", "1", "2", ... ─────────
            var layers = docJson.Canvas.Layers ?? [];

            var ordered = layers
                .Select(kvp =>
                {
                    int index = 0;
                    int.TryParse(kvp.Key, out index);
                    return (index, dto: kvp.Value, key: kvp.Key);
                })
                .OrderBy(t => t.index)
                .ToList();
            ordered.Reverse();

            foreach (var (index, dto, key) in ordered)
            {
                string layerPngName = $"layer{index}.png";

                var pngEntry = zip.GetEntry(layerPngName);
                if (pngEntry == null)
                {
                    // No pixels for this layer (e.g. pure group), skip for first pass.
                    continue;
                }

                // 2D pixels from PNG
                PixelSurface layerSurface = LoadSurfaceFromPng(pngEntry);

                if (layerSurface.Width != pixelWidth || layerSurface.Height != pixelHeight)
                {
                    LoggingService.Warning("Pyxel layer {LayerIndex} size {LW}x{LH} does not match canvas {CW}x{CH}", index, layerSurface.Width, layerSurface.Height, pixelWidth, pixelHeight);
                    throw new InvalidDataException($"Pyxel layer {index} bitmap size does not match canvas.");
                }

                // Create a new raster layer in PixlPunkt
                int newIndex = doc.AddLayer(dto?.Name ?? $"Layer {index}");
                if (newIndex < 0 || newIndex >= doc.Layers.Count)
                    throw new InvalidOperationException("Document.AddLayer returned invalid index.");

                if (doc.Layers[newIndex] is not RasterLayer rl)
                    throw new InvalidOperationException("Unexpected non-raster layer returned by AddLayer.");

                // Copy pixel data into our surface
                Buffer.BlockCopy(
                    layerSurface.Pixels, 0,
                    rl.Surface.Pixels, 0,
                    rl.Surface.Pixels.Length);

                // Map basic flags
                rl.Visible = !(dto?.Hidden ?? false);
                rl.Locked = false; // Pyxel doesn't really have lock in this way
                rl.Opacity = (byte)Math.Clamp(dto?.Alpha ?? 255, 0, 255);
                rl.Blend = MapPyxelBlend(dto?.BlendMode);
                rl.UpdatePreview();
            }

            // Ensure composite is up-to-date
            doc.CompositeTo(doc.Surface);

            LoggingService.Info("Pyxel import complete for {FilePath}: {LayerCount} layers", filePath, doc.Layers.Count);
            return doc;
        }

        /// <summary>
        /// JSON deserialization options for PyxelEdit metadata.
        /// </summary>
        private static readonly JsonSerializerOptions PyxelJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Maps PyxelEdit blend mode string to PixlPunkt <see cref="BlendMode"/> enum.
        /// </summary>
        /// <param name="pyxelBlend">PyxelEdit blend mode name (case-insensitive).</param>
        /// <returns>Corresponding <see cref="BlendMode"/> value, or <see cref="BlendMode.Normal"/> if unknown.</returns>
        private static BlendMode MapPyxelBlend(string? pyxelBlend)
        {
            if (string.IsNullOrEmpty(pyxelBlend))
                return BlendMode.Normal;

            return pyxelBlend.ToLowerInvariant() switch
            {
                "normal" => BlendMode.Normal,
                "multiply" => BlendMode.Multiply,
                "screen" => BlendMode.Screen,
                "overlay" => BlendMode.Overlay,
                "add" or "addition" or "linear_dodge" => BlendMode.Add,
                "subtract" => BlendMode.Subtract,
                // Fallback to normal; we can refine as we learn more modes.
                _ => BlendMode.Normal
            };
        }

        /// <summary>
        /// Loads a PixelSurface from a PNG stored within a ZIP archive entry.
        /// </summary>
        /// <param name="entry">ZIP entry containing PNG image data.</param>
        /// <returns>A <see cref="PixelSurface"/> with pixels in BGRA format.</returns>
        /// <remarks>
        /// <para>
        /// Reads PNG data using <see cref="System.Drawing.Bitmap"/>, locks bits as Format32bppArgb,
        /// and copies to <see cref="PixelSurface.Pixels"/> byte array. Assumes premultiplied alpha
        /// and BGRA/ARGB memory layout matching PixlPunkt's internal format.
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Uses unsafe pointer copy via <see cref="System.Runtime.InteropServices.Marshal.Copy(nint, byte[], int, int)"/>
        /// for direct memory transfer without per-pixel iteration.
        /// </para>
        /// </remarks>
        private static PixelSurface LoadSurfaceFromPng(ZipArchiveEntry entry)
        {
            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            ms.Position = 0;

            using var bmp = new System.Drawing.Bitmap(ms);
            int w = bmp.Width;
            int h = bmp.Height;

            var surf = new PixelSurface(w, h);

            var rect = new System.Drawing.Rectangle(0, 0, w, h);
            var data = bmp.LockBits(
                rect,
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                int byteCount = w * h * 4;
                System.Runtime.InteropServices.Marshal.Copy(
                    data.Scan0,
                    surf.Pixels,
                    0,
                    byteCount);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return surf;
        }

        // ──────────────────────────────────────────────────────────────────
        // A S E P R I T E   (.ase / .aseprite)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Imports an Aseprite file, extracting frame 0 layers into a CanvasDocument.
        /// </summary>
        /// <param name="filePath">Path to the .ase or .aseprite file.</param>
        /// <returns>A <see cref="CanvasDocument"/> with frame 0 layers.</returns>
        /// <exception cref="InvalidDataException">Thrown if the file is not a valid Aseprite document.</exception>
        /// <remarks>
        /// <para><strong>Aseprite Binary Format Overview:</strong></para>
        /// <para>
        /// Aseprite uses a chunk-based binary format with ZLIB compression for pixel data.
        /// This importer extracts frame 0 (first frame) and creates layers from cel data.
        /// </para>
        /// <para><strong>Supported Features:</strong></para>
        /// <list type="bullet">
        /// <item>RGBA and Indexed color modes (Grayscale not yet supported)</item>
        /// <item>Layer names, visibility, opacity, and blend modes</item>
        /// <item>Compressed (ZLIB) and uncompressed cel data</item>
        /// <item>Linked cels (reference to previous frame cel)</item>
        /// </list>
        /// <para><strong>Limitations:</strong></para>
        /// <list type="bullet">
        /// <item>Only frame 0 is imported (animations not preserved)</item>
        /// <item>Layer groups are flattened</item>
        /// <item>Tilemap layers not yet supported</item>
        /// </list>
        /// </remarks>
        public static CanvasDocument ImportAseprite(string filePath)
        {
            using var fs = File.OpenRead(filePath);
            using var br = new BinaryReader(fs);

            // ── 1) Read File Header ──────────────────────────────────────
            uint fileSize = br.ReadUInt32();
            ushort magic = br.ReadUInt16();
            if (magic != 0xA5E0)
                throw new InvalidDataException("Not a valid Aseprite file (bad magic number).");

            ushort frameCount = br.ReadUInt16();
            ushort width = br.ReadUInt16();
            ushort height = br.ReadUInt16();
            ushort colorDepth = br.ReadUInt16(); // 8=indexed, 16=grayscale, 32=RGBA
            uint flags = br.ReadUInt32();
            ushort speed = br.ReadUInt16(); // deprecated
            br.ReadUInt32(); // reserved (0)
            br.ReadUInt32(); // reserved (0)
            byte transparentIndex = br.ReadByte();
            br.ReadBytes(3); // ignore
            ushort numColors = br.ReadUInt16();
            byte pixelWidth = br.ReadByte();
            byte pixelHeight = br.ReadByte();
            short gridX = br.ReadInt16();
            short gridY = br.ReadInt16();
            ushort gridWidth = br.ReadUInt16();
            ushort gridHeight = br.ReadUInt16();
            br.ReadBytes(84); // reserved

            if (colorDepth != 32 && colorDepth != 8)
            {
                LoggingService.Warning("Aseprite file has unsupported color depth {ColorDepth}, attempting to import anyway", colorDepth);
            }

            LoggingService.Info("Aseprite import {FilePath}: {W}x{H}, depth={Depth}, frames={Frames}",
                filePath, width, height, colorDepth, frameCount);

            // ── 2) Parse frames (we only care about frame 0) ─────────────
            var layers = new List<AseLayer>();
            var cels = new List<AseCel>();
            uint[]? palette = null;

            // Read frame 0
            if (frameCount > 0)
            {
                uint frameBytes = br.ReadUInt32();
                ushort frameMagic = br.ReadUInt16();
                if (frameMagic != 0xF1FA)
                    throw new InvalidDataException("Invalid frame magic in Aseprite file.");

                ushort oldChunkCount = br.ReadUInt16();
                ushort frameDuration = br.ReadUInt16();
                br.ReadBytes(2); // reserved
                uint newChunkCount = br.ReadUInt32();

                uint chunkCount = newChunkCount == 0 ? oldChunkCount : newChunkCount;

                for (uint c = 0; c < chunkCount; c++)
                {
                    long chunkStart = fs.Position;
                    uint chunkSize = br.ReadUInt32();
                    ushort chunkType = br.ReadUInt16();

                    switch (chunkType)
                    {
                        case 0x2004: // Layer chunk
                            layers.Add(ReadAseLayerChunk(br));
                            break;

                        case 0x2005: // Cel chunk
                            cels.Add(ReadAseCelChunk(br, colorDepth, width, height));
                            break;

                        case 0x2019: // Palette chunk
                            palette = ReadAsePaletteChunk(br);
                            break;

                        case 0x0004: // Old palette chunk (deprecated but still used)
                            if (palette == null)
                                palette = ReadAseOldPaletteChunk(br);
                            break;
                    }

                    // Skip to end of chunk
                    fs.Position = chunkStart + chunkSize;
                }
            }

            // ── 3) Create document ───────────────────────────────────────
            string docName = Path.GetFileNameWithoutExtension(filePath);

            // Use grid dimensions for tile size if available, otherwise default 16x16
            int tileW = gridWidth > 0 ? gridWidth : 16;
            int tileH = gridHeight > 0 ? gridHeight : 16;
            int tilesX = Math.Max(1, width / tileW);
            int tilesY = Math.Max(1, height / tileH);

            var doc = new CanvasDocument(
                docName,
                width,
                height,
                CreateSize(tileW, tileH),
                CreateSize(tilesX, tilesY));

            // ── 4) Create layers from cels ───────────────────────────────
            // Sort cels by layer index and build layers bottom-to-top
            var celsByLayer = cels
                .GroupBy(c => c.LayerIndex)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var group in celsByLayer)
            {
                int layerIdx = group.Key;
                var cel = group.First(); // Frame 0 cel

                // Get layer info if available
                string layerName = layerIdx < layers.Count ? layers[layerIdx].Name : $"Layer {layerIdx}";
                bool visible = layerIdx < layers.Count ? layers[layerIdx].Visible : true;
                byte opacity = layerIdx < layers.Count ? layers[layerIdx].Opacity : (byte)255;
                BlendMode blend = layerIdx < layers.Count ? MapAseBlend(layers[layerIdx].BlendMode) : BlendMode.Normal;

                // Skip group layers (they have no pixel data)
                if (layerIdx < layers.Count && layers[layerIdx].Type != 0)
                    continue;

                // Create new layer
                int newIdx = doc.AddLayer(layerName);
                if (doc.Layers[newIdx] is not RasterLayer rl)
                    continue;

                // Compose cel pixels onto layer surface
                if (cel.Pixels != null && cel.Width > 0 && cel.Height > 0)
                {
                    byte[] finalPixels;
                    if (colorDepth == 8 && palette != null)
                    {
                        // Convert indexed to BGRA
                        finalPixels = ConvertIndexedToBgra(cel.Pixels, palette, transparentIndex);
                    }
                    else
                    {
                        // Already RGBA, convert to BGRA
                        finalPixels = ConvertRgbaToBgra(cel.Pixels);
                    }

                    // Blit cel onto layer at position
                    BlitPixels(finalPixels, cel.Width, cel.Height, cel.X, cel.Y, rl.Surface);
                }

                rl.Visible = visible;
                rl.Opacity = opacity;
                rl.Blend = blend;
                rl.UpdatePreview();
            }

            // Ensure composite is up-to-date
            doc.CompositeTo(doc.Surface);

            LoggingService.Info("Aseprite import complete for {FilePath}: {LayerCount} layers", filePath, doc.Layers.Count);
            return doc;
        }

        // ── Aseprite Helper Types ────────────────────────────────────────

        private sealed class AseLayer
        {
            public ushort Flags { get; set; }
            public ushort Type { get; set; } // 0=normal, 1=group, 2=tilemap
            public string Name { get; set; } = string.Empty;
            public byte Opacity { get; set; } = 255;
            public ushort BlendMode { get; set; }
            public bool Visible => (Flags & 1) != 0;
        }

        private sealed class AseCel
        {
            public ushort LayerIndex { get; set; }
            public short X { get; set; }
            public short Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[]? Pixels { get; set; } // Raw pixel data (RGBA or indexed)
        }

        private static AseLayer ReadAseLayerChunk(BinaryReader br)
        {
            ushort flags = br.ReadUInt16();
            ushort type = br.ReadUInt16();
            ushort childLevel = br.ReadUInt16();
            ushort defaultWidth = br.ReadUInt16(); // ignored
            ushort defaultHeight = br.ReadUInt16(); // ignored
            ushort blendMode = br.ReadUInt16();
            byte opacity = br.ReadByte();
            br.ReadBytes(3); // reserved

            ushort nameLen = br.ReadUInt16();
            string name = nameLen > 0
                ? System.Text.Encoding.UTF8.GetString(br.ReadBytes(nameLen))
                : string.Empty;

            return new AseLayer
            {
                Flags = flags,
                Type = type,
                Name = name,
                Opacity = opacity,
                BlendMode = blendMode
            };
        }

        private static AseCel ReadAseCelChunk(BinaryReader br, ushort colorDepth, int docWidth, int docHeight)
        {
            ushort layerIndex = br.ReadUInt16();
            short x = br.ReadInt16();
            short y = br.ReadInt16();
            byte opacity = br.ReadByte();
            ushort celType = br.ReadUInt16();
            short zIndex = br.ReadInt16();
            br.ReadBytes(5); // reserved

            var cel = new AseCel { LayerIndex = layerIndex, X = x, Y = y };

            switch (celType)
            {
                case 0: // Raw cel
                    {
                        ushort w = br.ReadUInt16();
                        ushort h = br.ReadUInt16();
                        int bytesPerPixel = colorDepth / 8;
                        cel.Width = w;
                        cel.Height = h;
                        cel.Pixels = br.ReadBytes(w * h * bytesPerPixel);
                    }
                    break;

                case 1: // Linked cel (reference to previous frame)
                    // We only process frame 0, so linked cels are empty
                    cel.Width = 0;
                    cel.Height = 0;
                    break;

                case 2: // Compressed cel (ZLIB)
                    {
                        ushort w = br.ReadUInt16();
                        ushort h = br.ReadUInt16();
                        cel.Width = w;
                        cel.Height = h;

                        // Read remaining chunk data as compressed
                        // The chunk size includes header, so we need to decompress what's left
                        using var zlibStream = new System.IO.Compression.ZLibStream(
                            br.BaseStream,
                            System.IO.Compression.CompressionMode.Decompress,
                            leaveOpen: true);

                        int bytesPerPixel = colorDepth / 8;
                        int totalBytes = w * h * bytesPerPixel;
                        cel.Pixels = new byte[totalBytes];

                        int read = 0;
                        while (read < totalBytes)
                        {
                            int n = zlibStream.Read(cel.Pixels, read, totalBytes - read);
                            if (n == 0) break;
                            read += n;
                        }
                    }
                    break;

                case 3: // Compressed tilemap (not yet supported)
                    LoggingService.Warning("Aseprite tilemap cels not yet supported");
                    break;
            }

            return cel;
        }

        private static uint[] ReadAsePaletteChunk(BinaryReader br)
        {
            uint paletteSize = br.ReadUInt32();
            uint firstIndex = br.ReadUInt32();
            uint lastIndex = br.ReadUInt32();
            br.ReadBytes(8); // reserved

            var palette = new uint[256];

            for (uint i = firstIndex; i <= lastIndex && i < 256; i++)
            {
                ushort flags = br.ReadUInt16();
                byte r = br.ReadByte();
                byte g = br.ReadByte();
                byte b = br.ReadByte();
                byte a = br.ReadByte();

                // Store as BGRA
                palette[i] = (uint)((a << 24) | (r << 16) | (g << 8) | b);

                // Skip name if present
                if ((flags & 1) != 0)
                {
                    ushort nameLen = br.ReadUInt16();
                    br.ReadBytes(nameLen);
                }
            }

            return palette;
        }

        private static uint[] ReadAseOldPaletteChunk(BinaryReader br)
        {
            var palette = new uint[256];
            ushort packets = br.ReadUInt16();

            int index = 0;
            for (int p = 0; p < packets; p++)
            {
                byte skip = br.ReadByte();
                index += skip;

                byte count = br.ReadByte();
                int numColors = count == 0 ? 256 : count;

                for (int c = 0; c < numColors && index < 256; c++, index++)
                {
                    byte r = br.ReadByte();
                    byte g = br.ReadByte();
                    byte b = br.ReadByte();
                    palette[index] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
                }
            }

            return palette;
        }

        private static BlendMode MapAseBlend(ushort aseBlend)
        {
            return aseBlend switch
            {
                0 => BlendMode.Normal,
                1 => BlendMode.Multiply,
                2 => BlendMode.Screen,
                3 => BlendMode.Overlay,
                // 4-15: Various unsupported blend modes, default to Normal
                16 => BlendMode.Add, // Addition
                17 => BlendMode.Subtract,
                _ => BlendMode.Normal
            };
        }

        private static byte[] ConvertRgbaToBgra(byte[] rgba)
        {
            var bgra = new byte[rgba.Length];
            for (int i = 0; i < rgba.Length; i += 4)
            {
                bgra[i + 0] = rgba[i + 2]; // B <- R
                bgra[i + 1] = rgba[i + 1]; // G <- G
                bgra[i + 2] = rgba[i + 0]; // R <- B
                bgra[i + 3] = rgba[i + 3]; // A <- A
            }
            return bgra;
        }

        private static byte[] ConvertIndexedToBgra(byte[] indexed, uint[] palette, byte transparentIndex)
        {
            var bgra = new byte[indexed.Length * 4];
            for (int i = 0; i < indexed.Length; i++)
            {
                byte idx = indexed[i];
                uint color = idx == transparentIndex ? 0 : palette[idx];

                bgra[i * 4 + 0] = (byte)(color & 0xFF);        // B
                bgra[i * 4 + 1] = (byte)((color >> 8) & 0xFF);  // G
                bgra[i * 4 + 2] = (byte)((color >> 16) & 0xFF); // R
                bgra[i * 4 + 3] = (byte)((color >> 24) & 0xFF); // A
            }
            return bgra;
        }

        private static void BlitPixels(byte[] src, int srcW, int srcH, int dstX, int dstY, PixelSurface dst)
        {
            int dstW = dst.Width;
            int dstH = dst.Height;

            for (int sy = 0; sy < srcH; sy++)
            {
                int dy = dstY + sy;
                if (dy < 0 || dy >= dstH) continue;

                for (int sx = 0; sx < srcW; sx++)
                {
                    int dx = dstX + sx;
                    if (dx < 0 || dx >= dstW) continue;

                    int srcIdx = (sy * srcW + sx) * 4;
                    int dstIdx = (dy * dstW + dx) * 4;

                    dst.Pixels[dstIdx + 0] = src[srcIdx + 0];
                    dst.Pixels[dstIdx + 1] = src[srcIdx + 1];
                    dst.Pixels[dstIdx + 2] = src[srcIdx + 2];
                    dst.Pixels[dstIdx + 3] = src[srcIdx + 3];
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // T I L E D   T M X   (.tmx)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Imports a Tiled TMX tilemap file as a CanvasDocument.
        /// </summary>
        /// <param name="filePath">Path to the .tmx file.</param>
        /// <returns>A <see cref="CanvasDocument"/> representing the rendered tilemap.</returns>
        /// <exception cref="InvalidDataException">Thrown if the file is not a valid TMX document.</exception>
        /// <remarks>
        /// <para><strong>Import Process:</strong></para>
        /// <list type="number">
        /// <item>Parse TMX XML to extract map dimensions and tile size</item>
        /// <item>Load embedded or external tileset images</item>
        /// <item>Render each layer by placing tiles according to GID data</item>
        /// <item>Create CanvasDocument with one RasterLayer per tilemap layer</item>
        /// </list>
        /// <para><strong>Supported Features:</strong></para>
        /// <list type="bullet">
        /// <item>CSV and base64 (uncompressed) tile data encoding</item>
        /// <item>External and embedded tilesets</item>
        /// <item>Multiple tile layers</item>
        /// <item>Layer visibility and opacity</item>
        /// </list>
        /// <para><strong>Limitations:</strong></para>
        /// <list type="bullet">
        /// <item>Object layers are ignored</item>
        /// <item>Compressed tile data (gzip/zlib) requires additional handling</item>
        /// <item>Animated tiles show only first frame</item>
        /// </list>
        /// </remarks>
        public static CanvasDocument ImportTmx(string filePath)
        {
            var doc = System.Xml.Linq.XDocument.Load(filePath);
            var mapElement = doc.Root ?? throw new InvalidDataException("TMX file has no root element.");

            if (mapElement.Name.LocalName != "map")
                throw new InvalidDataException("TMX file root element is not 'map'.");

            // Parse map attributes
            int mapWidth = int.Parse(mapElement.Attribute("width")?.Value ?? "0");
            int mapHeight = int.Parse(mapElement.Attribute("height")?.Value ?? "0");
            int tileWidth = int.Parse(mapElement.Attribute("tilewidth")?.Value ?? "16");
            int tileHeight = int.Parse(mapElement.Attribute("tileheight")?.Value ?? "16");

            if (mapWidth <= 0 || mapHeight <= 0)
                throw new InvalidDataException("TMX map has invalid dimensions.");

            int pixelWidth = mapWidth * tileWidth;
            int pixelHeight = mapHeight * tileHeight;

            LoggingService.Info("TMX import {FilePath}: map={W}x{H} tiles, tile={TW}x{TH}px",
                filePath, mapWidth, mapHeight, tileWidth, tileHeight);

            // Load tilesets
            var tilesets = new List<TmxTileset>();
            string baseDir = Path.GetDirectoryName(filePath) ?? ".";

            foreach (var tsElement in mapElement.Elements("tileset"))
            {
                var tileset = LoadTmxTileset(tsElement, baseDir, tileWidth, tileHeight);
                if (tileset != null)
                    tilesets.Add(tileset);
            }

            // Create document
            string docName = Path.GetFileNameWithoutExtension(filePath);
            var canvasDoc = new CanvasDocument(
                docName,
                pixelWidth,
                pixelHeight,
                CreateSize(tileWidth, tileHeight),
                CreateSize(mapWidth, mapHeight));

            // Process each tile layer
            foreach (var layerElement in mapElement.Elements("layer"))
            {
                string layerName = layerElement.Attribute("name")?.Value ?? "Layer";
                int layerWidth = int.Parse(layerElement.Attribute("width")?.Value ?? mapWidth.ToString());
                int layerHeight = int.Parse(layerElement.Attribute("height")?.Value ?? mapHeight.ToString());
                bool visible = layerElement.Attribute("visible")?.Value != "0";
                float opacity = float.Parse(layerElement.Attribute("opacity")?.Value ?? "1", System.Globalization.CultureInfo.InvariantCulture);

                var dataElement = layerElement.Element("data");
                if (dataElement == null) continue;

                string encoding = dataElement.Attribute("encoding")?.Value ?? "";
                string compression = dataElement.Attribute("compression")?.Value ?? "";

                uint[] tileGids = ParseTmxTileData(dataElement, encoding, compression, layerWidth * layerHeight);

                // Create layer
                int layerIdx = canvasDoc.AddLayer(layerName);
                if (canvasDoc.Layers[layerIdx] is not RasterLayer rl) continue;

                rl.Visible = visible;
                rl.Opacity = (byte)Math.Clamp((int)(opacity * 255), 0, 255);

                // Render tiles
                RenderTmxLayer(tileGids, layerWidth, layerHeight, tileWidth, tileHeight, tilesets, rl.Surface);
                rl.UpdatePreview();
            }

            canvasDoc.CompositeTo(canvasDoc.Surface);
            LoggingService.Info("TMX import complete for {FilePath}: {LayerCount} layers", filePath, canvasDoc.Layers.Count);
            return canvasDoc;
        }

        // ── TMX/TSX Helper Types and Methods ─────────────────────────────

        private sealed class TmxTileset
        {
            public int FirstGid { get; set; }
            public int TileWidth { get; set; }
            public int TileHeight { get; set; }
            public int TileCount { get; set; }
            public int Columns { get; set; }
            public byte[]? ImagePixels { get; set; } // BGRA
            public int ImageWidth { get; set; }
            public int ImageHeight { get; set; }
        }

        private static TmxTileset? LoadTmxTileset(System.Xml.Linq.XElement tsElement, string baseDir, int defaultTileW, int defaultTileH)
        {
            int firstGid = int.Parse(tsElement.Attribute("firstgid")?.Value ?? "1");

            string? source = tsElement.Attribute("source")?.Value;
            if (!string.IsNullOrEmpty(source))
            {
                string tsxPath = Path.IsPathRooted(source) ? source : Path.Combine(baseDir, source);
                if (File.Exists(tsxPath))
                {
                    var tsxDoc = System.Xml.Linq.XDocument.Load(tsxPath);
                    var tsxRoot = tsxDoc.Root;
                    if (tsxRoot != null)
                    {
                        tsElement = tsxRoot;
                        baseDir = Path.GetDirectoryName(tsxPath) ?? baseDir;
                    }
                }
            }

            int tileWidth = int.Parse(tsElement.Attribute("tilewidth")?.Value ?? defaultTileW.ToString());
            int tileHeight = int.Parse(tsElement.Attribute("tileheight")?.Value ?? defaultTileH.ToString());
            int tileCount = int.Parse(tsElement.Attribute("tilecount")?.Value ?? "0");
            int columns = int.Parse(tsElement.Attribute("columns")?.Value ?? "1");

            var imageElement = tsElement.Element("image");
            if (imageElement == null) return null;

            string imageSrc = imageElement.Attribute("source")?.Value ?? "";
            string imagePath = Path.IsPathRooted(imageSrc) ? imageSrc : Path.Combine(baseDir, imageSrc);

            if (!File.Exists(imagePath))
            {
                LoggingService.Warning("TMX tileset image not found: {Path}", imagePath);
                return null;
            }

            using var bmp = new Bitmap(imagePath);
            var tileset = new TmxTileset
            {
                FirstGid = firstGid,
                TileWidth = tileWidth,
                TileHeight = tileHeight,
                TileCount = tileCount,
                Columns = columns > 0 ? columns : Math.Max(1, bmp.Width / tileWidth),
                ImageWidth = bmp.Width,
                ImageHeight = bmp.Height
            };

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                tileset.ImagePixels = new byte[bmp.Width * bmp.Height * 4];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, tileset.ImagePixels, 0, tileset.ImagePixels.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            return tileset;
        }

        private static uint[] ParseTmxTileData(System.Xml.Linq.XElement dataElement, string encoding, string compression, int expectedCount)
        {
            var gids = new uint[expectedCount];

            if (encoding == "csv")
            {
                string csvText = dataElement.Value.Trim();
                var parts = csvText.Split(',', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < Math.Min(parts.Length, expectedCount); i++)
                {
                    if (uint.TryParse(parts[i].Trim(), out uint gid))
                        gids[i] = gid;
                }
            }
            else if (encoding == "base64")
            {
                byte[] decoded = Convert.FromBase64String(dataElement.Value.Trim());

                if (!string.IsNullOrEmpty(compression))
                {
                    using var ms = new MemoryStream(decoded);
                    Stream decompressStream;

                    if (compression == "gzip")
                        decompressStream = new GZipStream(ms, CompressionMode.Decompress);
                    else if (compression == "zlib")
                        decompressStream = new ZLibStream(ms, CompressionMode.Decompress);
                    else
                    {
                        LoggingService.Warning("TMX unsupported compression: {Compression}", compression);
                        return gids;
                    }

                    using (decompressStream)
                    using (var br = new BinaryReader(decompressStream))
                    {
                        for (int i = 0; i < expectedCount; i++)
                        {
                            try { gids[i] = br.ReadUInt32(); }
                            catch { break; }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < Math.Min(decoded.Length / 4, expectedCount); i++)
                    {
                        gids[i] = BitConverter.ToUInt32(decoded, i * 4);
                    }
                }
            }
            else
            {
                int idx = 0;
                foreach (var tileEl in dataElement.Elements("tile"))
                {
                    if (idx >= expectedCount) break;
                    if (uint.TryParse(tileEl.Attribute("gid")?.Value ?? "0", out uint gid))
                        gids[idx] = gid;
                    idx++;
                }
            }

            return gids;
        }

        private static void RenderTmxLayer(uint[] gids, int layerWidth, int layerHeight, int tileWidth, int tileHeight,
            List<TmxTileset> tilesets, PixelSurface surface)
        {
            const uint FLIPPED_HORIZONTALLY = 0x80000000;
            const uint FLIPPED_VERTICALLY = 0x40000000;
            const uint FLIPPED_DIAGONALLY = 0x20000000;
            const uint GID_MASK = 0x1FFFFFFF;

            for (int y = 0; y < layerHeight; y++)
            {
                for (int x = 0; x < layerWidth; x++)
                {
                    int idx = y * layerWidth + x;
                    if (idx >= gids.Length) continue;

                    uint rawGid = gids[idx];
                    if (rawGid == 0) continue;

                    bool flipH = (rawGid & FLIPPED_HORIZONTALLY) != 0;
                    bool flipV = (rawGid & FLIPPED_VERTICALLY) != 0;
                    bool flipD = (rawGid & FLIPPED_DIAGONALLY) != 0;
                    uint gid = rawGid & GID_MASK;

                    TmxTileset? tileset = null;
                    foreach (var ts in tilesets.OrderByDescending(t => t.FirstGid))
                    {
                        if (gid >= ts.FirstGid)
                        {
                            tileset = ts;
                            break;
                        }
                    }

                    if (tileset?.ImagePixels == null) continue;

                    int localId = (int)(gid - tileset.FirstGid);
                    int srcTileX = (localId % tileset.Columns) * tileset.TileWidth;
                    int srcTileY = (localId / tileset.Columns) * tileset.TileHeight;

                    int dstX = x * tileWidth;
                    int dstY = y * tileHeight;

                    BlitTile(tileset.ImagePixels, tileset.ImageWidth, srcTileX, srcTileY,
                        tileset.TileWidth, tileset.TileHeight,
                        surface, dstX, dstY, tileWidth, tileHeight,
                        flipH, flipV, flipD);
                }
            }
        }

        private static void BlitTile(byte[] src, int srcImageW, int srcX, int srcY, int srcTileW, int srcTileH,
            PixelSurface dst, int dstX, int dstY, int dstTileW, int dstTileH,
            bool flipH, bool flipV, bool flipD)
        {
            int dstW = dst.Width;
            int dstH = dst.Height;

            for (int ty = 0; ty < Math.Min(srcTileH, dstTileH); ty++)
            {
                for (int tx = 0; tx < Math.Min(srcTileW, dstTileW); tx++)
                {
                    int readX = tx;
                    int readY = ty;

                    if (flipD) (readX, readY) = (readY, readX);
                    if (flipH) readX = srcTileW - 1 - readX;
                    if (flipV) readY = srcTileH - 1 - readY;

                    int sx = srcX + readX;
                    int sy = srcY + readY;
                    int dx = dstX + tx;
                    int dy = dstY + ty;

                    if (dx < 0 || dx >= dstW || dy < 0 || dy >= dstH) continue;
                    if (sx < 0 || sx >= srcImageW || sy < 0) continue;

                    int srcIdx = (sy * srcImageW + sx) * 4;
                    int dstIdx = (dy * dstW + dx) * 4;

                    if (srcIdx + 3 >= src.Length || dstIdx + 3 >= dst.Pixels.Length) continue;

                    byte a = src[srcIdx + 3];
                    if (a == 0) continue;

                    if (a == 255)
                    {
                        dst.Pixels[dstIdx + 0] = src[srcIdx + 0];
                        dst.Pixels[dstIdx + 1] = src[srcIdx + 1];
                        dst.Pixels[dstIdx + 2] = src[srcIdx + 2];
                        dst.Pixels[dstIdx + 3] = 255;
                    }
                    else
                    {
                        int invA = 255 - a;
                        dst.Pixels[dstIdx + 0] = (byte)((src[srcIdx + 0] * a + dst.Pixels[dstIdx + 0] * invA) / 255);
                        dst.Pixels[dstIdx + 1] = (byte)((src[srcIdx + 1] * a + dst.Pixels[dstIdx + 1] * invA) / 255);
                        dst.Pixels[dstIdx + 2] = (byte)((src[srcIdx + 2] * a + dst.Pixels[dstIdx + 2] * invA) / 255);
                        dst.Pixels[dstIdx + 3] = (byte)Math.Min(255, dst.Pixels[dstIdx + 3] + a);
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // T I L E D   T S X   (.tsx)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Imports a Tiled TSX tileset file as a CanvasDocument.
        /// </summary>
        /// <param name="filePath">Path to the .tsx file.</param>
        /// <returns>A <see cref="CanvasDocument"/> containing the tileset image.</returns>
        /// <exception cref="InvalidDataException">Thrown if the file is not a valid TSX document.</exception>
        /// <remarks>
        /// <para>
        /// TSX files define tilesets for Tiled tilemaps. This importer loads the tileset image
        /// and creates a document with the tile grid configuration matching the tileset definition.
        /// </para>
        /// </remarks>
        public static CanvasDocument ImportTsx(string filePath)
        {
            var doc = System.Xml.Linq.XDocument.Load(filePath);
            var tilesetElement = doc.Root ?? throw new InvalidDataException("TSX file has no root element.");

            if (tilesetElement.Name.LocalName != "tileset")
                throw new InvalidDataException("TSX file root element is not 'tileset'.");

            string name = tilesetElement.Attribute("name")?.Value ?? Path.GetFileNameWithoutExtension(filePath);
            int tileWidth = int.Parse(tilesetElement.Attribute("tilewidth")?.Value ?? "16");
            int tileHeight = int.Parse(tilesetElement.Attribute("tileheight")?.Value ?? "16");
            int tileCount = int.Parse(tilesetElement.Attribute("tilecount")?.Value ?? "0");
            int columns = int.Parse(tilesetElement.Attribute("columns")?.Value ?? "1");

            string baseDir = Path.GetDirectoryName(filePath) ?? ".";

            // Load image
            var imageElement = tilesetElement.Element("image");
            if (imageElement == null)
                throw new InvalidDataException("TSX tileset has no image element.");

            string imageSrc = imageElement.Attribute("source")?.Value ?? "";
            string imagePath = Path.IsPathRooted(imageSrc) ? imageSrc : Path.Combine(baseDir, imageSrc);

            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Tileset image not found: {imagePath}");

            LoggingService.Info("TSX import {FilePath}: tile={TW}x{TH}, count={Count}, columns={Cols}",
                filePath, tileWidth, tileHeight, tileCount, columns);

            // Load the image
            using var bmp = new Bitmap(imagePath);
            int width = bmp.Width;
            int height = bmp.Height;

            int tilesX = columns > 0 ? columns : Math.Max(1, width / tileWidth);
            int tilesY = tileCount > 0 && columns > 0 ? (tileCount + columns - 1) / columns : Math.Max(1, height / tileHeight);

            var canvasDoc = new CanvasDocument(
                name,
                width,
                height,
                CreateSize(tileWidth, tileHeight),
                CreateSize(tilesX, tilesY));

            // Remove default layer and add our tileset layer
            if (canvasDoc.Layers.Count == 1 && canvasDoc.Layers[0] is RasterLayer)
                canvasDoc.RemoveLayer(0);

            int layerIdx = canvasDoc.AddLayer("Tileset");
            if (canvasDoc.Layers[layerIdx] is not RasterLayer rl)
                throw new InvalidDataException("Failed to create RasterLayer for TSX import.");

            CopyBitmapToSurface(bmp, rl.Surface);
            rl.Visible = true;
            rl.Opacity = 255;
            rl.UpdatePreview();

            canvasDoc.CompositeTo(canvasDoc.Surface);
            LoggingService.Info("TSX import complete for {FilePath}", filePath);
            return canvasDoc;
        }

        // ──────────────────────────────────────────────────────────────────
        // W I N D O W S   I C O N   (.ico)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Imports the largest frame from a Windows icon (.ico) file as a single-layer document.
        /// </summary>
        /// <param name="filePath">Path to the .ico file.</param>
        /// <returns>A <see cref="CanvasDocument"/> containing the icon as a single raster layer.</returns>
        public static CanvasDocument ImportIconAsDocument(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Icon file not found.", filePath);

            GetLargestIconSize(filePath, out int targetW, out int targetH);

            if (targetW <= 0 || targetH <= 0)
                throw new InvalidDataException("Could not determine a valid icon size from .ico file.");

            using var ico = new Icon(filePath, targetW, targetH);
            using var srcBmp = ico.ToBitmap();

            int width = srcBmp.Width;
            int height = srcBmp.Height;

            int tileW = 16;
            int tileH = 16;
            int tilesX = Math.Max(1, width / tileW);
            int tilesY = Math.Max(1, height / tileH);

            string name = Path.GetFileNameWithoutExtension(filePath);

            var doc = new CanvasDocument(
                name,
                width,
                height,
                CreateSize(tileW, tileH),
                CreateSize(tilesX, tilesY));

            if (doc.Layers.Count == 1 && doc.Layers[0] is RasterLayer)
                doc.RemoveLayer(0);

            int layerIndex = doc.AddLayer("Icon");
            if (doc.Layers[layerIndex] is not RasterLayer rl)
                throw new InvalidDataException("Failed to create RasterLayer for icon import.");

            CopyBitmapToSurface(srcBmp, rl.Surface);
            rl.Visible = true;
            rl.Opacity = 255;
            rl.Blend = BlendMode.Normal;
            rl.UpdatePreview();

            doc.CompositeTo(doc.Surface);
            return doc;
        }

        private static void GetLargestIconSize(string filePath, out int maxWidth, out int maxHeight)
        {
            maxWidth = 0;
            maxHeight = 0;

            using var fs = File.OpenRead(filePath);
            using var br = new BinaryReader(fs);

            ushort reserved = br.ReadUInt16();
            ushort type = br.ReadUInt16();
            ushort count = br.ReadUInt16();

            if (reserved != 0 || type != 1 || count == 0)
                throw new InvalidDataException("Not a valid .ico file.");

            int bestArea = 0;

            for (int i = 0; i < count; i++)
            {
                byte widthByte = br.ReadByte();
                byte heightByte = br.ReadByte();
                br.ReadBytes(6); // colorCount, reserved, planes, bitCount
                br.ReadUInt32(); // bytesInRes
                br.ReadUInt32(); // imageOffset

                int w = widthByte == 0 ? 256 : widthByte;
                int h = heightByte == 0 ? 256 : heightByte;

                int area = w * h;
                if (area > bestArea)
                {
                    bestArea = area;
                    maxWidth = w;
                    maxHeight = h;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // W I N D O W S   C U R S O R   (.cur)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Imports the largest frame from a Windows cursor (.cur) file as a single-layer document.
        /// </summary>
        /// <param name="filePath">Path to the .cur file.</param>
        /// <returns>A <see cref="CanvasDocument"/> containing the cursor as a single raster layer.</returns>
        public static CanvasDocument ImportCursorAsDocument(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Cursor file not found.", filePath);

            using var srcBmp = CursorImportHelper.LoadCursorBitmap(filePath);

            int width = srcBmp.Width;
            int height = srcBmp.Height;

            int tileW = 16;
            int tileH = 16;
            int tilesX = Math.Max(1, (width + tileW - 1) / tileW);
            int tilesY = Math.Max(1, (height + tileH - 1) / tileH);

            string name = Path.GetFileNameWithoutExtension(filePath);

            var doc = new CanvasDocument(
                name,
                width,
                height,
                CreateSize(tileW, tileH),
                CreateSize(tilesX, tilesY));

            if (doc.Layers.Count == 1 && doc.Layers[0] is RasterLayer)
                doc.RemoveLayer(0);

            int layerIndex = doc.AddLayer("Cursor");
            if (doc.Layers[layerIndex] is not RasterLayer rl)
                throw new InvalidDataException("Failed to create RasterLayer for cursor import.");

            CopyBitmapToSurface(srcBmp, rl.Surface);
            rl.Visible = true;
            rl.Opacity = 255;
            rl.Blend = BlendMode.Normal;
            rl.UpdatePreview();

            doc.CompositeTo(doc.Surface);
            return doc;
        }

        /// <summary>
        /// Copies pixel data from a 32bpp ARGB bitmap to a PixelSurface.
        /// </summary>
        private static void CopyBitmapToSurface(Bitmap src, PixelSurface surface)
        {
            if (src.Width != surface.Width || src.Height != surface.Height)
                throw new InvalidDataException(
                    $"Bitmap size mismatch. Expected {surface.Width}x{surface.Height}, got {src.Width}x{src.Height}.");

            using var bmp =
                src.PixelFormat == PixelFormat.Format32bppArgb
                    ? (Bitmap)src.Clone()
                    : src.Clone(
                        new Rectangle(0, 0, src.Width, src.Height),
                        PixelFormat.Format32bppArgb);

            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            try
            {
                int stride = data.Stride;
                int w = bmp.Width;
                int h = bmp.Height;

                byte[] dst = surface.Pixels;

                unsafe
                {
                    byte* srcBase = (byte*)data.Scan0;
                    int dstStride = w * 4;

                    fixed (byte* dstBase = dst)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            byte* srcRow = srcBase + y * stride;
                            byte* dstRow = dstBase + y * dstStride;
                            Buffer.MemoryCopy(srcRow, dstRow, dstStride, dstStride);
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // P Y X E L   D T O s
        // ──────────────────────────────────────────────────────────────────

        private sealed class PyxelDocDto
        {
            public PyxelCanvasDto? Canvas { get; set; }
            public PyxelTilesetDto? Tileset { get; set; }
        }

        private sealed class PyxelCanvasDto
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public Dictionary<string, PyxelLayerDto> Layers { get; set; } = [];
        }

        private sealed class PyxelTilesetDto
        {
            public int TileWidth { get; set; }
            public int TileHeight { get; set; }
        }

        private sealed class PyxelLayerDto
        {
            public string Type { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool Hidden { get; set; }
            public bool Muted { get; set; }
            public bool Soloed { get; set; }
            public int Alpha { get; set; } = 255;
            public string BlendMode { get; set; } = "normal";
        }
    }
}
