using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using PixlPunkt.Core.Serialization;
using PixlPunkt.Core.Settings;

namespace PixlPunkt.Core.Ascii
{
    /// <summary>
    /// Global registry for ASCII glyph sets (built-in + custom).
    /// </summary>
    public static class AsciiGlyphSets
    {
        private static readonly Dictionary<string, AsciiGlyphSet> _byName =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _builtInNames =
            new(StringComparer.OrdinalIgnoreCase);

        static AsciiGlyphSets()
        {
            // ─────────────────────────────────────────────────────────────
            // Built-in ramps with 4x4 glyph bitmaps for pattern matching
            // Each ulong is a 4x4 bitmap where bit 0 = top-left, bit 15 = bottom-right
            // Row-major order: bits 0-3 = row 0, bits 4-7 = row 1, etc.
            // ─────────────────────────────────────────────────────────────

            // A) Basic – clean density ramp with 4x4 bitmaps
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Basic (4x4)",
                Ramp = " .:-=+*#%@",
                GlyphWidth = 4,
                GlyphHeight = 4,
                GlyphBitmaps = new ulong[]
                {
                    0b0000_0000_0000_0000, // ' ' - empty
                    0b0000_0000_0100_0000, // '.' - single dot center-bottom
                    0b0000_0000_0110_0000, // ':' - two dots
                    0b0000_0110_0110_0000, // '-' - horizontal line middle
                    0b0000_1111_0000_0000, // '=' - double horizontal
                    0b0100_1110_0100_0000, // '+' - cross
                    0b0101_1111_0101_0000, // '*' - star pattern
                    0b1010_0101_1010_0101, // '#' - checkerboard
                    0b1111_1010_0101_1111, // '%' - dense pattern
                    0b1111_1111_1111_1111, // '@' - full block
                }
            });

