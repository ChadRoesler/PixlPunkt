using System;
using System.Collections.Generic;
using System.Globalization;
using PixlPunkt.Core.Coloring.Helpers;
using Windows.UI;

namespace PixlPunkt.Core.Palette.Helpers.Defaults
{
    /// <summary>
    /// Provides a curated collection of predefined color palettes for pixel art and retro aesthetics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DefaultPalettes contains 24 carefully selected color palettes spanning retro hardware limitations
    /// (Game Boy, NES, C64), modern pixel art standards (PICO-8, DawnBringer), thematic collections
    /// (Cyberpunk, Steampunk, Vaporwave), and utility palettes (Grayscale, Skin Tones). Each palette
    /// is optimized for specific artistic styles or technical constraints.
    /// </para>
    /// <para><strong>Palette Categories:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Retro Hardware</strong> (7 palettes): Game Boy (4 colors), NES (16), C64 (16), CGA (16),
    /// EGA/VGA (16), ZX Spectrum (16), PICO-8 (16) - Historically accurate limitations of classic systems</item>
    /// <item><strong>Artistic Themes</strong> (11 palettes): Pastelly, Dystopian, Brutal, Steampunk, Dieselpunk,
    /// Cyberpunk Neon, Vaporwave, Earthy, Cosmic, WaterWorld, Kingdom Death Monster - Curated mood palettes</item>
    /// <item><strong>Community Standards</strong> (3 palettes): DawnBringer16 (DB16), Seren-12 Midnight Orchard,
    /// Solarized - Popular palettes from pixel art community</item>
    /// <item><strong>Utility</strong> (3 palettes): Grayscale (16), Skin Tones (16), PixlPunkt Default (100) -
    /// General-purpose and specialized color sets</item>
    /// </list>
    /// <para><strong>Access Patterns:</strong></para>
    /// <para>
    /// Palettes can be accessed via:
    /// <br/>- <see cref="All"/>: Complete list for UI enumeration and palette pickers
    /// <br/>- <see cref="ByName"/>: Case-insensitive dictionary lookup for programmatic access
    /// </para>
    /// <para><strong>Implementation Notes:</strong></para>
    /// <para>
    /// All palettes use hex string literals converted to BGRA via <see cref="RGB"/> helper method.
    /// The <see cref="DistinctStable"/> method enables palette composition from multiple sub-palettes
    /// while preserving color order and eliminating duplicates (used for CGA Mode 4 merged palette).
    /// </para>
    /// </remarks>
    /// <seealso cref="NamedPalette"/>
    /// <seealso cref="PaletteService"/>
    public static class DefaultPalettes
    {
        /// <summary>
        /// Gets the complete collection of all predefined palettes.
        /// </summary>
        /// <value>
        /// A read-only list containing 24 <see cref="NamedPalette"/> instances.
        /// Order matches the initialization sequence for consistent UI presentation.
        /// </value>
        public static IReadOnlyList<NamedPalette> All { get; }

        /// <summary>
        /// Gets a dictionary for fast case-insensitive palette lookup by name.
        /// </summary>
        /// <value>
        /// A dictionary keyed by palette name (case-insensitive) containing all available palettes.
        /// Useful for programmatic palette selection and deserialization scenarios.
        /// </value>
        public static readonly Dictionary<string, NamedPalette> ByName;

        /// <summary>
        /// Static constructor initializing all palettes and lookup structures.
        /// </summary>
        static DefaultPalettes()
        {
            var list = new List<NamedPalette>
            {
                PixlPunktPalette(),
                GameBoy(),
                NES_Compact16(),
                C64_Pepto16(),
                CGA_Mode4_Merged16(),
                PICO8_16(),
                Pastelly_16(),
                Dystopian_16(),
                Brutal_16(),
                Steampunk_16(),
                Dieselpunk_16(),
                Cyberpunk_Neon16(),
                Vaporwave_16(),
                Earthy_16(),
                Seren12_MidnightOrchard(),
                WaterWorld_16(),
                KingdomDeathMonster_16(),
                Cosmic_16(),
                EGA16(),
                ZX_Spectrum16(),
                DawnBringer16(),
                Solarized16(),
                Grayscale16(),
                SkinTones16(),
                K6BDThroneDream24(),
            };

            All = list;
            ByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (var p in list) ByName[p.Name] = p;
        }

