using System;
using System.Numerics;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Ascii;

namespace PixlPunkt.Uno.Core.Compositing.Effects
{
    public sealed class AsciiEffect : LayerEffectBase
    {
        public override string DisplayName => "ASCII";

        // ─────────────────────────────────────────────
        // New modes for your “lost memory” implementation
        // ─────────────────────────────────────────────
        public enum GlyphSelectionMode
        {
            /// <summary>Map average luminance to a ramp character (classic ASCII).</summary>
            LuminanceRamp = 0,

            /// <summary>Downsample cell to a binary signature and pick closest glyph by XOR+PopCount.</summary>
            PatternMatch = 1
        }

        public enum BinarizeMode
        {
            /// <summary>Threshold each sample against the cell’s average luminance.</summary>
            CellAverage = 0,

            /// <summary>Threshold each sample against a fixed value in [0..1].</summary>
            FixedThreshold = 1,

            /// <summary>Ordered dither (4×4 Bayer) against the sample luminance.</summary>
            Bayer4x4 = 2
        }

        public enum ForegroundColorMode
        {
            /// <summary>Use average cell color for the glyph.</summary>
            Average = 0,

            /// <summary>Use dominant cell color for the glyph.</summary>
            Dominant = 1
        }

        public enum BackgroundFillMode
        {
            /// <summary>Off pixels become transparent.</summary>
            Transparent = 0,

            /// <summary>Off pixels become secondary dominant cell color.</summary>
            SecondaryDominant = 1,

            /// <summary>Off pixels become dominant cell color.</summary>
            Dominant = 2
        }

        private string _glyphSetName = "Basic";
        private int _cellWidth = EffectLimits.DefaultBlockSize * 2;
        private int _cellHeight = EffectLimits.DefaultBlockSize * 2;
        private bool _applyOnAlpha = false;
        private bool _invert = false;
        private double _contrast = 1.0;

        private GlyphSelectionMode _selectionMode = GlyphSelectionMode.LuminanceRamp;
        private BinarizeMode _binarizeMode = BinarizeMode.CellAverage;
        private double _fixedThreshold = 0.5; // 0..1
        private ForegroundColorMode _foregroundColorMode = ForegroundColorMode.Average;
        private BackgroundFillMode _backgroundFillMode = BackgroundFillMode.Transparent;

