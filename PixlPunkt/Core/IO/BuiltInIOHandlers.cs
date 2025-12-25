using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using PixlPunkt.Core.Serialization;

namespace PixlPunkt.Core.IO
{
    /// <summary>
    /// Built-in import/export handler registrations using the SDK fluent builders.
    /// Demonstrates dogfooding the plugin SDK from Core.
    /// </summary>
    public static class BuiltInIOHandlers
    {
        /// <summary>
        /// Registers all built-in import and export handlers with the registries.
        /// </summary>
        public static void RegisterAll()
        {
            // Palette import handlers
            ImportRegistry.Instance.Register(CreateHexPaletteImport());
            ImportRegistry.Instance.Register(CreateGplPaletteImport());
            ImportRegistry.Instance.Register(CreatePalPaletteImport());
            ImportRegistry.Instance.Register(CreateAcoPaletteImport());

            // Image import handlers
            ImportRegistry.Instance.Register(CreatePngImageImport());
            ImportRegistry.Instance.Register(CreateBmpImageImport());
            ImportRegistry.Instance.Register(CreateJpegImageImport());
            ImportRegistry.Instance.Register(CreateGifImageImport());

            // Palette export handlers
            ExportRegistry.Instance.Register(CreateHexPaletteExport());
            ExportRegistry.Instance.Register(CreateGplPaletteExport());
            ExportRegistry.Instance.Register(CreateJsonPaletteExport());
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PALETTE IMPORT HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates import handler for .hex palette files (one hex color per line).
        /// </summary>
        private static IImportRegistration CreateHexPaletteImport()
        {
            return ImportBuilders.ForPalette("pixlpunkt.import.palette.hex")
                .WithFormat(".hex", "Hex Palette", "One hex color per line (#RRGGBB or #AARRGGBB)")
                .WithPriority(100)
                .WithHandler(ctx =>
                {
                    try
                    {
                        var text = ctx.ReadAllText();
                        var colors = ParseHexColors(text);

                        if (colors.Count == 0)
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = "No valid hex colors found in file"
                            };
                        }

                        return new PaletteImportResult
                        {
                            Colors = colors,
                            PaletteName = Path.GetFileNameWithoutExtension(ctx.FileName)
                        };
                    }
                    catch (Exception ex)
                    {
                        return new PaletteImportResult
                        {
                            ErrorMessage = $"Import failed: {ex.Message}"
                        };
                    }
                })
                .Build();
        }