        /// <summary>
        /// Converts a hex color string (RRGGBB) to packed BGRA format.
        /// </summary>
        /// <param name="hex">Hex color string (with or without '#' prefix). Must be exactly 6 characters.</param>
        /// <returns>Packed 32-bit BGRA color value with full opacity (alpha = 255).</returns>
        /// <exception cref="ArgumentException">Thrown if hex string is not 6 characters (RRGGBB).</exception>
        /// <remarks>
        /// Parses RGB components using invariant culture hex parsing. Alpha channel is always set to
        /// 255 (fully opaque) as all default palettes use solid colors.
        /// </remarks>
        private static uint RGB(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length != 6) throw new ArgumentException($"Hex must be RRGGBB: {hex}");
            byte r = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ColorUtil.ToBGRA(Color.FromArgb(255, r, g, b));
        }

        /// <summary>
        /// Converts an array of hex strings to packed BGRA color array.
        /// </summary>
        /// <param name="hexes">Array of hex color strings in RRGGBB format.</param>
        /// <returns>Array of packed BGRA color values.</returns>
        /// <remarks>
        /// Convenience method for palette definition using string literals. Each hex string is
        /// converted via <see cref="RGB"/> method.
        /// </remarks>
        private static uint[] RGBArr(params string[] hexes)
        {
            var arr = new uint[hexes.Length];
            for (int i = 0; i < hexes.Length; i++) arr[i] = RGB(hexes[i]);
            return arr;
        }

        /// <summary>
        /// Merges multiple color arrays into a single array with duplicates removed, preserving order.
        /// </summary>
        /// <param name="groups">Variable number of color arrays to merge.</param>
        /// <returns>Flattened array containing unique colors in encounter order.</returns>
        /// <remarks>
        /// Used to construct composite palettes (e.g., CGA Mode 4 merged from multiple sub-palettes).
        /// First occurrence of each unique color is preserved; subsequent duplicates are skipped.
        /// Order stability ensures consistent palette presentation.
        /// </remarks>
        private static uint[] DistinctStable(params uint[][] groups)
        {
            var seen = new HashSet<uint>();
            var outList = new List<uint>();
            foreach (var g in groups)
                foreach (var c in g)
                    if (seen.Add(c)) outList.Add(c);
            return outList.ToArray();
        }

        // ── PALETTE DEFINITIONS ──────────────────────────────────────────

        /// <summary>
        /// Creates the PixlPunkt Default palette (100 colors).
        /// </summary>
        /// <returns>A comprehensive general-purpose palette with broad color coverage.</returns>
        /// <remarks>
        /// Large palette suitable for general pixel art without color limitations. Includes
        /// natural tones, vivid accents, and a wide luminance range for maximum flexibility.
        /// </remarks>
        private static NamedPalette PixlPunktPalette() => new(
            "PixlPunkt Default",
            RGBArr(
                "#0A411E", "#113B0A", "#1D2E55", "#228899", "#267E31", "#268617", "#2B406D", "#2D232F",
                "#2EA41C", "#2EB6CE", "#2F4E42", "#322C3C", "#351F41", "#3C333F", "#3C6B54", "#3D2C30",
                "#3F2C1A", "#41467D", "#435BB9", "#463C2E", "#49464E", "#4B404A", "#4B6594", "#4E2D65",
                "#521717", "#533434", "#559777", "#603E2C", "#618BDB", "#62503A", "#677BC2", "#6D6874",
                "#70B336", "#71432A", "#73562F", "#754B8F", "#77461F", "#7FB7DC", "#801B09", "#867A2F",
                "#896B3A", "#8C372F", "#8DA0C5", "#90E669", "#924604", "#972A68", "#982727", "#998797",
                "#A0B9E9", "#A1CE99", "#A22E1D", "#A7A3AD", "#A87575", "#A8D87F", "#A9B3D0", "#AE92BE",
                "#B0682E", "#B2D3EA", "#B3E4EE", "#B8391A", "#B9A488", "#C16D48", "#C28656", "#C4AC20",
                "#C83988", "#C86C9C", "#CB3434", "#D1E4F2", "#D2EBDD", "#D35331", "#D7CED6", "#DAB69A",
                "#DADDE1", "#DEA7C3", "#DF4F22", "#DFC945", "#E08585", "#E7D66E", "#E8EEF2", "#E9730C",
                "#EE9060", "#EE9260", "#EFD535", "#F0ECE3", "#F1E7A8", "#F69A49", "#F6E160", "#FAF6EF",
                "#FCD9A3", "#FDF7DA", "#FEC99B", "#005784", "#12182A", "#4FAFA5", "#D0E9CF"
            )
        );

