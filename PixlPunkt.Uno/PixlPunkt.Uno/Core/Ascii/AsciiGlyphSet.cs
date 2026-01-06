using System;
using System.Collections.Generic;

namespace PixlPunkt.Uno.Core.Ascii
{
    /// <summary>
    /// Describes an ASCII glyph set usable by the ASCII layer effect.
    /// </summary>
    public sealed class AsciiGlyphSet
    {
        /// <summary>
        /// Human-readable name (e.g. "Basic", "Blocks", "DFRunesLight").
        /// Used for UI and lookup.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Characters ordered from darkest (index 0) to brightest.
        /// Example: " .:-=+*#%@".
        /// </summary>
        public string Ramp { get; init; } = string.Empty;

        /// <summary>
        /// Glyph cell width in pixels (for bitmap rendering).
        /// </summary>
        public int GlyphWidth { get; init; } = 8;

        /// <summary>
        /// Glyph cell height in pixels (for bitmap rendering).
        /// </summary>
        public int GlyphHeight { get; init; } = 8;

        /// <summary>
        /// Optional per-glyph bitmaps (row-major, top-left bit = bit 0).
        /// Length should match Ramp.Length when provided.
        /// If empty, the effect will fall back to "fill entire cell" mode.
        /// </summary>
        public IReadOnlyList<ulong> GlyphBitmaps { get; init; } = Array.Empty<ulong>();
    }
}
