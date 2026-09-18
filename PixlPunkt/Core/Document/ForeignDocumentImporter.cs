using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Helpers;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.Core.Document
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
        // SHARED HELPERS
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a <see cref="CanvasDocument"/> with a single named raster layer,
        /// removing the default layer that <see cref="CanvasDocument"/> creates automatically.
        /// </summary>
        private static (CanvasDocument Doc, RasterLayer Layer) CreateSingleLayerDocument(
            string name,
            int width,
            int height,
            SizeInt32 tileSize,
            SizeInt32 tileCounts,
            string layerName)
        {
            var doc = new CanvasDocument(name, width, height, tileSize, tileCounts);

#pragma warning disable IDE0150 // Prefer 'null' check over type check
            if (doc.Layers.Count == 1 && doc.Layers[0] is RasterLayer)
                doc.RemoveLayer(0);
#pragma warning restore IDE0150

            int idx = doc.AddLayer(layerName);
            if (doc.Layers[idx] is not RasterLayer rl)
                throw new InvalidDataException($"Failed to create RasterLayer for {layerName} import.");

            return (doc, rl);
        }

        /// <summary>
        /// Applies standard finalization to an imported layer and composites the document.
        /// </summary>
        private static void FinalizeImportedLayer(CanvasDocument doc, RasterLayer layer, BlendMode blend = BlendMode.Normal)
        {
            layer.Visible = true;
            layer.Opacity = 255;
            layer.Blend = blend;
            layer.UpdatePreview();
            doc.CompositeTo(doc.Surface);
        }

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
                    ".ico" when OperatingSystem.IsWindows() => ImportIconAsDocument(filePath),
                    ".cur" when OperatingSystem.IsWindows() => ImportCursorAsDocument(filePath),
                    ".ico" or ".cur" => throw new PlatformNotSupportedException("Icon and cursor import are currently supported on Windows only."),
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
        /// <item>Tile reference transforms (rotation/flip) are currently ignored</item>
        /// <item>All raster layers must match canvas dimensions (no offset/partial layers)</item>
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
            // ── 2) Parse and sort layers by numeric key: "0", "1", "2", ... ─────────
            var layers = docJson.Canvas.Layers ?? [];

            var parsed = new List<(int Index, PyxelLayerDto Dto)>();
            foreach (var kvp in layers)
            {
                if (!int.TryParse(kvp.Key, out int index))
                {
                    LoggingService.Warning("Pyxel import: skipping non-numeric layer key '{LayerKey}'", kvp.Key);
                    continue;
                }

                parsed.Add((index, kvp.Value ?? new PyxelLayerDto()));
            }

            var ordered = parsed
                .OrderByDescending(t => t.Index)
                .ToList();

            // Import tiles before applying layer tile references.
            var pyxelTileToDocTileId = ImportPyxelTiles(zip, doc, tileW, tileH);

            // Capture CanvasDocument's default starter layer. It is dropped only after the
            // imported stack is in place, so a file that yields no layers keeps a usable document.
            var starterLayer = doc.Layers.Count == 1 ? doc.Layers[0] : null;

            var builtByIndex = new Dictionary<int, LayerBase>();

            // Create all layer/folder nodes first.
            foreach (var (index, dto) in ordered)
            {
                if (IsPyxelGroupLayer(dto.Type))
                {
                    var folder = new LayerFolder(string.IsNullOrWhiteSpace(dto.Name) ? $"Group {index}" : dto.Name)
                    {
                        Visible = !dto.Hidden,
                        Locked = false,
                        IsExpanded = !dto.Collapsed
                    };
                    builtByIndex[index] = folder;
                    continue;
                }

                var rl = new RasterLayer(pixelWidth, pixelHeight, string.IsNullOrWhiteSpace(dto.Name) ? $"Layer {index}" : dto.Name);

                string layerPngName = $"layer{index}.png";
                var pngEntry = zip.GetEntry(layerPngName);

                if (pngEntry != null)
                {
                    PixelSurface layerSurface = LoadSurfaceFromPng(pngEntry);

                    if (layerSurface.Width != pixelWidth || layerSurface.Height != pixelHeight)
                    {
                        LoggingService.Warning("Pyxel layer {LayerIndex} size {LW}x{LH} does not match canvas {CW}x{CH}", index, layerSurface.Width, layerSurface.Height, pixelWidth, pixelHeight);
                        throw new InvalidDataException($"Pyxel layer {index} bitmap size does not match canvas.");
                    }

                    Buffer.BlockCopy(
                        layerSurface.Pixels, 0,
                        rl.Surface.Pixels, 0,
                        rl.Surface.Pixels.Length);
                }

                // Map core properties
                rl.Visible = !dto.Hidden;
                rl.Locked = false;
                rl.Opacity = (byte)Math.Clamp(dto.Alpha, 0, 255);
                rl.Blend = MapPyxelBlend(dto.BlendMode);
                rl.UpdatePreview();

                builtByIndex[index] = rl;
            }

            // Rebuild hierarchy by parentIndex while preserving stack order.
            var rootItems = new List<LayerBase>();
            foreach (var (index, dto) in ordered)
            {
                if (!builtByIndex.TryGetValue(index, out var node))
                    continue;

                if (dto.ParentIndex >= 0 &&
                    builtByIndex.TryGetValue(dto.ParentIndex, out var parentNode) &&
                    parentNode is LayerFolder parentFolder)
                {
                    parentFolder.AddChild(node);
                }
                else
                {
                    rootItems.Add(node);
                }
            }

            foreach (var rootItem in rootItems)
            {
                doc.InsertLayerTreeWithoutHistory(rootItem, null, int.MaxValue);
            }

            // Drop the starter layer now the imported stack exists. RemoveLayer cannot be used here:
            // it bails out when the document holds a single raster, and it pushes an undo entry.
            if (starterLayer != null && rootItems.Count > 0)
                doc.RemoveLayerTreeWithoutHistory(starterLayer);

            // Apply tile references to imported raster layers.
            foreach (var (index, dto) in ordered)
            {
                if (!builtByIndex.TryGetValue(index, out var node) || node is not RasterLayer rl)
                    continue;

                if (dto.TileRefs.Count == 0)
                    continue;

                var mapping = rl.GetOrCreateTileMapping(tilesX, tilesY);

                foreach (var tileRefKvp in dto.TileRefs)
                {
                    if (!int.TryParse(tileRefKvp.Key, out int cellIndex))
                        continue;

                    var tileRef = tileRefKvp.Value;
                    if (tileRef == null)
                        continue;

                    int tileX = cellIndex % tilesX;
                    int tileY = cellIndex / tilesX;

                    if ((uint)tileX >= (uint)tilesX || (uint)tileY >= (uint)tilesY)
                        continue;

                    if (!pyxelTileToDocTileId.TryGetValue(tileRef.Index, out int mappedTileId))
                        continue;

                    mapping.SetTileId(tileX, tileY, mappedTileId);
                }
            }

            ImportPyxelTileAnimations(docJson, doc, pyxelTileToDocTileId);

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

        private static Dictionary<int, int> ImportPyxelTiles(ZipArchive zip, CanvasDocument doc, int tileW, int tileH)
        {
            var result = new Dictionary<int, int>();

            var tileEntries = zip.Entries
                .Select(e => (entry: e, index: TryParsePyxelTileIndex(e.Name)))
                .Where(t => t.index >= 0)
                .OrderBy(t => t.index)
                .ToList();

            if (tileEntries.Count == 0)
                return result;

            doc.TileSet.Clear();

            foreach (var (entry, pyxelTileIndex) in tileEntries)
            {
                var tileSurface = LoadSurfaceFromPng(entry);
                if (tileSurface.Width != tileW || tileSurface.Height != tileH)
                {
                    LoggingService.Warning(
                        "Pyxel tile {TileIndex} size {TW}x{TH} does not match expected tile size {EW}x{EH}; skipping",
                        pyxelTileIndex,
                        tileSurface.Width,
                        tileSurface.Height,
                        tileW,
                        tileH);
                    continue;
                }

                int tileId = doc.TileSet.AddTile(tileSurface.Pixels);
                result[pyxelTileIndex] = tileId;
            }

            return result;
        }

        /// <summary>
        /// Reads an integer attribute from a Tiled element. A missing attribute yields
        /// <paramref name="fallback"/>; a present but non-numeric one is a malformed file.
        /// </summary>
        private static int ReadIntAttribute(System.Xml.Linq.XElement element, string name, int fallback)
        {
            string? raw = element.Attribute(name)?.Value;
            if (raw == null)
                return fallback;

            if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value))
                throw new InvalidDataException($"Invalid Tiled file: attribute '{name}' on <{element.Name.LocalName}> is not an integer ('{raw}').");

            return value;
        }

        private static int TryParsePyxelTileIndex(string entryName)
        {
            if (!entryName.StartsWith("tile", StringComparison.OrdinalIgnoreCase) ||
                !entryName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            string numberPart = entryName.Substring(4, entryName.Length - 8);
            return int.TryParse(numberPart, out int idx) ? idx : -1;
        }

        private static void ImportPyxelTileAnimations(PyxelDocDto docJson, CanvasDocument doc, Dictionary<int, int> pyxelTileToDocTileId)
        {
            if (docJson.Animations.Count == 0)
                return;

            // A Pyxel animation is a run of consecutive *tileset* indices (baseTile, baseTile+1, ...).
            // PixlPunkt reels reference canvas cells instead, so find, for each imported tile, a
            // cell on the canvas that uses it. Tiles not placed anywhere cannot be animated here.
            var cellForDocTileId = new Dictionary<int, (int X, int Y)>();
            foreach (var layer in doc.Layers)
            {
                var mapping = layer.TileMapping;
                if (mapping == null) continue;

                for (int ty = 0; ty < mapping.Height; ty++)
                {
                    for (int tx = 0; tx < mapping.Width; tx++)
                    {
                        int id = mapping.GetTileId(tx, ty);
                        if (id >= 0 && !cellForDocTileId.ContainsKey(id))
                            cellForDocTileId[id] = (tx, ty);
                    }
                }
            }

            var orderedAnimations = docJson.Animations
                .Select(kvp =>
                {
                    int.TryParse(kvp.Key, out int index);
                    return (index, dto: kvp.Value ?? new PyxelAnimationDto());
                })
                .OrderBy(a => a.index)
                .ToList();

            foreach (var (_, anim) in orderedAnimations)
            {
                int length = Math.Max(0, anim.Length);
                if (length == 0)
                    continue;

                string reelName = string.IsNullOrWhiteSpace(anim.Name) ? "Pyxel Animation" : anim.Name;
                int defaultFrameMs = anim.FrameDuration > 0 ? anim.FrameDuration : 100;
                var frames = new List<ReelFrame>(length);

                for (int i = 0; i < length; i++)
                {
                    int pyxelTileIndex = anim.BaseTile + i;

                    if (!pyxelTileToDocTileId.TryGetValue(pyxelTileIndex, out int docTileId))
                    {
                        LoggingService.Warning("Pyxel animation '{Reel}' frame {Frame} references tile {Tile}, which the file does not define; skipping frame",
                            reelName, i, pyxelTileIndex);
                        continue;
                    }

                    if (!cellForDocTileId.TryGetValue(docTileId, out var cell))
                    {
                        LoggingService.Warning("Pyxel animation '{Reel}' frame {Frame} uses tile {Tile}, which is not placed on the canvas; skipping frame",
                            reelName, i, pyxelTileIndex);
                        continue;
                    }

                    int? durationMs = null;
                    if (i < anim.FrameDurationMultipliers.Count)
                    {
                        int multiplier = anim.FrameDurationMultipliers[i];
                        if (multiplier > 0 && multiplier != 100)
                            durationMs = Math.Max(1, (int)Math.Round(defaultFrameMs * (multiplier / 100.0)));
                    }

                    frames.Add(new ReelFrame(cell.X, cell.Y, durationMs));
                }

                if (frames.Count == 0)
                {
                    LoggingService.Warning("Pyxel animation '{Reel}' produced no usable frames and was not imported", reelName);
                    continue;
                }

                var reel = doc.TileAnimationState.AddReel(reelName);
                reel.DefaultFrameTimeMs = defaultFrameMs;
                foreach (var f in frames)
                    reel.Frames.Add(f);
            }
        }

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
                "darken" => BlendMode.Darken,
                "lighten" => BlendMode.Lighten,
                "difference" => BlendMode.Difference,
                "hardlight" or "hard_light" => BlendMode.HardLight,
                "invert" => BlendMode.Invert,
                "subtract" => BlendMode.Subtract,
                // Fallback to normal; we can refine as we learn more modes.
                _ => BlendMode.Normal
            };
        }

        private static bool IsPyxelGroupLayer(string? layerType)
            => string.Equals(layerType, "group_layer", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Loads a PixelSurface from a PNG stored within a ZIP archive entry.
        /// </summary>
        /// <param name="entry">ZIP entry containing PNG image data.</param>
        /// <returns>A <see cref="PixelSurface"/> with pixels in BGRA format.</returns>
        /// <remarks>
        /// Decodes PNG using SkiaSharp for cross-platform support and copies BGRA bytes into a new surface.
        /// </remarks>
        private static PixelSurface LoadSurfaceFromPng(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            var (pixels, w, h) = SkiaImageEncoder.DecodeFromStream(stream);
            var surf = new PixelSurface(w, h);
            Buffer.BlockCopy(pixels, 0, surf.Pixels, 0, Math.Min(pixels.Length, surf.Pixels.Length));
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

            // ── 2) Parse all frames and chunks ───────────────────────────
            var layers = new List<AseLayer>();
            var frames = new List<AseFrame>(Math.Max(1, (int)frameCount));
            uint[]? globalPalette = null;

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                long frameStart = fs.Position;
                uint frameBytes = br.ReadUInt32();
                ushort frameMagic = br.ReadUInt16();
                if (frameMagic != 0xF1FA)
                    throw new InvalidDataException("Invalid frame magic in Aseprite file.");

                ushort oldChunkCount = br.ReadUInt16();
                ushort frameDuration = br.ReadUInt16();
                br.ReadBytes(2); // reserved
                uint newChunkCount = br.ReadUInt32();

                uint chunkCount = newChunkCount == 0 ? oldChunkCount : newChunkCount;
                var frame = new AseFrame { DurationMs = frameDuration, Palette = globalPalette };
                bool frameHasNewPalette = false;

                for (uint c = 0; c < chunkCount; c++)
                {
                    long chunkStart = fs.Position;
                    uint chunkSize = br.ReadUInt32();
                    ushort chunkType = br.ReadUInt16();

                    switch (chunkType)
                    {
                        case 0x2004: // Layer chunk
                            if (frameIndex == 0)
                                layers.Add(ReadAseLayerChunk(br));
                            else
                                _ = ReadAseLayerChunk(br);
                            break;

                        case 0x2005: // Cel chunk
                            {
                                var cel = ReadAseCelChunk(br, colorDepth, width, height);
                                frame.CelsByLayer[(int)cel.LayerIndex] = cel;
                            }
                            break;

                        case 0x2019: // Palette chunk
                            frame.Palette = ReadAsePaletteChunk(br, frame.Palette);
                            frameHasNewPalette = true;
                            globalPalette = frame.Palette;
                            break;

                        case 0x0004: // Old palette chunk (deprecated, and carries no alpha).
                            // Aseprite emits both formats; prefer 0x2019 when this frame has one.
                            if (!frameHasNewPalette)
                            {
                                frame.Palette = ReadAseOldPaletteChunk(br, frame.Palette);
                                globalPalette = frame.Palette;
                            }
                            break;
                    }

                    // Skip to end of chunk
                    fs.Position = chunkStart + chunkSize;
                }

                // Land exactly on the next frame regardless of how the chunk loop left the stream
                // (unknown chunk types, a ZLibStream that read ahead, a short frame).
                fs.Position = frameStart + frameBytes;
                frames.Add(frame);
            }

            if (layers.Count == 0)
            {
                int maxLayerIndex = frames
                    .SelectMany(f => f.CelsByLayer.Keys)
                    .DefaultIfEmpty(-1)
                    .Max();

                for (int i = 0; i <= maxLayerIndex; i++)
                {
                    layers.Add(new AseLayer
                    {
                        Name = $"Layer {i}",
                        Flags = 1,
                        Type = 0,
                        Opacity = 255,
                        BlendMode = 0,
                        ChildLevel = 0
                    });
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

            // Capture the starter layer; dropped after the Aseprite tree is inserted (see below).
            var starterLayer = doc.Layers.Count == 1 ? doc.Layers[0] : null;

            // ── 4) Rebuild Aseprite layer tree ───────────────────────────
            var rootItems = new List<LayerBase>();
            var folderAtDepth = new List<LayerFolder?>();
            var rasterByAseIndex = new Dictionary<int, RasterLayer>();

            for (int layerIdx = 0; layerIdx < layers.Count; layerIdx++)
            {
                var aseLayer = layers[layerIdx];
                LayerBase node;

                if (aseLayer.Type == 1)
                {
                    node = new LayerFolder(string.IsNullOrWhiteSpace(aseLayer.Name) ? $"Group {layerIdx}" : aseLayer.Name)
                    {
                        Visible = aseLayer.Visible,
                        Locked = false
                    };
                }
                else if (aseLayer.Type == 0)
                {
                    var raster = new RasterLayer(width, height, string.IsNullOrWhiteSpace(aseLayer.Name) ? $"Layer {layerIdx}" : aseLayer.Name)
                    {
                        Visible = aseLayer.Visible,
                        Locked = false,
                        Opacity = aseLayer.Opacity,
                        Blend = MapAseBlend(aseLayer.BlendMode)
                    };

                    // Seed raster pixels from frame 0 for immediate document appearance.
                    if (frames.Count > 0)
                    {
                        byte[] frame0Pixels = BuildAseLayerPixelsForFrame(
                            frames,
                            0,
                            layerIdx,
                            colorDepth,
                            transparentIndex,
                            width,
                            height,
                            globalPalette);

                        Buffer.BlockCopy(frame0Pixels, 0, raster.Surface.Pixels, 0, Math.Min(frame0Pixels.Length, raster.Surface.Pixels.Length));
                    }

                    raster.UpdatePreview();
                    rasterByAseIndex[layerIdx] = raster;
                    node = raster;
                }
                else
                {
                    LoggingService.Warning("Aseprite layer type {LayerType} is not fully supported; skipping layer '{LayerName}'", aseLayer.Type, aseLayer.Name);
                    continue;
                }

                int childLevel = Math.Max(0, (int)aseLayer.ChildLevel);
                LayerFolder? parent = childLevel > 0 && childLevel - 1 < folderAtDepth.Count
                    ? folderAtDepth[childLevel - 1]
                    : null;

                if (parent != null)
                    parent.AddChild(node);
                else
                    rootItems.Add(node);

                if (node is LayerFolder folder)
                {
                    while (folderAtDepth.Count <= childLevel)
                        folderAtDepth.Add(null);

                    folderAtDepth[childLevel] = folder;
                    for (int d = childLevel + 1; d < folderAtDepth.Count; d++)
                        folderAtDepth[d] = null;
                }
            }

            foreach (var root in rootItems)
            {
                doc.InsertLayerTreeWithoutHistory(root, null, int.MaxValue);
            }

            // Drop the starter layer now the imported tree exists. RemoveLayer cannot be used here:
            // it bails out when the document holds a single raster, and it pushes an undo entry.
            if (starterLayer != null && rootItems.Count > 0)
                doc.RemoveLayerTreeWithoutHistory(starterLayer);

            // ── 5) Build canvas animation tracks from all frames ─────────
            if (frames.Count > 0)
            {
                var anim = doc.CanvasAnimationState;
                anim.SyncTracksFromDocument(doc);
                anim.FrameCount = Math.Max(1, (int)frameCount);

                int avgDuration = (int)Math.Max(1, Math.Round(frames.Average(f => Math.Max(1, f.DurationMs))));
                anim.FramesPerSecond = Math.Clamp((int)Math.Round(1000.0 / avgDuration), 1, 60);

                // Aseprite cels are sparse: most layers have no cel on most frames, and linked
                // cels repeat an earlier one. Storing a full-canvas buffer per layer per frame
                // would cost layers x frames x W x H x 4 bytes, nearly all of it blank or
                // duplicated. Emit a keyframe only where the resolved cel actually changes,
                // and let every "no cel" run share one blank buffer.
                int blankPixelDataId = -1;

                foreach (var layerPair in rasterByAseIndex)
                {
                    int layerIdx = layerPair.Key;
                    var raster = layerPair.Value;
                    AseCel? previousCel = null;
                    bool first = true;

                    for (int f = 0; f < frames.Count; f++)
                    {
                        var cel = ResolveAseCelForLayer(frames, f, layerIdx);
                        bool hasPixels = cel != null && cel.Pixels != null && cel.Width > 0 && cel.Height > 0;

                        // Same cel object as last frame (linked cel, or still no cel): nothing new to key.
                        if (!first && ReferenceEquals(cel, previousCel))
                            continue;
                        first = false;
                        previousCel = cel;

                        int pixelDataId;
                        if (hasPixels)
                        {
                            byte[] framePixels = BuildAseLayerPixelsForFrame(
                                frames,
                                f,
                                layerIdx,
                                colorDepth,
                                transparentIndex,
                                width,
                                height,
                                globalPalette);
                            pixelDataId = anim.StorePixelData(framePixels);
                        }
                        else
                        {
                            if (blankPixelDataId < 0)
                                blankPixelDataId = anim.StorePixelData(new byte[width * height * 4]);
                            pixelDataId = blankPixelDataId;
                        }

                        anim.SetKeyframe(raster, new LayerKeyframeData(
                            f,
                            raster.Visible,
                            raster.Opacity,
                            raster.Blend,
                            pixelDataId));
                    }
                }
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
            public ushort ChildLevel { get; set; }
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
            public byte Opacity { get; set; } = 255;
            public ushort CelType { get; set; }
            public ushort LinkedFrameIndex { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[]? Pixels { get; set; } // Raw pixel data (RGBA or indexed)
        }

        private sealed class AseFrame
        {
            public int DurationMs { get; set; } = 100;
            public Dictionary<int, AseCel> CelsByLayer { get; } = [];
            public uint[]? Palette { get; set; }
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
                ChildLevel = childLevel,
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

            var cel = new AseCel { LayerIndex = layerIndex, X = x, Y = y, Opacity = opacity, CelType = celType };

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
                    cel.LinkedFrameIndex = br.ReadUInt16();
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

        private static uint[] ReadAsePaletteChunk(BinaryReader br, uint[]? previous)
        {
            uint paletteSize = br.ReadUInt32();
            uint firstIndex = br.ReadUInt32();
            uint lastIndex = br.ReadUInt32();
            br.ReadBytes(8); // reserved

            // A palette chunk only describes entries firstIndex..lastIndex; every other
            // entry keeps whatever the palette in force already held.
            var palette = previous is null ? new uint[256] : (uint[])previous.Clone();

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

        private static uint[] ReadAseOldPaletteChunk(BinaryReader br, uint[]? previous)
        {
            // Packets skip over unchanged entries, so this is a partial update too.
            var palette = previous is null ? new uint[256] : (uint[])previous.Clone();
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
            switch (aseBlend)
            {
                case 0: return BlendMode.Normal;
                case 1: return BlendMode.Multiply;
                case 2: return BlendMode.Screen;
                case 3: return BlendMode.Overlay;
                case 4: return BlendMode.Darken;
                case 5: return BlendMode.Lighten;
                case 8: return BlendMode.HardLight;
                case 10: return BlendMode.Difference;
                case 16: return BlendMode.Add; // Addition
                case 17: return BlendMode.Subtract;

                // No exact equivalent; the nearest mode is used and the user is told.
                case 6: // Color Dodge
                    LoggingService.Warning("Aseprite blend mode Color Dodge has no equivalent; approximating with Add");
                    return BlendMode.Add;
                case 7: // Color Burn
                    LoggingService.Warning("Aseprite blend mode Color Burn has no equivalent; approximating with Multiply");
                    return BlendMode.Multiply;
                case 11: // Exclusion
                    LoggingService.Warning("Aseprite blend mode Exclusion has no equivalent; approximating with Difference");
                    return BlendMode.Difference;

                default: // 9 Soft Light, 12-15 Hue/Saturation/Color/Luminosity, 18 Divide
                    LoggingService.Warning("Aseprite blend mode {Mode} is not supported; falling back to Normal", aseBlend);
                    return BlendMode.Normal;
            }
        }

        private static byte[] BuildAseLayerPixelsForFrame(
            IReadOnlyList<AseFrame> frames,
            int frameIndex,
            int layerIndex,
            ushort colorDepth,
            byte transparentIndex,
            int canvasWidth,
            int canvasHeight,
            uint[]? fallbackPalette)
        {
            var dst = new PixelSurface(canvasWidth, canvasHeight);
            var cel = ResolveAseCelForLayer(frames, frameIndex, layerIndex);
            if (cel == null || cel.Pixels == null || cel.Width <= 0 || cel.Height <= 0)
                return dst.Pixels;

            var framePalette = frameIndex >= 0 && frameIndex < frames.Count ? frames[frameIndex].Palette : null;
            byte[] finalPixels;

            if (colorDepth == 8)
            {
                finalPixels = ConvertIndexedToBgra(cel.Pixels, framePalette ?? fallbackPalette ?? new uint[256], transparentIndex);
            }
            else if (colorDepth == 16)
            {
                finalPixels = ConvertGrayAlphaToBgra(cel.Pixels);
            }
            else
            {
                finalPixels = ConvertRgbaToBgra(cel.Pixels);
            }

            BlitPixels(finalPixels, cel.Width, cel.Height, cel.X, cel.Y, dst, cel.Opacity);
            return dst.Pixels;
        }

        private static AseCel? ResolveAseCelForLayer(IReadOnlyList<AseFrame> frames, int frameIndex, int layerIndex)
        {
            if ((uint)frameIndex >= (uint)frames.Count)
                return null;

            var visited = new HashSet<int>();
            int current = frameIndex;

            while (current >= 0 && current < frames.Count)
            {
                if (!visited.Add(current))
                    return null;

                if (!frames[current].CelsByLayer.TryGetValue(layerIndex, out var cel))
                    return null;

                if (cel.CelType == 1)
                {
                    current = cel.LinkedFrameIndex;
                    continue;
                }

                return cel;
            }

            return null;
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

        private static byte[] ConvertGrayAlphaToBgra(byte[] grayAlpha)
        {
            int pixelCount = grayAlpha.Length / 2;
            var bgra = new byte[pixelCount * 4];

            int src = 0;
            int dst = 0;
            for (int i = 0; i < pixelCount; i++)
            {
                byte g = grayAlpha[src++];
                byte a = grayAlpha[src++];

                bgra[dst++] = g;
                bgra[dst++] = g;
                bgra[dst++] = g;
                bgra[dst++] = a;
            }

            return bgra;
        }

        private static void BlitPixels(byte[] src, int srcW, int srcH, int dstX, int dstY, PixelSurface dst, byte sourceOpacity = 255)
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

                    byte srcB = src[srcIdx + 0];
                    byte srcG = src[srcIdx + 1];
                    byte srcR = src[srcIdx + 2];
                    byte srcA = src[srcIdx + 3];

                    int effectiveA = (srcA * sourceOpacity + 127) / 255;
                    if (effectiveA <= 0)
                        continue;

                    byte dstB = dst.Pixels[dstIdx + 0];
                    byte dstG = dst.Pixels[dstIdx + 1];
                    byte dstR = dst.Pixels[dstIdx + 2];
                    byte dstA = dst.Pixels[dstIdx + 3];

                    int outA = effectiveA + ((dstA * (255 - effectiveA) + 127) / 255);
                    if (outA <= 0)
                    {
                        dst.Pixels[dstIdx + 0] = 0;
                        dst.Pixels[dstIdx + 1] = 0;
                        dst.Pixels[dstIdx + 2] = 0;
                        dst.Pixels[dstIdx + 3] = 0;
                        continue;
                    }

                    int srcFactor = effectiveA * 255;
                    int dstFactor = dstA * (255 - effectiveA);
                    int denom = outA * 255;

                    dst.Pixels[dstIdx + 0] = (byte)Math.Clamp((srcB * srcFactor + dstB * dstFactor) / Math.Max(1, denom), 0, 255);
                    dst.Pixels[dstIdx + 1] = (byte)Math.Clamp((srcG * srcFactor + dstG * dstFactor) / Math.Max(1, denom), 0, 255);
                    dst.Pixels[dstIdx + 2] = (byte)Math.Clamp((srcR * srcFactor + dstR * dstFactor) / Math.Max(1, denom), 0, 255);
                    dst.Pixels[dstIdx + 3] = (byte)Math.Clamp(outA, 0, 255);
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
            int mapWidth = ReadIntAttribute(mapElement, "width", 0);
            int mapHeight = ReadIntAttribute(mapElement, "height", 0);
            int tileWidth = ReadIntAttribute(mapElement, "tilewidth", 16);
            int tileHeight = ReadIntAttribute(mapElement, "tileheight", 16);

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
                int layerWidth = ReadIntAttribute(layerElement, "width", mapWidth);
                int layerHeight = ReadIntAttribute(layerElement, "height", mapHeight);
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
            int firstGid = ReadIntAttribute(tsElement, "firstgid", 1);

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

            int tileWidth = ReadIntAttribute(tsElement, "tilewidth", defaultTileW);
            int tileHeight = ReadIntAttribute(tsElement, "tileheight", defaultTileH);
            int tileCount = ReadIntAttribute(tsElement, "tilecount", 0);
            int columns = ReadIntAttribute(tsElement, "columns", 1);

            var imageElement = tsElement.Element("image");
            if (imageElement == null) return null;

            string imageSrc = imageElement.Attribute("source")?.Value ?? "";
            string imagePath = Path.IsPathRooted(imageSrc) ? imageSrc : Path.Combine(baseDir, imageSrc);

            if (!File.Exists(imagePath))
            {
                LoggingService.Warning("TMX tileset image not found: {Path}", imagePath);
                return null;
            }

            var (imagePixels, imageWidth, imageHeight) = SkiaImageEncoder.Decode(imagePath);
            var tileset = new TmxTileset
            {
                FirstGid = firstGid,
                TileWidth = tileWidth,
                TileHeight = tileHeight,
                TileCount = tileCount,
                Columns = columns > 0 ? columns : Math.Max(1, imageWidth / tileWidth),
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                ImagePixels = imagePixels
            };

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
            int tileWidth = ReadIntAttribute(tilesetElement, "tilewidth", 16);
            int tileHeight = ReadIntAttribute(tilesetElement, "tileheight", 16);
            int tileCount = ReadIntAttribute(tilesetElement, "tilecount", 0);
            int columns = ReadIntAttribute(tilesetElement, "columns", 1);

            string baseDir = Path.GetDirectoryName(filePath) ?? ".";

            // Load image
            var imageElement = tilesetElement.Element("image") ?? throw new InvalidDataException("TSX tileset has no image element.");
            string imageSrc = imageElement.Attribute("source")?.Value ?? "";
            string imagePath = Path.IsPathRooted(imageSrc) ? imageSrc : Path.Combine(baseDir, imageSrc);

            if (!File.Exists(imagePath))
                throw new FileNotFoundException($"Tileset image not found: {imagePath}");

            LoggingService.Info("TSX import {FilePath}: tile={TW}x{TH}, count={Count}, columns={Cols}",
                filePath, tileWidth, tileHeight, tileCount, columns);

            // Load the image
            var (imagePixels, width, height) = SkiaImageEncoder.Decode(imagePath);

            int tilesX = columns > 0 ? columns : Math.Max(1, width / tileWidth);
            int tilesY = tileCount > 0 && columns > 0 ? (tileCount + columns - 1) / columns : Math.Max(1, height / tileHeight);

            var (canvasDoc, rl) = CreateSingleLayerDocument(
                name, width, height,
                CreateSize(tileWidth, tileHeight),
                CreateSize(tilesX, tilesY),
                "Tileset");

            CopyBgraToSurface(imagePixels, width, height, rl.Surface);
            FinalizeImportedLayer(canvasDoc, rl);
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
        [SupportedOSPlatform("windows")]
        public static CanvasDocument ImportIconAsDocument(string filePath)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Icon import is only supported on Windows.");

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

            var (doc, rl) = CreateSingleLayerDocument(
                name, width, height,
                CreateSize(tileW, tileH),
                CreateSize(tilesX, tilesY),
                "Icon");

            CopyBitmapToSurface(srcBmp, rl.Surface);
            FinalizeImportedLayer(doc, rl);
            return doc;
        }

        [SupportedOSPlatform("windows")]
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
        [SupportedOSPlatform("windows")]
        public static CanvasDocument ImportCursorAsDocument(string filePath)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Cursor import is only supported on Windows.");

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

            var (doc, rl) = CreateSingleLayerDocument(
                name, width, height,
                CreateSize(tileW, tileH),
                CreateSize(tilesX, tilesY),
                "Cursor");

            CopyBitmapToSurface(srcBmp, rl.Surface);
            FinalizeImportedLayer(doc, rl);
            return doc;
        }

        /// <summary>
        /// Copies BGRA pixel bytes into a target <see cref="PixelSurface"/> of matching dimensions.
        /// </summary>
        private static void CopyBgraToSurface(byte[] sourcePixels, int width, int height, PixelSurface surface)
        {
            if (width != surface.Width || height != surface.Height)
                throw new InvalidDataException(
                    $"Bitmap size mismatch. Expected {surface.Width}x{surface.Height}, got {width}x{height}.");

            Buffer.BlockCopy(sourcePixels, 0, surface.Pixels, 0, Math.Min(sourcePixels.Length, surface.Pixels.Length));
        }

        /// <summary>
        /// Copies pixel data from a 32bpp ARGB bitmap to a PixelSurface.
        /// </summary>
        [SupportedOSPlatform("windows")]
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
            public Dictionary<string, PyxelAnimationDto> Animations { get; set; } = [];
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
            public int ParentIndex { get; set; } = -1;
            public bool Collapsed { get; set; }
            public bool Hidden { get; set; }
            public bool Muted { get; set; }
            public bool Soloed { get; set; }
            public int Alpha { get; set; } = 255;
            public string BlendMode { get; set; } = "normal";
            public Dictionary<string, PyxelTileRefDto> TileRefs { get; set; } = [];
        }

        private sealed class PyxelTileRefDto
        {
            public int Index { get; set; }
            public int Rot { get; set; }
            public bool FlipX { get; set; }
            public bool FlipY { get; set; }
        }

        private sealed class PyxelAnimationDto
        {
            public string Name { get; set; } = string.Empty;
            public int Length { get; set; }
            public int FrameDuration { get; set; } = 100;
            public List<int> FrameDurationMultipliers { get; set; } = [];
            public int BaseTile { get; set; }
        }
    }
}