        /// <summary>
        /// Creates the Game Boy DMG palette (4 colors).
        /// </summary>
        /// <returns>Authentic 4-color greenscale palette from original Game Boy hardware.</returns>
        /// <remarks>
        /// Classic monochrome LCD palette with distinctive olive-green tint. Essential for
        /// authentic Game Boy aesthetic. Colors: near-black, dark green, light green, off-white.
        /// </remarks>
        private static NamedPalette GameBoy() => new(
            "Game Boy (DMG)",
            RGBArr("#0F380F", "#306230", "#8BAC0F", "#9BBC0F")
        );

        /// <summary>
        /// Creates the NES Compact 16 palette (16 colors).
        /// </summary>
        /// <returns>Color palette from the NES console, optimized for compactness.</returns>
        /// <remarks>
        /// 16-color palette used in the NES (Nintendo Entertainment System) for simple and
        /// compact color representation. Enables creation of nostalgic NES-style graphics.
        /// </remarks>
        private static NamedPalette NES_Compact16() => new(
            "NES (Compact 16)",
            RGBArr(
                "#000000", "#7C7C7C", "#BCBCBC", "#F8F8F8",
                "#0000BC", "#0078F8", "#3CBCFC", "#6888FC",
                "#00A800", "#B8F818", "#58D854", "#F8B800",
                "#E45C10", "#F87858", "#D800CC", "#9878F8"
            )
        );

        /// <summary>
        /// Creates the Commodore 64 palette (16 colors) using Pepto color settings.
        /// </summary>
        /// <returns>16-color palette for the Commodore 64 computer.</returns>
        /// <remarks>
        /// Based on the extended color palette used in many Commodore 64 games and demos.
        /// Provides vibrant colors and unique combinations for retro Commodore 64 aesthetics.
        /// </remarks>
        private static NamedPalette C64_Pepto16() => new(
            "Commodore 64 (16)",
            RGBArr(
                "#000000", "#FFFFFF", "#68372B", "#70A4B2",
                "#6F3D86", "#588D43", "#352879", "#B8C76F",
                "#6F4F25", "#433900", "#9A6759", "#444444",
                "#6C6C6C", "#9AD284", "#6C5EB5", "#959595"
            )
        );

        /// <summary>
        /// Creates the CGA Mode 4 merged palette (16 colors).
        /// </summary>
        /// <returns>Merged CGA palette simulating Mode 4 display with 16 colors.</returns>
        /// <remarks>
        /// Combines low and high nibble colors from CGA graphics card to simulate
        /// the expanded color capabilities of Mode 4. Essential for accurate CGA graphics emulation.
        /// </remarks>
        private static NamedPalette CGA_Mode4_Merged16()
        {
            var p0Low = RGBArr("#000000", "#00AAAA", "#AA00AA", "#AAAAAA");
            var p0High = RGBArr("#000000", "#00FFFF", "#FF00FF", "#FFFFFF");
            var p1Low = RGBArr("#000000", "#00AA00", "#AA0000", "#AA5500");
            var p1High = RGBArr("#000000", "#55FF55", "#FF5555", "#FFFF55");

            var merged = DistinctStable(p0Low, p0High, p1Low, p1High);
            return new("CGA Mode 4 (Merged 16)", merged);
        }