        /// <summary>
        /// Creates import handler for GIMP .gpl palette files.
        /// </summary>
        private static IImportRegistration CreateGplPaletteImport()
        {
            return ImportBuilders.ForPalette("pixlpunkt.import.palette.gpl")
                .WithFormat(".gpl", "GIMP Palette", "GIMP palette format (RGB triplets)")
                .WithPriority(100)
                .WithHandler(ctx =>
                {
                    try
                    {
                        var text = ctx.ReadAllText();
                        var lines = text.Split('\n', '\r');

                        // Validate GPL header
                        bool foundHeader = false;
                        string paletteName = Path.GetFileNameWithoutExtension(ctx.FileName);
                        var colors = new List<uint>();

                        foreach (var rawLine in lines)
                        {
                            var line = rawLine.Trim();

                            // Skip empty lines
                            if (string.IsNullOrEmpty(line))
                                continue;

                            // Check for header
                            if (line.StartsWith("GIMP Palette", StringComparison.OrdinalIgnoreCase))
                            {
                                foundHeader = true;
                                continue;
                            }

                            // Extract name if present
                            if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                            {
                                paletteName = line.Substring(5).Trim();
                                continue;
                            }

                            // Skip comments and metadata
                            if (line.StartsWith("#") || line.StartsWith("Columns:"))
                                continue;

                            // Parse color line: "R G B colorname" (colorname is optional)
                            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3 &&
                                byte.TryParse(parts[0], out byte r) &&
                                byte.TryParse(parts[1], out byte g) &&
                                byte.TryParse(parts[2], out byte b))
                            {
                                // Pack as BGRA with full alpha
                                uint color = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
                                colors.Add(color);
                            }
                        }

                        if (!foundHeader)
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = "Not a valid GIMP palette file (missing header)"
                            };
                        }

                        if (colors.Count == 0)
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = "No valid colors found in GIMP palette"
                            };
                        }

                        return new PaletteImportResult
                        {
                            Colors = colors,
                            PaletteName = paletteName
                        };
                    }
                    catch (Exception ex)
                    {
                        return new PaletteImportResult
                        {
                            ErrorMessage = $"Import failed: {ex.Message}"
                        };
                    }
                })
                .Build();
        }

        /// <summary>
        /// Creates import handler for Microsoft .pal palette files (RIFF format).
        /// </summary>
        private static IImportRegistration CreatePalPaletteImport()
        {
            return ImportBuilders.ForPalette("pixlpunkt.import.palette.pal")
                .WithFormat(".pal", "Microsoft Palette", "Microsoft RIFF palette format")
                .WithPriority(100)
                .WithMagicBytes([0x52, 0x49, 0x46, 0x46]) // "RIFF"
                .WithHandler(ctx =>
                {
                    try
                    {
                        var bytes = ctx.ReadAllBytes();

                        // RIFF PAL format:
                        // 0-3: "RIFF"
                        // 4-7: file size - 8
                        // 8-11: "PAL "
                        // 12-15: "data"
                        // 16-19: data chunk size
                        // 20-21: version (always 0x0300)
                        // 22-23: number of colors
                        // 24+: RGBX entries (4 bytes each: R, G, B, X)

                        if (bytes.Length < 24)
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = "File too small to be a valid PAL file"
                            };
                        }

                        // Check RIFF header
                        if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = "Not a valid RIFF file"
                            };
                        }

                        // Check PAL type
                        if (bytes[8] != 'P' || bytes[9] != 'A' || bytes[10] != 'L' || bytes[11] != ' ')
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = "Not a valid PAL file"
                            };
                        }

                        // Get color count
                        int colorCount = bytes[22] | (bytes[23] << 8);
                        if (colorCount == 0 || colorCount > 256)
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = $"Invalid color count: {colorCount}"
                            };
                        }

                        var colors = new List<uint>();
                        int offset = 24;

                        for (int i = 0; i < colorCount && offset + 4 <= bytes.Length; i++)
                        {
                            byte r = bytes[offset];
                            byte g = bytes[offset + 1];
                            byte b = bytes[offset + 2];
                            // bytes[offset + 3] is padding/flags, ignored

                            uint color = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
                            colors.Add(color);
                            offset += 4;
                        }

                        return new PaletteImportResult
                        {
                            Colors = colors,
                            PaletteName = Path.GetFileNameWithoutExtension(ctx.FileName)
                        };
                    }
                    catch (Exception ex)
                    {
                        return new PaletteImportResult
                        {
                            ErrorMessage = $"Import failed: {ex.Message}"
                        };
                    }
                })
                .Build();
        }

        /// <summary>
        /// Creates import handler for Adobe .aco swatch files.
        /// </summary>
        private static IImportRegistration CreateAcoPaletteImport()
        {
            return ImportBuilders.ForPalette("pixlpunkt.import.palette.aco")
                .WithFormat(".aco", "Adobe Color Swatches", "Adobe Photoshop color swatch file")
                .WithPriority(100)
                .WithHandler(ctx =>
                {
                    try
                    {
                        using var stream = ctx.OpenRead();
                        using var br = new BinaryReader(stream);

                        // ACO format: version (2 bytes), count (2 bytes), then entries
                        ushort version = ReadUInt16BE(br);
                        ushort count = ReadUInt16BE(br);

                        if (version != 1 && version != 2)
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = $"Unsupported ACO version: {version}"
                            };
                        }

                        var colors = new List<uint>();

                        for (int i = 0; i < count; i++)
                        {
                            ushort colorSpace = ReadUInt16BE(br);
                            ushort w = ReadUInt16BE(br);  // Channel 1
                            ushort x = ReadUInt16BE(br);  // Channel 2
                            ushort y = ReadUInt16BE(br);  // Channel 3
                            ushort z = ReadUInt16BE(br);  // Channel 4

                            uint color;
                            if (colorSpace == 0) // RGB
                            {
                                // Values are 0-65535, scale to 0-255
                                byte r = (byte)(w >> 8);
                                byte g = (byte)(x >> 8);
                                byte b = (byte)(y >> 8);
                                color = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
                            }
                            else if (colorSpace == 8) // Grayscale
                            {
                                byte gray = (byte)(w >> 8);
                                color = 0xFF000000u | ((uint)gray << 16) | ((uint)gray << 8) | gray;
                            }
                            else
                            {
                                // Skip unsupported color spaces (HSB, CMYK, Lab, etc.)
                                continue;
                            }

                            colors.Add(color);

                            // Version 2 has color names after each entry
                            if (version == 2)
                            {
                                // Skip 4 bytes + name length
                                br.ReadInt32(); // padding
                                int nameLen = ReadUInt16BE(br);
                                if (nameLen > 0)
                                {
                                    br.ReadBytes(nameLen * 2); // UTF-16 characters
                                }
                            }
                        }

                        if (colors.Count == 0)
                        {
                            return new PaletteImportResult
                            {
                                ErrorMessage = "No RGB colors found in ACO file"
                            };
                        }

                        return new PaletteImportResult
                        {
                            Colors = colors,
                            PaletteName = Path.GetFileNameWithoutExtension(ctx.FileName)
                        };
                    }
                    catch (Exception ex)
                    {
                        return new PaletteImportResult
                        {
                            ErrorMessage = $"Import failed: {ex.Message}"
                        };
                    }
                })
                .Build();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // IMAGE IMPORT HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates import handler for PNG image files.
        /// </summary>
        private static IImportRegistration CreatePngImageImport()
        {
            return ImportBuilders.ForImage("pixlpunkt.import.image.png")
                .WithFormat(".png", "PNG Image", "Portable Network Graphics image")
                .WithPriority(100)
                .WithMagicBytes([0x89, 0x50, 0x4E, 0x47]) // PNG magic
                .WithHandler(ctx => ImportImageWithGdi(ctx))
                .Build();
        }

        /// <summary>
        /// Creates import handler for BMP image files.
        /// </summary>
        private static IImportRegistration CreateBmpImageImport()
        {
            return ImportBuilders.ForImage("pixlpunkt.import.image.bmp")
                .WithFormat(".bmp", "BMP Image", "Windows Bitmap image")
                .WithPriority(100)
                .WithMagicBytes([0x42, 0x4D]) // "BM"
                .WithHandler(ctx => ImportImageWithGdi(ctx))
                .Build();
        }

        /// <summary>
        /// Creates import handler for JPEG image files.
        /// </summary>
        private static IImportRegistration CreateJpegImageImport()
        {
            return ImportBuilders.ForImage("pixlpunkt.import.image.jpg")
                .WithFormat(".jpg", "JPEG Image", "JPEG image (also accepts .jpeg)")
                .WithPriority(100)
                .WithMagicBytes([0xFF, 0xD8, 0xFF]) // JPEG magic
                .WithCanImport((ext, _) => ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                           ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                .WithHandler(ctx => ImportImageWithGdi(ctx))
                .Build();
        }

        /// <summary>
        /// Creates import handler for GIF image files.
        /// </summary>
        private static IImportRegistration CreateGifImageImport()
        {
            return ImportBuilders.ForImage("pixlpunkt.import.image.gif")
                .WithFormat(".gif", "GIF Image", "Graphics Interchange Format image")
                .WithPriority(100)
                .WithMagicBytes([0x47, 0x49, 0x46]) // "GIF"
                .WithHandler(ctx => ImportImageWithGdi(ctx))
                .Build();
        }

        /// <summary>
        /// Imports an image using System.Drawing (GDI+).
        /// </summary>
        private static ImageImportResult ImportImageWithGdi(IImportContext ctx)
        {
            try
            {
                var bytes = ctx.ReadAllBytes();
                using var ms = new MemoryStream(bytes);
                using var bmp = new System.Drawing.Bitmap(ms);

                int width = bmp.Width;
                int height = bmp.Height;

                // Convert to 32bpp ARGB and extract pixels
                var rect = new System.Drawing.Rectangle(0, 0, width, height);

                System.Drawing.Bitmap? converted = null;
                try
                {
                    if (bmp.PixelFormat != System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                    {
                        converted = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (var g = System.Drawing.Graphics.FromImage(converted))
                        {
                            g.DrawImage(bmp, 0, 0, width, height);
                        }
                    }

                    var source = converted ?? bmp;
                    var data = source.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    try
                    {
                        var pixels = new uint[width * height];
                        int stride = data.Stride;

                        unsafe
                        {
                            byte* srcBase = (byte*)data.Scan0;
                            for (int y = 0; y < height; y++)
                            {
                                byte* srcRow = srcBase + y * stride;
                                for (int x = 0; x < width; x++)
                                {
                                    int offset = x * 4;
                                    byte b = srcRow[offset];
                                    byte g = srcRow[offset + 1];
                                    byte r = srcRow[offset + 2];
                                    byte a = srcRow[offset + 3];

                                    // Pack as ARGB uint (matches PixlPunkt internal format)
                                    pixels[y * width + x] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
                                }
                            }
                        }

                        return new ImageImportResult
                        {
                            Pixels = pixels,
                            Width = width,
                            Height = height,
                            SuggestedName = Path.GetFileNameWithoutExtension(ctx.FileName)
                        };
                    }
                    finally
                    {
                        source.UnlockBits(data);
                    }
                }
                finally
                {
                    converted?.Dispose();
                }
            }
            catch (Exception ex)
            {
                return new ImageImportResult
                {
                    ErrorMessage = $"Import failed: {ex.Message}"
                };
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // PALETTE EXPORT HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates export handler for .hex palette files.
        /// </summary>
        private static IExportRegistration CreateHexPaletteExport()
        {
            return ExportBuilders.ForPalette("pixlpunkt.export.palette.hex")
                .WithFormat(".hex", "Hex Palette", "One hex color per line (#RRGGBB)")
                .WithPriority(100)
                .WithHandler((ctx, data) =>
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"# {data.Name}");
                        sb.AppendLine($"# {data.Colors.Count} colors");
                        sb.AppendLine();

                        foreach (var color in data.Colors)
                        {
                            byte r = (byte)(color >> 16);
                            byte g = (byte)(color >> 8);
                            byte b = (byte)color;
                            sb.AppendLine($"#{r:X2}{g:X2}{b:X2}");
                        }

                        ctx.WriteAllText(sb.ToString());
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .Build();
        }

        /// <summary>
        /// Creates export handler for GIMP .gpl palette files.
        /// </summary>
        private static IExportRegistration CreateGplPaletteExport()
        {
            return ExportBuilders.ForPalette("pixlpunkt.export.palette.gpl")
                .WithFormat(".gpl", "GIMP Palette", "GIMP palette format")
                .WithPriority(100)
                .WithHandler((ctx, data) =>
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("GIMP Palette");
                        sb.AppendLine($"Name: {data.Name}");
                        sb.AppendLine("Columns: 16");
                        sb.AppendLine("#");

                        int index = 0;
                        foreach (var color in data.Colors)
                        {
                            byte r = (byte)(color >> 16);
                            byte g = (byte)(color >> 8);
                            byte b = (byte)color;
                            sb.AppendLine($"{r,3} {g,3} {b,3}\tColor {index}");
                            index++;
                        }

                        ctx.WriteAllText(sb.ToString());
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .Build();
        }

        /// <summary>
        /// Creates export handler for PixlPunkt JSON palette files.
        /// </summary>
        private static IExportRegistration CreateJsonPaletteExport()
        {
            return ExportBuilders.ForPalette("pixlpunkt.export.palette.json")
                .WithFormat(".json", "PixlPunkt Palette", "PixlPunkt JSON palette format")
                .WithPriority(100)
                .WithHandler((ctx, data) =>
                {
                    try
                    {
                        // Use strongly-typed model for source-generated serialization
                        var palette = new PaletteExportJsonModel
                        {
                            Name = data.Name,
                            Description = $"Exported from PixlPunkt ({data.Colors.Count} colors)",
                            Colors = data.Colors
                        };

                        var json = JsonSerializer.Serialize(palette, PaletteExportJsonContext.Default.PaletteExportJsonModel);

                        ctx.WriteAllText(json);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                })
                .Build();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Parses hex colors from text (one color per line).
        /// </summary>
        private static List<uint> ParseHexColors(string text)
        {
            var colors = new List<uint>();
            var lines = text.Split('\n', '\r');

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") && !IsHexColor(line))
                    continue;

                if (TryParseHexColor(line, out uint color))
                {
                    colors.Add(color);
                }
            }

            return colors;
        }

        /// <summary>
        /// Checks if a line starting with # is a hex color (not a comment).
        /// </summary>
        private static bool IsHexColor(string line)
        {
            var hex = line.TrimStart('#');
            return hex.Length == 6 || hex.Length == 8;
        }

        /// <summary>
        /// Attempts to parse a hex color string.
        /// </summary>
        private static bool TryParseHexColor(string input, out uint color)
        {
            color = 0;
            var hex = input.TrimStart('#');

            if (hex.Length == 6)
            {
                // #RRGGBB -> AARRGGBB with alpha = 255
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                {
                    color = 0xFF000000u | rgb;
                    return true;
                }
            }
            else if (hex.Length == 8)
            {
                // #AARRGGBB
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads a big-endian 16-bit unsigned integer.
        /// </summary>
        private static ushort ReadUInt16BE(BinaryReader br)
        {
            byte hi = br.ReadByte();
            byte lo = br.ReadByte();
            return (ushort)((hi << 8) | lo);
        }
    }
}