            // B) Blocks – chunky terminal shading with 4x4 bitmaps
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Blocks (4x4)",
                Ramp = " ░▒▓█",
                GlyphWidth = 4,
                GlyphHeight = 4,
                GlyphBitmaps = new ulong[]
                {
                    0b0000_0000_0000_0000, // ' ' - empty
                    0b1000_0001_1000_0001, // '░' - light shade (sparse dots)
                    0b1010_0101_1010_0101, // '▒' - medium shade (checkerboard)
                    0b1110_1101_1011_0111, // '▓' - dark shade (mostly filled)
                    0b1111_1111_1111_1111, // '█' - full block
                }
            });

            // C) SharpSymbols – noisy/techy with 4x4 bitmaps
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "SharpSymbols (4x4)",
                Ramp = " .'`^*+x%#@",
                GlyphWidth = 4,
                GlyphHeight = 4,
                GlyphBitmaps = new ulong[]
                {
                    0b0000_0000_0000_0000, // ' ' - empty
                    0b0000_0000_0100_0000, // '.' - dot
                    0b0000_0100_0000_0000, // ''' - apostrophe top
                    0b0100_0000_0000_0000, // '`' - backtick
                    0b0100_1010_0000_0000, // '^' - caret
                    0b0100_1110_0100_0000, // '*' - asterisk
                    0b0100_1110_0100_0000, // '+' - plus
                    0b1001_0110_0110_1001, // 'x' - X shape
                    0b1010_0101_1010_0101, // '%' - checkerboard
                    0b0110_1111_1111_0110, // '#' - hash
                    0b1111_1111_1111_1111, // '@' - full
                }
            });

            // D) DFRunesLight – punctuation-y "DF-ish" with 4x4 bitmaps
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "DFRunesLight (4x4)",
                Ramp = " .,:;|/+=*#%@",
                GlyphWidth = 4,
                GlyphHeight = 4,
                GlyphBitmaps = new ulong[]
                {
                    0b0000_0000_0000_0000, // ' ' - empty
                    0b0000_0000_0100_0000, // '.' - dot
                    0b0000_0100_0000_0100, // ',' - comma
                    0b0000_0100_0100_0000, // ':' - colon
                    0b0100_0100_0100_0100, // ';' - semicolon / vertical dots
                    0b0100_0100_0100_0100, // '|' - vertical bar
                    0b0001_0010_0100_1000, // '/' - diagonal
                    0b0100_1110_0100_0000, // '+' - plus
                    0b0000_1111_1111_0000, // '=' - equals
                    0b0110_1111_1111_0110, // '*' - star
                    0b0110_1111_1111_0110, // '#' - hash
                    0b1010_0101_1010_0101, // '%' - percent/checker
                    0b1111_1111_1111_1111, // '@' - full
                }
            });

            // E) Boxes – structural/wall-ish with 4x4 bitmaps
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Boxes (4x4)",
                Ramp = " -=║╬█",
                GlyphWidth = 4,
                GlyphHeight = 4,
                GlyphBitmaps = new ulong[]
                {
                    0b0000_0000_0000_0000, // ' ' - empty
                    0b0000_1111_0000_0000, // '-' - horizontal line
                    0b0000_1111_1111_0000, // '=' - double horizontal
                    0b0110_0110_0110_0110, // '║' - vertical double
                    0b0110_1111_1111_0110, // '╬' - cross
                    0b1111_1111_1111_1111, // '█' - full block
                }
            });

            // F) Debug – numbers, for bucket-debugging (no meaningful bitmaps)
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "DebugNumbers (4x4)",
                Ramp = "0123456789",
                GlyphWidth = 4,
                GlyphHeight = 4,
                GlyphBitmaps = new ulong[]
                {
                    0b0110_1001_1001_0110, // '0'
                    0b0100_1100_0100_1110, // '1'
                    0b0110_0001_0110_1111, // '2'
                    0b1110_0110_0001_1110, // '3'
                    0b1001_1111_0001_0001, // '4'
                    0b1111_1110_0001_1110, // '5'
                    0b0111_1000_1111_0111, // '6'
                    0b1111_0001_0010_0100, // '7'
                    0b0110_0110_1001_0110, // '8'
                    0b0110_1001_0111_0001, // '9'
                }
            });

            // G) Gradient – smooth density gradient with 4x4 bitmaps (16 levels)
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Gradient16 (4x4)",
                Ramp = " .,:;!|[{#%&$@MW",
                GlyphWidth = 4,
                GlyphHeight = 4,
                GlyphBitmaps = new ulong[]
                {
                    0b0000_0000_0000_0000, // 0/16 - empty
                    0b0000_0000_0000_0001, // 1/16
                    0b0001_0000_0000_0100, // 2/16
                    0b0001_0100_0001_0100, // 3/16
                    0b0101_0000_0101_0000, // 4/16
                    0b0101_0001_0100_0101, // 5/16
                    0b0101_0101_0001_0101, // 6/16
                    0b0101_0101_0101_0101, // 7/16
                    0b1010_0101_1010_0101, // 8/16 - checkerboard
                    0b1010_1101_0101_1010, // 9/16
                    0b1010_1110_1011_1010, // 10/16
                    0b1110_1010_1110_1011, // 11/16
                    0b1010_1111_1011_1110, // 12/16
                    0b1110_1111_1011_1111, // 13/16
                    0b1110_1111_1111_1110, // 14/16
                    0b1111_1111_1111_1111, // 15/16 - full
                }
            });

            // ─────────────────────────────────────────────────────────────
            // 8x8 GLYPH SETS - Higher resolution versions
            // Each ulong is an 8x8 bitmap (64 bits)
            // Row-major order: bits 0-7 = row 0, bits 8-15 = row 1, etc.
            // ─────────────────────────────────────────────────────────────

            // 8x8 Basic
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Basic (8x8)",
                Ramp = " .:-=+*#%@",
                GlyphWidth = 8,
                GlyphHeight = 8,
                GlyphBitmaps = new ulong[]
                {
                    0x0000000000000000, // ' ' - empty
                    0x0000000018000000, // '.' - single dot
                    0x0000001818000000, // ':' - two dots vertical
                    0x0000007E00000000, // '-' - horizontal line
                    0x00007E007E000000, // '=' - double horizontal
                    0x0018187E7E181800, // '+' - cross
                    0x18245A7E7E5A2418, // '*' - star pattern
                    0xAA55AA55AA55AA55, // '#' - checkerboard
                    0xFF55AA55AA55AAFF, // '%' - dense pattern
                    0xFFFFFFFFFFFFFFFF, // '@' - full block
                }
            });

            // 8x8 Blocks
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Blocks (8x8)",
                Ramp = " ░▒▓█",
                GlyphWidth = 8,
                GlyphHeight = 8,
                GlyphBitmaps = new ulong[]
                {
                    0x0000000000000000, // ' ' - empty
                    0x8800220088002200, // '░' - light shade (sparse)
                    0xAA55AA55AA55AA55, // '▒' - medium shade (checkerboard)
                    0xDDBB77EEDDBBEEFF, // '▓' - dark shade
                    0xFFFFFFFFFFFFFFFF, // '█' - full block
                }
            });

            // 8x8 SharpSymbols
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "SharpSymbols (8x8)",
                Ramp = " .'`^*+x%#@",
                GlyphWidth = 8,
                GlyphHeight = 8,
                GlyphBitmaps = new ulong[]
                {
                    0x0000000000000000, // ' ' - empty
                    0x0000000018000000, // '.' - dot
                    0x0018180000000000, // ''' - apostrophe
                    0x1818000000000000, // '`' - backtick
                    0x183C420000000000, // '^' - caret
                    0x18187E7E181800, // '*' - asterisk
                    0x0018187E7E181800, // '+' - plus
                    0x8142241818244281, // 'x' - X shape
                    0xAA55AA55AA55AA55, // '%' - checkerboard
                    0x3C7E7E7E7E7E3C00, // '#' - hash/rounded square
                    0xFFFFFFFFFFFFFFFF, // '@' - full
                }
            });

            // 8x8 DFRunesLight
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "DFRunesLight (8x8)",
                Ramp = " .,:;|/+=*#%@",
                GlyphWidth = 8,
                GlyphHeight = 8,
                GlyphBitmaps = new ulong[]
                {
                    0x0000000000000000, // ' ' - empty
                    0x0000000018000000, // '.' - dot
                    0x0000001800180000, // ',' - comma
                    0x0000181800181800, // ':' - colon
                    0x1818001818001818, // ';' - semicolon pattern
                    0x1818181818181818, // '|' - vertical bar
                    0x0102040810204080, // '/' - diagonal
                    0x0018187E7E181800, // '+' - plus
                    0x00007E007E000000, // '=' - equals
                    0x183C7E7E7E3C1800, // '*' - star
                    0x3C7EFFFFFFFF7E3C, // '#' - filled
                    0xAA55AA55AA55AA55, // '%' - checker
                    0xFFFFFFFFFFFFFFFF, // '@' - full
                }
            });

            // 8x8 Boxes
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Boxes (8x8)",
                Ramp = " -=║╬█",
                GlyphWidth = 8,
                GlyphHeight = 8,
                GlyphBitmaps = new ulong[]
                {
                    0x0000000000000000, // ' ' - empty
                    0x000000FF00000000, // '-' - horizontal line
                    0x0000FF00FF000000, // '=' - double horizontal
                    0x3636363636363636, // '║' - vertical double
                    0x3636FF00FF363636, // '╬' - cross
                    0xFFFFFFFFFFFFFFFF, // '█' - full block
                }
            });

            // 8x8 DebugNumbers
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "DebugNumbers (8x8)",
                Ramp = "0123456789",
                GlyphWidth = 8,
                GlyphHeight = 8,
                GlyphBitmaps = new ulong[]
                {
                    0x3C66666E76663C00, // '0'
                    0x1838181818187E00, // '1'
                    0x3C66060C18307E00, // '2'
                    0x3C66061C06663C00, // '3'
                    0x0C1C3C6C7E0C0C00, // '4'
                    0x7E60607C06663C00, // '5'
                    0x1C30607C66663C00, // '6'
                    0x7E060C1818181800, // '7'
                    0x3C66663C66663C00, // '8'
                    0x3C66663E060C3800, // '9'
                }
            });

            // 8x8 Gradient (16 levels)
            RegisterBuiltIn(new AsciiGlyphSet
            {
                Name = "Gradient16 (8x8)",
                Ramp = " .,:;!|[{#%&$@MW",
                GlyphWidth = 8,
                GlyphHeight = 8,
                GlyphBitmaps = new ulong[]
                {
                    0x0000000000000000, // 0/16 - empty
                    0x0000000000000080, // 1/16
                    0x0000002000000400, // 2/16
                    0x0020000400200004, // 3/16
                    0x0022000400220004, // 4/16
                    0x2200440022004400, // 5/16
                    0x2200440A22004408, // 6/16
                    0x220044AA220044AA, // 7/16
                    0xAA55AA55AA55AA55, // 8/16 - checkerboard
                    0xAA55AA57AA55AA75, // 9/16
                    0xAA55AB55AA55BA55, // 10/16
                    0xAA55BB55AA55BB55, // 11/16
                    0xAADDBB55AADDBF55, // 12/16
                    0xBBDDBB77BBDDBF77, // 13/16
                    0xFFDDBB77FFDDBF77, // 14/16
                    0xFFFFFFFFFFFFFFFF, // 15/16 - full
                }
            });
        }

        /// <summary>
        /// Gets all registered glyph sets, sorted by name.
        /// </summary>
        public static IReadOnlyList<AsciiGlyphSet> All =>
            _byName.Values.OrderBy(s => s.Name).ToList();

        /// <summary>
        /// Gets the default glyph set.
        /// </summary>
        public static AsciiGlyphSet Default => Get("Basic");

        /// <summary>
        /// Registers a built-in glyph set (cannot be removed).
        /// </summary>
        private static void RegisterBuiltIn(AsciiGlyphSet set)
        {
            ArgumentNullException.ThrowIfNull(set);
            if (string.IsNullOrWhiteSpace(set.Name))
                throw new ArgumentException("AsciiGlyphSet.Name must not be empty.", nameof(set));

            _byName[set.Name] = set;
            _builtInNames.Add(set.Name);
        }

        /// <summary>
        /// Registers a custom glyph set (can be removed).
        /// </summary>
        /// <param name="set">The glyph set to register.</param>
        public static void Register(AsciiGlyphSet set)
        {
            ArgumentNullException.ThrowIfNull(set);
            if (string.IsNullOrWhiteSpace(set.Name))
                throw new ArgumentException("AsciiGlyphSet.Name must not be empty.", nameof(set));

            _byName[set.Name] = set;
        }

        /// <summary>
        /// Removes a custom glyph set. Built-in sets cannot be removed.
        /// </summary>
        /// <param name="name">The name of the glyph set to remove.</param>
        /// <returns>True if the set was removed, false if it was built-in or not found.</returns>
        public static bool Remove(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                System.Diagnostics.Debug.WriteLine($"[AsciiGlyphSets] Remove failed: name is null or empty");
                return false;
            }
            
            if (_builtInNames.Contains(name))
            {
                System.Diagnostics.Debug.WriteLine($"[AsciiGlyphSets] Remove failed: '{name}' is a built-in set");
                return false;
            }

            bool removed = _byName.Remove(name);
            System.Diagnostics.Debug.WriteLine($"[AsciiGlyphSets] Remove '{name}': {(removed ? "SUCCESS" : "NOT FOUND")}. Remaining sets: {string.Join(", ", _byName.Keys)}");
            return removed;
        }

        /// <summary>
        /// Checks if a glyph set is built-in.
        /// </summary>
        /// <param name="name">The name of the glyph set.</param>
        /// <returns>True if the set is built-in, false otherwise.</returns>
        public static bool IsBuiltIn(string name)
        {
            return !string.IsNullOrEmpty(name) && _builtInNames.Contains(name);
        }

        /// <summary>
        /// Gets a glyph set by name, or the default if not found.
        /// </summary>
        /// <param name="name">The name of the glyph set.</param>
        /// <returns>The glyph set, or the default set if not found.</returns>
        public static AsciiGlyphSet Get(string? name)
        {
            if (!string.IsNullOrEmpty(name) && _byName.TryGetValue(name, out var s))
                return s;

            // Fallback: first registered
            if (_byName.Count == 0)
                throw new InvalidOperationException("No ASCII glyph sets registered.");

            return _byName.Values.First();
        }

        /// <summary>
        /// Loads custom glyph sets from the default GlyphSets folder.
        /// Call this once at startup.
        /// </summary>
        public static void LoadCustomSets()
        {
            LoadFromFolder(AppPaths.GlyphSetsDirectory);
        }

        /// <summary>
        /// Load custom sets from a folder with *.asciifont.json files.
        /// </summary>
        /// <param name="folderPath">The folder path to load from.</param>
        public static void LoadFromFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            if (!Directory.Exists(folderPath)) return;

            foreach (var file in Directory.EnumerateFiles(folderPath, "*.asciifont.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    // Use source-generated JSON context for trimming compatibility
                    var model = JsonSerializer.Deserialize(json, AsciiGlyphSetJsonContext.Default.AsciiGlyphSetJson);
                    if (model is null) continue;
                    if (string.IsNullOrWhiteSpace(model.Ramp)) continue;

                    var name = string.IsNullOrWhiteSpace(model.Name)
                        ? Path.GetFileNameWithoutExtension(file)
                        : model.Name;

                    // Don't override built-in sets
                    if (_builtInNames.Contains(name)) continue;

                    var glyphWidth = model.GlyphWidth <= 0 ? 4 : model.GlyphWidth;
                    var glyphHeight = model.GlyphHeight <= 0 ? 4 : model.GlyphHeight;

                    // If we have bitmaps, parse them as hex -> ulong.
                    IReadOnlyList<ulong> bitmaps = Array.Empty<ulong>();
                    if (model.Bitmaps is { Count: > 0 } list &&
                        list.Count == model.Ramp.Length &&
                        glyphWidth * glyphHeight <= 64)
                    {
                        var parsed = new List<ulong>(list.Count);
                        foreach (var hex in list)
                        {
                            if (ulong.TryParse(hex, NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out var bits))
                            {
                                parsed.Add(bits);
                            }
                            else
                            {
                                // malformed bitmap entry, bail on this file
                                parsed.Clear();
                                break;
                            }
                        }

                        if (parsed.Count == model.Ramp.Length)
                            bitmaps = parsed;
                    }

                    Register(new AsciiGlyphSet
                    {
                        Name = name,
                        Ramp = model.Ramp,
                        GlyphWidth = glyphWidth,
                        GlyphHeight = glyphHeight,
                        GlyphBitmaps = bitmaps
                    });
                }
                catch
                {
                    // ignore malformed files; keep editor robust
                }
            }
        }
    }
}
