using System;

namespace PixlPunkt.Core.Compositing.Effects
{
    /// <summary>
    /// Adds film grain noise texture to the layer simulating analog film or sensor noise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This effect generates pseudo-random noise using a hash-based noise function and blends it with
    /// the layer pixels. Commonly used to add texture, simulate vintage film, or match noisy footage.
    /// The noise pattern is deterministic based on pixel position and seed value.
    /// </para>
    /// <para><strong>Parameters:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Amount</strong> (0..1): Noise intensity. At 0, no noise. At 1, maximum ±64 value deviation per channel.</item>
    /// <item><strong>Monochrome</strong>: When true, applies same noise value to all RGB channels (grayscale grain).
    /// When false, applies independent noise to each channel (colored grain).</item>
    /// <item><strong>Seed</strong>: Random seed controlling noise pattern. Different seeds produce different grain textures.</item>
    /// </list>
    /// <para><strong>Noise Algorithm:</strong></para>
    /// <para>
    /// Uses integer hash function combining pixel coordinates (x, y), seed, and channel index to generate
    /// pseudo-random values in range [-1..1]. The hash function is:
    /// <code>
    /// hash = (x × 73856093) XOR (y × 19349663) XOR (seed × 83492791) XOR (channel × 2654435761)
    /// hash = hash XOR (hash >> 13)
    /// hash = hash × 1274126177
    /// hash = hash XOR (hash >> 16)
    /// noise = ((hash AND 0xFFFF) / 65535) × 2 - 1
    /// </code>
    /// This produces spatially-coherent noise without requiring lookup tables or perlin noise generation.
    /// </para>
    /// <para><strong>Performance:</strong></para>
    /// <para>
    /// Very fast per-pixel operation using only integer arithmetic and bit operations. Processes entire
    /// frame in O(width × height) with minimal overhead beyond pixel access.
    /// </para>
    /// </remarks>
    public sealed class GrainEffect : LayerEffectBase
    {
        public override string DisplayName => "Grain";

        private double _amount = 0.3;   // 0..1
        private bool _monochrome = true;
        private int _seed = 1;

        public double Amount
        {
            get => _amount;
            set
            {
                value = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_amount - value) < double.Epsilon) return;
                _amount = value;
                OnPropertyChanged();
            }
        }

        public bool Monochrome
        {
            get => _monochrome;
            set
            {
                if (_monochrome == value) return;
                _monochrome = value;
                OnPropertyChanged();
            }
        }

        public int Seed
        {
            get => _seed;
            set
            {
                if (_seed == value) return;
                _seed = value;
                OnPropertyChanged();
            }
        }

        private static float Noise(int x, int y, int seed, int channel)
        {
            unchecked
            {
                uint n = (uint)(x * 73856093 ^ y * 19349663 ^ seed * 83492791 ^ channel * 2654435761u);
                n ^= n >> 13;
                n *= 1274126177u;
                n ^= n >> 16;
                // 0..1 → -1..1
                return (n & 0xFFFF) / 65535f * 2f - 1f;
            }
        }

        public override void Apply(Span<uint> pixels, int width, int height)
        {
            if (!IsEnabled) return;
            if (width <= 0 || height <= 0) return;
            if (pixels.Length < width * height) return;
            if (Amount <= 0.0) return;

            float amp = (float)(Amount * 64.0); // max ±64 steps

            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, idx++)
                {
                    uint col = pixels[idx];
                    byte a = (byte)(col >> 24);
                    if (a == 0) continue;

                    byte r = (byte)(col >> 16);
                    byte g = (byte)(col >> 8);
                    byte b = (byte)col;

                    if (Monochrome)
                    {
                        float n = Noise(x, y, Seed, 0);
                        int d = (int)Math.Round(n * amp);
                        int rI = r + d;
                        int gI = g + d;
                        int bI = b + d;
                        r = (byte)Math.Clamp(rI, 0, 255);
                        g = (byte)Math.Clamp(gI, 0, 255);
                        b = (byte)Math.Clamp(bI, 0, 255);
                    }
                    else
                    {
                        float nr = Noise(x, y, Seed, 0);
                        float ng = Noise(x, y, Seed, 1);
                        float nb = Noise(x, y, Seed, 2);

                        int rI = r + (int)Math.Round(nr * amp);
                        int gI = g + (int)Math.Round(ng * amp);
                        int bI = b + (int)Math.Round(nb * amp);

                        r = (byte)Math.Clamp(rI, 0, 255);
                        g = (byte)Math.Clamp(gI, 0, 255);
                        b = (byte)Math.Clamp(bI, 0, 255);
                    }

                    pixels[idx] = (uint)(a << 24 | r << 16 | g << 8 | b);
                }
            }
        }
    }
}