        /// <summary>
        /// Creates the PICO-8 palette (16 colors).
        /// </summary>
        /// <returns>Color palette from the PICO-8 fantasy console.</returns>
        /// <remarks>
        /// The PICO-8 uses a 16-color palette with specific choices for retro-style visuals.
        /// This palette is great for achieving the unique look of PICO-8 games and demos.
        /// </remarks>
        private static NamedPalette PICO8_16() => new(
            "PICO-8 (16)",
            RGBArr(
                "#000000", "#1D2B53", "#7E2553", "#008751",
                "#AB5236", "#5F574F", "#C2C3C7", "#FFF1E8",
                "#FF004D", "#FFA300", "#FFEC27", "#00E436",
                "#29ADFF", "#83769C", "#FF77A8", "#FFCCAA"
            )
        );

        /// <summary>
        /// Creates the Pastelly palette (16 colors).
        /// </summary>
        /// <returns>A pastel color palette with soft and light tones.</returns>
        /// <remarks>
        /// Features gentle pastel shades for creating soft and calming pixel art.
        /// Suitable for themes like spring, light-hearted content, or anything requiring
        /// a subtle and understated color scheme.
        /// </remarks>
        private static NamedPalette Pastelly_16() => new(
            "Pastelly (16)",
            RGBArr(
                "#FFC7DC", "#FF9FC8", "#FFB59E", "#FFD19A",
                "#FFE37E", "#D6F59E", "#A7EDC7", "#8DE6DA",
                "#9FD7FF", "#B7C4FF", "#CFB8FF", "#EAB6F3",
                "#FFB7B3", "#FFCAA6", "#CFE5FF", "#D4F4E8"
            )
        );

        /// <summary>
        /// Creates the Dystopian palette (16 colors).
        /// </summary>
        /// <returns>A color palette evoking a dark, dystopian atmosphere.</returns>
        /// <remarks>
        /// Contains deep, muted colors suitable for creating ominous and gritty scenes.
        /// Ideal for horror, sci-fi, or any artwork requiring a sense of foreboding.
        /// </remarks>
        private static NamedPalette Dystopian_16() => new(
            "Dystopian (16)",
            RGBArr(
                "#0B0E12", "#14181D", "#1C2228", "#272D34",
                "#333B43", "#414B54", "#556169", "#6B7880",
                "#8A9499", "#A2A9AD", "#7A3E2E", "#6D5B3C",
                "#8F7F2A", "#6B8F2A", "#2F5B59", "#7E1E1E"
            )
        );

        /// <summary>
        /// Creates the Brutal(ist) palette (16 colors).
        /// </summary>
        /// <returns>A high-contrast grayscale palette with accent colors.</returns>
        /// <remarks>
        /// Based on the Brutalist art movement, this palette features stark contrasts
        /// and vibrant accents. Great for high-impact, visually striking designs.
        /// </remarks>
        private static NamedPalette Brutal_16() => new(
            "Brutal(ist) (16)",
            RGBArr(
                "#000000", "#1E1E1E", "#2E2E2E", "#3E3E3E",
                "#515151", "#666666", "#7A7A7A", "#909090",
                "#A6A6A6", "#BDBDBD", "#D5D5D5", "#FFFFFF",
                "#FFD600", "#FF3D00", "#B71C1C", "#1565C0"
            )
        );

        /// <summary>
        /// Creates the Steampunk palette (16 colors).
        /// </summary>
        /// <returns>A palette reflecting steampunk aesthetics with bronze and teal tones.</returns>
        /// <remarks>
        /// Inspired by steampunk fiction and design, featuring metallic tones, dark woods,
        /// and steam-powered machinery hues. Perfect for creating Victorian-era sci-fi art.
        /// </remarks>
        private static NamedPalette Steampunk_16() => new(
            "Steampunk (16)",
            RGBArr(
                "#1B1410", "#2F241C", "#3B2A1E", "#5C3B28",
                "#7A4E2D", "#9C6133", "#B97A3D", "#D9A657",
                "#EED9A0", "#6E2E2A", "#A4472C", "#2F3B3A",
                "#3E5C59", "#2E5D52", "#76A697", "#C4D7C6"
            )
        );

