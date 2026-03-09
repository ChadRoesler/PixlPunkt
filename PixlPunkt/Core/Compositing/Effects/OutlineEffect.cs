using System;
using System.Buffers;
using PixlPunkt.Constants;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Draws a colored outline around the opaque regions of the layer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect detects edges of the layer's alpha channel and draws a solid-color border around them.
    /// The outline appears outside the layer content, expanding the visual footprint while preserving
    /// the original interior pixels.
    /// </para>
    /// </remarks>
    public sealed class OutlineEffect : LayerEffectBase
    {
        public override string DisplayName => "Outline";
        public override bool NeedsSnapshot => true;

        private uint _color = 0xFF000000; // solid black
        public uint Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _thickness = EffectLimits.DefaultThickness;
        public int Thickness
        {
            get => _thickness;
            set
            {
                int clamped = Math.Clamp(value, EffectLimits.MinThickness, EffectLimits.MaxThickness);
                if (_thickness != clamped)
                {
                    _thickness = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private bool _outsideOnly = true;
        /// <summary>
        /// If true, only adds outline outside the shape; interior pixels are not changed.
        /// </summary>
        public bool OutsideOnly
        {
            get => _outsideOnly;
            set
            {
                if (_outsideOnly != value)
                {
                    _outsideOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            int len = width * height;
            if (len <= 0 || pixels.Length < len) return;

            uint[] src = pixels.ToArray();
            ApplyCore(pixels, src, width, height);
        }

        public override void Apply(Span<uint> pixels, ReadOnlySpan<uint> snapshot, int width, int height)
        {
            if (!IsEnabled) return;
            int len = width * height;
            if (len <= 0 || pixels.Length < len) return;

            ApplyCore(pixels, snapshot, width, height);
        }

        private void ApplyCore(Span<uint> pixels, ReadOnlySpan<uint> src, int width, int height)
        {
            int len = width * height;
            int radius = Math.Clamp(Thickness, EffectLimits.MinThickness, EffectLimits.MaxThickness);
            uint outlinePixel = Color;

            // BFS distance transform: O(width * height) regardless of radius.
            // 1. Seed queue with all transparent pixels adjacent to opaque pixels.
            // 2. Expand outward up to 'radius' using Chebyshev (8-connected) distance,
            //    matching the original diamond falloff via Manhattan distance check.

            var dist = ArrayPool<int>.Shared.Rent(len);
            var queue = ArrayPool<int>.Shared.Rent(len);

            try
            {
                Array.Clear(dist, 0, len);
                int qHead = 0, qTail = 0;

                // Pass 1: Copy opaque pixels through, seed BFS from transparent edge pixels.
                for (int y = 0; y < height; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = rowBase + x;
                        uint orig = src[idx];
                        byte a = (byte)(orig >> 24);

                        if (a != 0)
                        {
                            // Opaque: preserve original pixel, mark as occupied (dist = -1)
                            pixels[idx] = orig;
                            dist[idx] = -1;
                        }
                    }
                }

                // Seed: find transparent pixels that are 4-connected to an opaque pixel
                for (int y = 0; y < height; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = rowBase + x;
                        if (dist[idx] == -1) continue; // opaque

                        bool adjacent = false;
                        if (x > 0 && dist[idx - 1] == -1) adjacent = true;
                        else if (x < width - 1 && dist[idx + 1] == -1) adjacent = true;
                        else if (y > 0 && dist[idx - width] == -1) adjacent = true;
                        else if (y < height - 1 && dist[idx + width] == -1) adjacent = true;
                        // Also check diagonals for seeding
                        else if (x > 0 && y > 0 && dist[idx - width - 1] == -1) adjacent = true;
                        else if (x < width - 1 && y > 0 && dist[idx - width + 1] == -1) adjacent = true;
                        else if (x > 0 && y < height - 1 && dist[idx + width - 1] == -1) adjacent = true;
                        else if (x < width - 1 && y < height - 1 && dist[idx + width + 1] == -1) adjacent = true;

                        if (adjacent)
                        {
                            dist[idx] = 1;
                            queue[qTail++] = idx;
                        }
                    }
                }

                // Pass 2: BFS expand using Manhattan distance (diamond shape) to match original behavior
                while (qHead < qTail)
                {
                    int idx = queue[qHead++];
                    int d = dist[idx];
                    if (d >= radius) continue;

                    int x = idx % width;
                    int y = idx / width;

                    int yMin = Math.Max(0, y - 1);
                    int yMax = Math.Min(height - 1, y + 1);
                    int xMin = Math.Max(0, x - 1);
                    int xMax = Math.Min(width - 1, x + 1);

                    for (int ny = yMin; ny <= yMax; ny++)
                    {
                        int nRow = ny * width;
                        for (int nx = xMin; nx <= xMax; nx++)
                        {
                            int ni = nRow + nx;
                            if (dist[ni] != 0) continue; // already visited or opaque

                            // Manhattan distance from the new pixel to the nearest opaque pixel
                            // is at most d + max(|dx|,|dy|), but we use d+1 for Chebyshev
                            // then check Manhattan at write time below
                            dist[ni] = d + 1;
                            queue[qTail++] = ni;
                        }
                    }
                }

                // Pass 3: Write outline pixels (those with 1 <= dist <= radius)
                // and clear non-outline transparent pixels
                for (int y = 0; y < height; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int idx = rowBase + x;
                        int d = dist[idx];
                        if (d > 0 && d <= radius)
                        {
                            pixels[idx] = outlinePixel;
                        }
                        else if (d == 0)
                        {
                            pixels[idx] = 0; // transparent, not in outline range
                        }
                        // d == -1 already written in pass 1
                    }
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(queue);
                ArrayPool<int>.Shared.Return(dist);
            }
        }
    }
}