        /// <summary>
        /// Name of the glyph set to use (matches AsciiGlyphSet.Name).
        /// </summary>
        public string GlyphSetName
        {
            get => _glyphSetName;
            set
            {
                value ??= "Basic";
                if (_glyphSetName == value) return;
                _glyphSetName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Cell width in pixels (columns per glyph).
        /// </summary>
        public int CellWidth
        {
            get => _cellWidth;
            set
            {
                value = Math.Clamp(value, EffectLimits.MinBlockSize, EffectLimits.MaxBlockSize);
                if (_cellWidth == value) return;
                _cellWidth = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Cell height in pixels (rows per glyph).
        /// </summary>
        public int CellHeight
        {
            get => _cellHeight;
            set
            {
                value = Math.Clamp(value, EffectLimits.MinBlockSize, EffectLimits.MaxBlockSize);
                if (_cellHeight == value) return;
                _cellHeight = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// If false, fully transparent pixels are ignored when computing averages.
        /// If true, transparent pixels still contribute to brightness (for pure-alpha art).
        /// </summary>
        public bool ApplyOnAlpha
        {
            get => _applyOnAlpha;
            set
            {
                if (_applyOnAlpha == value) return;
                _applyOnAlpha = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Invert brightness → dark areas use bright glyphs and vice versa.
        /// For PatternMatch, this inverts the binary signature (and thus prefers inverted masks).
        /// </summary>
        public bool Invert
        {
            get => _invert;
            set
            {
                if (_invert == value) return;
                _invert = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Brightness contrast curve. 1.0 = linear, greater than 1 darkens midtones, less than 1 brightens.
        /// </summary>
        public double Contrast
        {
            get => _contrast;
            set
            {
                value = Math.Clamp(value, 0.1, 4.0);
                if (Math.Abs(_contrast - value) < 1e-6) return;
                _contrast = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// How to choose the glyph (ramp vs pattern matching).
        /// </summary>
        public GlyphSelectionMode Selection
        {
            get => _selectionMode;
            set
            {
                if (_selectionMode == value) return;
                _selectionMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// How to binarize/downsample the source cell when using PatternMatch.
        /// </summary>
        public BinarizeMode Binarize
        {
            get => _binarizeMode;
            set
            {
                if (_binarizeMode == value) return;
                _binarizeMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Fixed threshold in [0..1] used when Binarize = FixedThreshold.
        /// </summary>
        public double FixedThreshold
        {
            get => _fixedThreshold;
            set
            {
                value = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_fixedThreshold - value) < 1e-6) return;
                _fixedThreshold = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Foreground (glyph ink) color.
        /// </summary>
        public ForegroundColorMode ForegroundColor
        {
            get => _foregroundColorMode;
            set
            {
                if (_foregroundColorMode == value) return;
                _foregroundColorMode = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Background fill behavior for the "off" pixels of the glyph mask.
        /// </summary>
        public BackgroundFillMode Background
        {
            get => _backgroundFillMode;
            set
            {
                if (_backgroundFillMode == value) return;
                _backgroundFillMode = value;
                OnPropertyChanged();
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            if (width <= 0 || height <= 0) return;

            int total = width * height;
            if (pixels.Length < total) return;

            // Resolve glyph set
            var set = AsciiGlyphSets.Get(GlyphSetName);
            string ramp = set.Ramp;
            if (string.IsNullOrEmpty(ramp)) return;
            int rampLen = ramp.Length;

            int cellW = Math.Max(1, CellWidth);
            int cellH = Math.Max(1, CellHeight);

            // Glyph bitmaps (optional). We allow scaling glyphW/H -> cellW/H.
            var bitmaps = set.GlyphBitmaps;
            bool hasBitmaps =
                bitmaps is { Count: > 0 } &&
                bitmaps.Count == rampLen &&
                set.GlyphWidth > 0 &&
                set.GlyphHeight > 0 &&
                (set.GlyphWidth * set.GlyphHeight) <= 64;

            int glyphW = hasBitmaps ? set.GlyphWidth : 0;
            int glyphH = hasBitmaps ? set.GlyphHeight : 0;

            // Work on a copy of the source
            uint[] src = pixels.ToArray();
            Span<uint> dst = pixels;

            // Pre-allocate histogram arrays ONCE outside the loop to avoid stack overflow
            // Using heap allocation for safety with large images
            int[] keys = new int[256];
            int[] keyCounts = new int[256];

            for (int y0 = 0; y0 < height; y0 += cellH)
            {
                int yMax = Math.Min(y0 + cellH, height);

                for (int x0 = 0; x0 < width; x0 += cellW)
                {
                    int xMax = Math.Min(x0 + cellW, width);

                    long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
                    int count = 0;

                    // Dominant/secondary via quantized histogram (4 bits/channel => 4096 buckets).
                    // We cap tracking to 256 unique buckets per cell to keep this fast.
                    int domKey = -1, domCount = 0;
                    int secKey = -1, secCount = 0;

                    // Clear the histogram arrays for this cell
                    int used = 0;

                    // ─────────────────────────────────────────────
                    // 1) Average + dominant/secondary in the cell
                    // ─────────────────────────────────────────────
                    for (int y = y0; y < yMax; y++)
                    {
                        int row = y * width;
                        for (int x = x0; x < xMax; x++)
                        {
                            uint c = src[row + x];
                            byte a = (byte)(c >> 24);
                            if (a == 0 && !ApplyOnAlpha) continue;

                            byte r = (byte)(c >> 16);
                            byte g = (byte)(c >> 8);
                            byte b = (byte)c;

                            sumR += r;
                            sumG += g;
                            sumB += b;
                            sumA += a;
                            count++;

                            // Quantize 8-bit -> 4-bit per channel
                            int key = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);

                            int idx = -1;
                            for (int i = 0; i < used; i++)
                            {
                                if (keys[i] == key) { idx = i; break; }
                            }

                            if (idx < 0)
                            {
                                if (used >= 256) continue; // saturated
                                keys[used] = key;
                                keyCounts[used] = 1;
                                used++;
                            }
                            else
                            {
                                keyCounts[idx]++;
                            }
                        }
                    }

                    if (count == 0)
                    {
                        // No contributing pixels; just clear this block.
                        for (int y = y0; y < yMax; y++)
                        {
                            int row = y * width;
                            for (int x = x0; x < xMax; x++)
                                dst[row + x] = 0;
                        }
                        continue;
                    }

                    // Find top-2 buckets
                    for (int i = 0; i < used; i++)
                    {
                        int k = keys[i];
                        int c = keyCounts[i];

                        if (c > domCount)
                        {
                            secKey = domKey; secCount = domCount;
                            domKey = k; domCount = c;
                        }
                        else if (c > secCount && k != domKey)
                        {
                            secKey = k; secCount = c;
                        }
                    }

                    byte avgR = (byte)(sumR / count);
                    byte avgG = (byte)(sumG / count);
                    byte avgB = (byte)(sumB / count);
                    byte avgA = (byte)(sumA / count);

                    // Cell luminance (normalized) based on average color
                    double cellLum = (0.299 * avgR + 0.587 * avgG + 0.114 * avgB) / 255.0;
                    if (Math.Abs(Contrast - 1.0) > 1e-6)
                    {
                        cellLum = Math.Pow(Math.Clamp(cellLum, 0.0, 1.0), Contrast);
                    }

                    // Resolve FG/BG colors
                    (byte fgR, byte fgG, byte fgB) = ForegroundColor == ForegroundColorMode.Dominant
                        ? DequantizeRgb(domKey >= 0 ? domKey : (((avgR >> 4) << 8) | ((avgG >> 4) << 4) | (avgB >> 4)))
                        : (avgR, avgG, avgB);

                    uint fgPacked =
                        ((uint)avgA << 24) |
                        ((uint)fgR << 16) |
                        ((uint)fgG << 8) |
                        fgB;

                    uint bgPacked = 0;
                    if (Background != BackgroundFillMode.Transparent)
                    {
                        int bgKey = Background == BackgroundFillMode.SecondaryDominant && secKey >= 0 ? secKey : domKey;
                        if (bgKey < 0) bgKey = ((avgR >> 4) << 8) | ((avgG >> 4) << 4) | (avgB >> 4);

                        var (br, bg, bb) = DequantizeRgb(bgKey);
                        bgPacked =
                            ((uint)avgA << 24) |
                            ((uint)br << 16) |
                            ((uint)bg << 8) |
                            bb;
                    }

                    // ─────────────────────────────────────────────
                    // 2) Choose glyph
                    // ─────────────────────────────────────────────
                    int glyphIndex;

                    if (Selection == GlyphSelectionMode.PatternMatch && hasBitmaps)
                    {
                        // Build signature at glyphW x glyphH (e.g., 4x4 base set, 8x8, etc.)
                        ulong cellBits = BuildCellBits(
                            src, width,
                            x0, y0, xMax, yMax,
                            glyphW, glyphH,
                            Binarize,
                            FixedThreshold,
                            cellLum,
                            ApplyOnAlpha,
                            Invert,
                            Contrast);

                        glyphIndex = FindBestGlyphByXorPopcount(cellBits, bitmaps);
                    }
                    else
                    {
                        // Classic ramp selection using cell luminance
                        double t = cellLum;

                        if (Invert) t = 1.0 - t;
                        t = Math.Clamp(t, 0.0, 1.0);

                        glyphIndex = (int)Math.Round(t * (rampLen - 1));
                        if (glyphIndex < 0) glyphIndex = 0;
                        if (glyphIndex >= rampLen) glyphIndex = rampLen - 1;
                    }

                    // ─────────────────────────────────────────────
                    // 3) Draw
                    //    - If we have bitmaps -> true glyph mask (scaled to cellW x cellH).
                    //    - Otherwise -> fill the whole cell (mosaic).
                    // ─────────────────────────────────────────────
                    if (!hasBitmaps)
                    {
                        for (int y = y0; y < yMax; y++)
                        {
                            int row = y * width;
                            for (int x = x0; x < xMax; x++)
                                dst[row + x] = fgPacked;
                        }
                    }
                    else
                    {
                        ulong bits = bitmaps[glyphIndex];

                        // Scale glyphW x glyphH mask into cellW x cellH
                        for (int gy = 0; gy < glyphH; gy++)
                        {
                            int sy0 = y0 + (gy * cellH) / glyphH;
                            int sy1 = y0 + ((gy + 1) * cellH) / glyphH;
                            if (sy0 >= height) break;
                            if (sy1 > height) sy1 = height;

                            for (int gx = 0; gx < glyphW; gx++)
                            {
                                int sx0 = x0 + (gx * cellW) / glyphW;
                                int sx1 = x0 + ((gx + 1) * cellW) / glyphW;
                                if (sx0 >= width) break;
                                if (sx1 > width) sx1 = width;

                                int bitIndex = gy * glyphW + gx;
                                bool on = ((bits >> bitIndex) & 1UL) != 0;

                                uint color = on
                                    ? fgPacked
                                    : (Background == BackgroundFillMode.Transparent ? 0u : bgPacked);

                                for (int y = sy0; y < sy1; y++)
                                {
                                    int row = y * width;
                                    for (int x = sx0; x < sx1; x++)
                                        dst[row + x] = color;
                                }
                            }
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static (byte r, byte g, byte b) DequantizeRgb(int key12)
        {
            // key: r4<<8 | g4<<4 | b4  where each channel is 0..15
            int r4 = (key12 >> 8) & 0xF;
            int g4 = (key12 >> 4) & 0xF;
            int b4 = key12 & 0xF;

            // Expand 0..15 -> 0..255
            return ((byte)(r4 * 17), (byte)(g4 * 17), (byte)(b4 * 17));
        }

        private static int FindBestGlyphByXorPopcount(ulong cellBits, System.Collections.Generic.IReadOnlyList<ulong> glyphBitmaps)
        {
            int bestIndex = 0;
            int bestDist = int.MaxValue;

            for (int i = 0; i < glyphBitmaps.Count; i++)
            {
                ulong v = cellBits ^ glyphBitmaps[i];
                int dist = (int)BitOperations.PopCount(v);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                    if (bestDist == 0) break;
                }
            }

            return bestIndex;
        }

        private static ulong BuildCellBits(
            uint[] src, int width,
            int x0, int y0, int xMax, int yMax,
            int sigW, int sigH,
            BinarizeMode binarize,
            double fixedThreshold,
            double cellLumNormalized,
            bool applyOnAlpha,
            bool invert,
            double contrast)
        {
            // Downsample the cell into sigW x sigH “samples” by averaging luminance in each sub-rect.
            // Each sample becomes 1 bit in the returned mask.
            // Bit order: row-major (gy*sigW + gx).

            if (sigW <= 0 || sigH <= 0) return 0UL;

            int cellW = xMax - x0;
            int cellH = yMax - y0;

            // If using CellAverage, threshold is derived from the cell’s luminance (already contrast-adjusted in Apply).
            double cellThresh = binarize == BinarizeMode.CellAverage
                ? Math.Clamp(cellLumNormalized, 0.0, 1.0)
                : 0.0;

            ulong bits = 0UL;

            for (int gy = 0; gy < sigH; gy++)
            {
                int sy0 = y0 + (gy * cellH) / sigH;
                int sy1 = y0 + ((gy + 1) * cellH) / sigH;
                if (sy1 <= sy0) sy1 = Math.Min(sy0 + 1, yMax);

                for (int gx = 0; gx < sigW; gx++)
                {
                    int sx0 = x0 + (gx * cellW) / sigW;
                    int sx1 = x0 + ((gx + 1) * cellW) / sigW;
                    if (sx1 <= sx0) sx1 = Math.Min(sx0 + 1, xMax);

                    long lumSum = 0;
                    int lumCount = 0;

                    for (int y = sy0; y < sy1; y++)
                    {
                        int row = y * width;
                        for (int x = sx0; x < sx1; x++)
                        {
                            uint c = src[row + x];
                            byte a = (byte)(c >> 24);
                            if (a == 0 && !applyOnAlpha) continue;

                            byte r = (byte)(c >> 16);
                            byte g = (byte)(c >> 8);
                            byte b = (byte)c;

                            lumSum += (long)(0.299 * r + 0.587 * g + 0.114 * b);
                            lumCount++;
                        }
                    }

                    double lum = lumCount == 0 ? 0.0 : (lumSum / (double)lumCount) / 255.0; // 0..1
                    lum = Math.Clamp(lum, 0.0, 1.0);

                    // Apply contrast curve to sample luminance to match ramp behavior feel
                    if (Math.Abs(contrast - 1.0) > 1e-6)
                    {
                        lum = Math.Pow(lum, contrast);
                    }

                    double threshold = binarize switch
                    {
                        BinarizeMode.FixedThreshold => fixedThreshold,
                        BinarizeMode.Bayer4x4 => Bayer4x4Threshold(gx, gy),
                        _ => cellThresh
                    };

                    bool on = lum > threshold;
                    if (invert) on = !on;

                    int bitIndex = gy * sigW + gx;
                    if (on) bits |= (1UL << bitIndex);
                }
            }

            return bits;
        }

        private static double Bayer4x4Threshold(int x, int y)
        {
            // 4×4 Bayer matrix normalized to [0..1]
            // Using (v + 0.5)/16 as threshold.
            //
            //  0  8  2 10
            // 12  4 14  6
            //  3 11  1  9
            // 15  7 13  5
            int xi = x & 3;
            int yi = y & 3;

            int v = yi switch
            {
                0 => xi switch { 0 => 0, 1 => 8, 2 => 2, 3 => 10, _ => 0 },
                1 => xi switch { 0 => 12, 1 => 4, 2 => 14, 3 => 6, _ => 0 },
                2 => xi switch { 0 => 3, 1 => 11, 2 => 1, 3 => 9, _ => 0 },
                _ => xi switch { 0 => 15, 1 => 7, 2 => 13, 3 => 5, _ => 0 }
            };

            return (v + 0.5) / 16.0;
        }
    }
}