        /// <summary>
        /// Creates the Dieselpunk palette (16 colors).
        /// </summary>
        /// <returns>A palette capturing the gritty and industrial feel of dieselpunk.</returns>
        /// <remarks>
        /// Earthy tones, industrial grays, and oil-slick colors characterize this palette.
        /// Ideal for dieselpunk, near-future, or retro-futuristic scenes with an industrial edge.
        /// </remarks>
        private static NamedPalette Dieselpunk_16() => new(
            "Dieselpunk (16)",
            RGBArr(
                "#0F1A24", "#1C2B36", "#2B3C4A", "#3C4F61",
                "#4E6173", "#5F6F7B", "#2F3E2E", "#3E563E",
                "#5E7A5E", "#3A2C24", "#5C4738", "#8C332B",
                "#C2A24B", "#B7C1C8", "#E2DCC5", "#6A7075"
            )
        );

        /// <summary>
        /// Creates the Cyberpunk Neon palette (16 colors).
        /// </summary>
        /// <returns>A vibrant, high-contrast palette for cyberpunk themes.</returns>
        /// <remarks>
        /// Fluorescent colors and stark contrasts evoke a high-tech, low-life aesthetic.
        /// Suitable for cyberpunk, retro-futuristic, or any artwork needing a neon-lit look.
        /// </remarks>
        private static NamedPalette Cyberpunk_Neon16() => new(
            "Cyberpunk Neon (16)",
            RGBArr(
                "#070716", "#1B003A", "#3D0E66", "#7A00FF",
                "#FF00A8", "#FF2E88", "#FF6EC7", "#00E5FF",
                "#00A3FF", "#00F0B5", "#2CFF8F", "#C4FF00",
                "#FFE500", "#FF7A00", "#FF3B30", "#1A1F2E"
            )
        );

        /// <summary>
        /// Creates the Vaporwave palette (16 colors).
        /// </summary>
        /// <returns>A pastel and neon palette for vaporwave aesthetics.</returns>
        /// <remarks>
        /// Combines pastel colors with vibrant neons, inspired by vaporwave music and art.
        /// Ideal for dreamlike, retro-futuristic, or synthwave-inspired artwork.
        /// </remarks>
        private static NamedPalette Vaporwave_16() => new(
            "Vaporwave (16)",
            RGBArr(
                "#FDE2FF", "#E4C1F9", "#D1AEFF", "#C1E9F9",
                "#B8F3FF", "#A1F0E5", "#F7CAC9", "#FFC4D6",
                "#F9E79F", "#F8F0E3", "#91A8D0", "#B8E1FF",
                "#5B5F97", "#FF6F91", "#FFC75F", "#4ECDC4"
            )
        );

        /// <summary>
        /// Creates the Earthy Naturals palette (16 colors).
        /// </summary>
        /// <returns>A palette of natural earth tones and plant life colors.</returns>
        /// <remarks>
        /// Inspired by nature, featuring greens, browns, and other earthy hues.
        /// Suitable for environmental pixel art, from lush forests to dry deserts.
        /// </remarks>
        private static NamedPalette Earthy_16() => new(
            "Earthy Naturals (16)",
            RGBArr(
                "#3B2F2F", "#6B4F3F", "#8D5524", "#A0522D",
                "#D2691E", "#B5651D", "#C68642", "#E0C097",
                "#5D473A", "#8B7765", "#7C6A5C", "#C0B283",
                "#556B2F", "#7F8B52", "#8A9A5B", "#4D5D53"
            )
        );

        /// <summary>
        /// Creates the Seren-12 Midnight Orchard palette (12 colors).
        /// </summary>
        /// <returns>A limited palette with rich, deep colors for dramatic effects.</returns>
        /// <remarks>
        /// Fewer colors (12) allow for quicker pixel art animation and consistency.
        /// Suggested for use in sprite animations, ensuring uniformity across frames.
        /// </remarks>
        private static NamedPalette Seren12_MidnightOrchard() => new(
            "Seren-12 (Midnight Orchard)",
            RGBArr(
                "#0B1026", "#1E2A78", "#6F3AFF", "#FF59E6",
                "#00C2C7", "#6FF3D6", "#B7FF4C", "#FFD23F",
                "#FF7A48", "#FF4D6D", "#E6F0FF", "#1A1E2A"
            )
        );

