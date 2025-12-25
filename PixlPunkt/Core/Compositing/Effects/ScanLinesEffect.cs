using System;
using PixlPunkt.Constants;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Applies horizontal scanline overlay effect simulating CRT monitor or retro display appearance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect draws alternating horizontal lines across the layer, creating a classic CRT television
    /// or vintage computer monitor aesthetic. The scanlines can be customized with adjustable thickness,
    /// spacing, intensity, and color tint.
    /// </para>
    /// <para><strong>Parameters:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Intensity</strong> (0..1): Controls blend strength between original pixel and line tint.</item>
    /// <item><strong>LineThickness</strong> (1..128 px): Height of each scanline in pixels.</item>
    /// <item><strong>LineSpacing</strong> (0..128 px): Gap between scanlines.</item>
    /// <item><strong>Color</strong> (BGRA): Tint color for the scanlines (default: black).</item>
    /// <item><strong>ApplyOnAlpha</strong>: When true, draws scanlines over fully transparent pixels.</item>
    /// </list>
    /// <para><strong>Algorithm:</strong></para>
    /// <para>
    /// For each pixel in a scanline row (determined by y % (thickness + spacing)), blends the original
    /// color toward the tint color using linear interpolation: <c>result = original × (1 - t) + tint × t</c>,
    /// where <c>t = intensity × tintAlpha</c>. Transparent pixels are either skipped or filled with
    /// tinted color based on <see cref="ApplyOnAlpha"/> setting.
    /// </para>
    /// </remarks>
    public sealed partial class ScanLinesEffect : LayerEffectBase
    {
        public override string DisplayName => "Scanlines";

        private double _intensity = 0.8;   // 0..1
        private int _thickness = EffectLimits.MinLineSpacing;        // px
        private int _spacing = EffectLimits.MinLineSpacing;          // gap between lines in px
        private bool _applyOnAlpha = false;   // apply on transparent pixels
        // Tint color for the scanlines (0xAARRGGBB). Default = black.
        private uint _color = 0xFF000000;
        public bool ApplyOnAlpha
        {
            get => _applyOnAlpha;
            set
            {
                if (_applyOnAlpha != value)
                {
                    _applyOnAlpha = value;
                    OnPropertyChanged();
                }
            }
        }
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

        public double Intensity
        {
            get => _intensity;
            set
            {
                value = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_intensity - value) < double.Epsilon) return;
                _intensity = value;
                OnPropertyChanged();
            }
        }

        public int LineThickness
        {
            get => _thickness;
            set
            {
                value = Math.Clamp(value, EffectLimits.MinLineSpacing, EffectLimits.MaxBlockSize);
                if (_thickness == value) return;
                _thickness = value;
                OnPropertyChanged();
            }
        }

        public int LineSpacing
        {
            get => _spacing;
            set
            {
                value = Math.Clamp(value, 0, EffectLimits.MaxBlockSize);
                if (_spacing == value) return;
                _spacing = value;
                OnPropertyChanged();
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            if (width <= 0 || height <= 0) return;

            int total = width * height;
            if (pixels.Length < total) return;

            if (Intensity <= 0.0) return;

            // Unpack line tint (0xAARRGGBB)
            byte lineA = (byte)(Color >> 24);
            byte lineR = (byte)(Color >> 16);
            byte lineG = (byte)(Color >> 8);
            byte lineB = (byte)Color;

            float lineAf = lineA / 255f;
            float intensity = (float)Intensity;

            // Blend factor for non-transparent pixels
            float t = intensity * lineAf;
            float invT = 1f - t;

            int group = LineThickness + LineSpacing;
            if (group <= 0) group = 1;

            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                bool isLineRow = (y % group) < LineThickness;

                for (int x = 0; x < width; x++, idx++)
                {
                    if (!isLineRow) continue;

                    uint c = pixels[idx];
                    byte a = (byte)(c >> 24);

                    // Fully transparent pixel
                    if (a == 0)
                    {
                        if (!ApplyOnAlpha)
                            continue;

                        // Draw pure line tint on empty pixels, alpha scaled by Intensity
                        byte newA = (byte)Math.Clamp((int)MathF.Round(lineA * intensity), 0, 255);
                        if (newA == 0)
                            continue;

                        pixels[idx] = (uint)newA << 24 | (uint)lineR << 16 | (uint)lineG << 8 | lineB;
                        continue;
                    }

                    // Normal: blend existing color toward line tint
                    byte r = (byte)(c >> 16);
                    byte g = (byte)(c >> 8);
                    byte b = (byte)c;

                    float rOut = r * invT + lineR * t;
                    float gOut = g * invT + lineG * t;
                    float bOut = b * invT + lineB * t;

                    byte rF = (byte)Math.Clamp((int)MathF.Round(rOut), 0, 255);
                    byte gF = (byte)Math.Clamp((int)MathF.Round(gOut), 0, 255);
                    byte bF = (byte)Math.Clamp((int)MathF.Round(bOut), 0, 255);

                    pixels[idx] = (uint)a << 24 | (uint)rF << 16 | (uint)gF << 8 | bF;
                }
            }
        }
    }
}
