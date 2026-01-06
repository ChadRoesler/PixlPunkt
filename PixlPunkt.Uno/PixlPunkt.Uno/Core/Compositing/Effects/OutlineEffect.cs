using System;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Coloring.Helpers;

namespace PixlPunkt.Uno.Core.Compositing.Effects
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

            // Snapshot original for neighborhood tests
            uint[] src = pixels.ToArray();
            int radius = Math.Clamp(Thickness, EffectLimits.MinThickness, EffectLimits.MaxThickness);

            ColorUtil.Unpack(Color, out byte oa, out byte or_, out byte og, out byte ob);

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;
                    uint orig = src[idx];
                    ColorUtil.Unpack(orig, out byte a, out byte _, out byte _, out byte _);

                    // Keep original interior pixels
                    if (a != 0)
                    {
                        pixels[idx] = orig;
                        continue;
                    }

                    // Transparent pixel: see if it's near any solid pixel
                    bool nearSolid = false;
                    for (int oy = -radius; oy <= radius && !nearSolid; oy++)
                    {
                        int ny = y + oy;
                        if ((uint)ny >= (uint)height) continue;

                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            if (Math.Abs(ox) + Math.Abs(oy) > radius) continue; // diamond falloff

                            int nx = x + ox;
                            if ((uint)nx >= (uint)width) continue;

                            uint n = src[ny * width + nx];
                            ColorUtil.Unpack(n, out byte na, out _, out _, out _);
                            if (na != 0)
                            {
                                nearSolid = true;
                                break;
                            }
                        }
                    }

                    if (nearSolid)
                    {
                        pixels[idx] = ColorUtil.Pack(oa, or_, og, ob);
                    }
                    else
                    {
                        pixels[idx] = 0; // remain transparent
                    }
                }
            }
        }
    }
}
