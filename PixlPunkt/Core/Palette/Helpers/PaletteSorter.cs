using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Enums;

namespace PixlPunkt.Core.Palette.Helpers
{
    /// <summary>
    /// Provides sorting algorithms for palette colors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PaletteSorter offers various sorting strategies for organizing palette colors in meaningful ways.
    /// Sorting can be based on color properties (hue, saturation, lightness), individual channels (RGB),
    /// or perceptual brightness (luminance).
    /// </para>
    /// <para><strong>Sort Modes:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Hue</strong>: Groups colors by their position on the color wheel (reds ? oranges ? yellows ? greens ? etc.)</item>
    /// <item><strong>Saturation</strong>: Orders from grayscale/muted to vivid/pure colors</item>
    /// <item><strong>Lightness</strong>: Orders from dark to light (HSL lightness)</item>
    /// <item><strong>Luminance</strong>: Orders by perceived brightness (WCAG relative luminance)</item>
    /// <item><strong>Red/Green/Blue</strong>: Orders by individual color channel values</item>
    /// <item><strong>Reverse</strong>: Reverses the current order</item>
    /// </list>
    /// </remarks>
    public static class PaletteSorter
    {
        /// <summary>
        /// Sorts a list of colors according to the specified sort mode.
        /// </summary>
        /// <param name="colors">The colors to sort (packed BGRA uint values).</param>
        /// <param name="mode">The sorting algorithm to apply.</param>
        /// <returns>A new sorted list of colors.</returns>
        public static List<uint> Sort(IReadOnlyList<uint> colors, PaletteSortMode mode)
        {
            if (colors == null || colors.Count == 0)
                return [];

            return mode switch
            {
                PaletteSortMode.Default => [.. colors],
                PaletteSortMode.Hue => SortByHue(colors),
                PaletteSortMode.Saturation => SortBySaturation(colors),
                PaletteSortMode.Lightness => SortByLightness(colors),
                PaletteSortMode.Luminance => SortByLuminance(colors),
                PaletteSortMode.Red => SortByRed(colors),
                PaletteSortMode.Green => SortByGreen(colors),
                PaletteSortMode.Blue => SortByBlue(colors),
                PaletteSortMode.Reverse => Reverse(colors),
                _ => [.. colors]
            };
        }

        /// <summary>
        /// Sorts colors by hue (position on the color wheel, 0-360 degrees).
        /// </summary>
        /// <remarks>
        /// Groups colors by their base color family. Grays (low saturation) are sorted by lightness
        /// and placed at the end to keep chromatic colors together.
        /// </remarks>
        public static List<uint> SortByHue(IReadOnlyList<uint> colors)
        {
            const double saturationThreshold = 0.1;

            return [.. colors.OrderBy(c =>
            {
                var color = ColorUtil.ToColor(c);
                ColorUtil.ToHSL(color, out double h, out double s, out double l);

                // Put very low-saturation colors (grays) at the end, sorted by lightness
                if (s < saturationThreshold)
                    return 360.0 + l;

                return h;
            })];
        }

        /// <summary>
        /// Sorts colors by saturation (0% grayscale to 100% vivid).
        /// </summary>
        public static List<uint> SortBySaturation(IReadOnlyList<uint> colors)
        {
            return [.. colors.OrderBy(c =>
            {
                var color = ColorUtil.ToColor(c);
                ColorUtil.ToHSL(color, out _, out double s, out _);
                return s;
            })];
        }

        /// <summary>
        /// Sorts colors by lightness (0% black to 100% white).
        /// </summary>
        public static List<uint> SortByLightness(IReadOnlyList<uint> colors)
        {
            return [.. colors.OrderBy(c =>
            {
                var color = ColorUtil.ToColor(c);
                ColorUtil.ToHSL(color, out _, out _, out double l);
                return l;
            })];
        }

        /// <summary>
        /// Sorts colors by relative luminance (perceived brightness using WCAG formula).
        /// </summary>
        /// <remarks>
        /// Uses the WCAG 2.0 relative luminance formula which accounts for human perception.
        /// Green contributes most to perceived brightness, followed by red, then blue.
        /// </remarks>
        public static List<uint> SortByLuminance(IReadOnlyList<uint> colors)
        {
            return [.. colors.OrderBy(c => ColorUtil.RelativeLuminance(c))];
        }

        /// <summary>
        /// Sorts colors by red channel value (0-255).
        /// </summary>
        public static List<uint> SortByRed(IReadOnlyList<uint> colors)
        {
            return [.. colors.OrderBy(c => ColorUtil.GetR(c))];
        }

        /// <summary>
        /// Sorts colors by green channel value (0-255).
        /// </summary>
        public static List<uint> SortByGreen(IReadOnlyList<uint> colors)
        {
            return [.. colors.OrderBy(c => ColorUtil.GetG(c))];
        }

        /// <summary>
        /// Sorts colors by blue channel value (0-255).
        /// </summary>
        public static List<uint> SortByBlue(IReadOnlyList<uint> colors)
        {
            return [.. colors.OrderBy(c => ColorUtil.GetB(c))];
        }

        /// <summary>
        /// Reverses the order of colors.
        /// </summary>
        public static List<uint> Reverse(IReadOnlyList<uint> colors)
        {
            return [.. colors.Reverse()];
        }

        /// <summary>
        /// Gets a human-readable display name for a sort mode.
        /// </summary>
        public static string GetDisplayName(PaletteSortMode mode) => mode switch
        {
            PaletteSortMode.Default => "Default (from palette)",
            PaletteSortMode.Hue => "Hue",
            PaletteSortMode.Saturation => "Saturation",
            PaletteSortMode.Lightness => "Lightness",
            PaletteSortMode.Luminance => "Luminance",
            PaletteSortMode.Red => "Red Channel",
            PaletteSortMode.Green => "Green Channel",
            PaletteSortMode.Blue => "Blue Channel",
            PaletteSortMode.Reverse => "Reverse",
            _ => mode.ToString()
        };
    }
}