        /// <summary>
        /// Creates the WaterWorld palette (16 colors).
        /// </summary>
        /// <returns>A palette inspired by aquatic and oceanic themes.</returns>
        /// <remarks>
        /// Various shades of blue and aqua, reminiscent of water, sky, and marine life.
        /// Great for creating underwater scenes or artworks needing a watery theme.
        /// </remarks>
        private static NamedPalette WaterWorld_16() => new(
            "WaterWorld (16)",
            RGBArr(
                "#EAF7FF", "#CFF3FF", "#8DE3F4", "#55D1D7",
                "#55CABA", "#2FA6A6", "#4AB2F8", "#1E80E0",
                "#165AA8", "#0C376A", "#061F3A", "#4E6B2A",
                "#2E5A3A", "#B85A3C", "#D9C7A3", "#A4B3B8"
            )
        );

        /// <summary>
        /// Creates the Kingdom Death Monster palette (16 colors).
        /// </summary>
        /// <returns>A muted and dark palette for horror and dark fantasy themes.</returns>
        /// <remarks>
        /// Used in the Kingdom Death: Monster board game, featuring a range of muted tones
        /// and dark, moody colors. Enhances the horror and tension of the game’s setting.
        /// </remarks>
        private static NamedPalette KingdomDeathMonster_16() => new(
            "Kingdom Death Monster (16)",
            RGBArr(
                "#EDE9E0", "#D7D0C4", "#BEB9B1", "#A5A1A0",
                "#8E8A89", "#727072", "#57555A", "#3C3A3F",
                "#121115", "#7A1E24", "#C23832", "#D7B570",
                "#9CAD7A", "#4B395C", "#6B7E8B", "#5A463C"
            )
        );

        /// <summary>
        /// Creates the Cosmic palette (16 colors).
        /// </summary>
        /// <returns>A dark and vibrant palette for cosmic and space themes.</returns>
        /// <remarks>
        /// Deep blacks and vibrant, otherworldly colors depict a cosmic or space setting.
        /// Suitable for sci-fi artwork, starry skies, or any cosmic-themed pixel art.
        /// </remarks>
        private static NamedPalette Cosmic_16() => new(
            "Cosmic (16)",
            RGBArr(
                "#05060B", "#0B0F1C", "#141B34", "#1E1033",
                "#2B2E5E", "#3E2E7A", "#0D4F8A", "#1479C9",
                "#2EE8FF", "#45FFA6", "#7C5CFF", "#C54FFF",
                "#FF4DD2", "#FFB400", "#FFE9B8", "#F8F7FF"
            )
        );

        /// <summary>
        /// Creates the EGA / VGA palette (16 colors).
        /// </summary>
        /// <returns>Palette from the EGA and VGA graphics standards.</returns>
        /// <remarks>
        /// Standard 16-color palette used in EGA and VGA computer graphics, compatible
        /// with a wide range of software and games from the era.
        /// </remarks>
        private static NamedPalette EGA16() => new(
            "EGA / VGA (16)",
            RGBArr(
                "#000000", "#0000AA", "#00AA00", "#00AAAA",
                "#AA0000", "#AA00AA", "#AA5500", "#AAAAAA",
                "#555555", "#5555FF", "#55FF55", "#55FFFF",
                "#FF5555", "#FF55FF", "#FFFF55", "#FFFFFF"
            )
        );

        /// <summary>
        /// Creates the ZX Spectrum palette (16 colors).
        /// </summary>
        /// <returns>Color palette from the ZX Spectrum home computer.</returns>
        /// <remarks>
        /// The ZX Spectrum used a 16-color palette with bright and distinct choices.
        /// This palette is essential for recreating the look of ZX Spectrum software and games.
        /// </remarks>
        private static NamedPalette ZX_Spectrum16() => new(
            "ZX Spectrum (16)",
            RGBArr(
                "#000000", "#0000D7", "#D70000", "#D700D7",
                "#00D700", "#00D7D7", "#D7D700", "#D7D7D7",
                "#2B2B2B", "#0000FF", "#FF0000", "#FF00FF",
                "#00FF00", "#00FFFF", "#FFFF00", "#FFFFFF"
            )
        );

        /// <summary>
        /// Creates the DawnBringer palette (16 colors).
        /// </summary>
        /// <returns>A vibrant and balanced palette for general pixel art use.</returns>
        /// <remarks>
        /// Well-balanced colors suitable for a wide range of pixel art styles and genres.
        /// Popularized by the pixel art community for its versatility and vibrancy.
        /// </remarks>
        private static NamedPalette DawnBringer16() => new(
            "DawnBringer (DB16)",
            RGBArr(
                "#140C1C", "#442434", "#30346D", "#4E4A4E",
                "#854C30", "#346524", "#D04648", "#757161",
                "#597DCE", "#D27D2C", "#8595A1", "#6DAA2C",
                "#D2AA99", "#6DC2CA", "#DAD45E", "#DEEED6"
            )
        );

        /// <summary>
        /// Creates the Solarized palette (16 colors).
        /// </summary>
        /// <returns>A palette with scientifically chosen colors for readability and aesthetics.</returns>
        /// <remarks>
        /// Originally designed for coding, this palette is known for its ease on the eyes
        /// and effective use of contrast. Works well in both dim and bright environments.
        /// </remarks>
        private static NamedPalette Solarized16() => new(
            "Solarized (16)",
            RGBArr(
                "#002B36", "#073642", "#586E75", "#657B83",
                "#839496", "#93A1A1", "#EEE8D5", "#FDF6E3",
                "#B58900", "#CB4B16", "#DC322F", "#D33682",
                "#6C71C4", "#268BD2", "#2AA198", "#859900"
            )
        );

        /// <summary>
        /// Creates the Grayscale palette (16 colors).
        /// </summary>
        /// <returns>A smooth gradient of grays from black to white.</returns>
        /// <remarks>
        /// Useful for shading, highlighting, and creating depth in pixel art.
        /// Also acts as a base for artists to create custom palettes from a neutral starting point.
        /// </remarks>
        private static NamedPalette Grayscale16() => new(
            "Grayscale (16)",
            RGBArr(
                "#000000", "#111111", "#222222", "#333333",
                "#444444", "#555555", "#666666", "#777777",
                "#888888", "#999999", "#AAAAAA", "#BBBBBB",
                "#CCCCCC", "#DDDDDD", "#EEEEEE", "#FFFFFF"
            )
        );

        /// <summary>
        /// Creates the Skin Tones palette (16 colors).
        /// </summary>
        /// <returns>A diverse range of human skin tones from deep to pale.</returns>
        /// <remarks>
        /// Specialized palette for character art covering a broad spectrum of melanin levels.
        /// Graduated steps ensure smooth shading transitions for portraits and figure work.
        /// Useful for inclusive character representation across diverse ethnicities.
        /// </remarks>
        private static NamedPalette SkinTones16() => new(
            "Skin Tones (16)",
            RGBArr(
                "#1B0E08", "#2E1A10", "#4B2E1F", "#5A3A2E",
                "#6A4638", "#7D5542", "#8F624A", "#A27156",
                "#B58062", "#C78E6F", "#D89E7E", "#E4AF8F",
                "#EDBF9F", "#F3CFB5", "#F8E0CC", "#FDECE0"
            )
        );

        /// <summary>
        /// Creates the K6BD: Throne Dream palette (16 colors).
        /// </summary>
        /// <returns>A diverse range of colors inspired by Kill 6 Billion Demons.</returns>
        /// <remarks>
        /// Specialized palette for character art covering a broad spectrum of melanin levels.
        /// Graduated steps ensure smooth shading transitions for portraits and figure work.
        /// Useful for inclusive character representation across diverse ethnicities.
        /// </remarks>
        private static NamedPalette K6BDThroneDream24() => new(
            "K6BD: Throne Dream (24)",
            RGBArr(
                "#07060A", "#151021", "#2E2A3A", "#6C6472", 
                "#F2E9D8", "#FFF8F0", "#E6C44A", "#1E3A8A",
                "#D01F2B", "#F4D6E6", "#E8872C", "#5A2A86",
                "#2BB673", "#B78B2A", "#7A5520", "#8A0F18",
                "#4A0810", "#2B1348", "#5E2E8C", "#9A6BFF",
                "#17407D", "#2B7CFF", "#24D6FF", "#D8FF57"
            )
        );
    }
}
