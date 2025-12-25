# Custom Glyph Sets for ASCII Effect

PixlPunkt's ASCII effect supports custom glyph sets that define how brightness values are mapped to characters with custom bitmap patterns.

## Quick Start

1. Open **Settings → Glyph Set Editor**
2. Click the **+** button to create a new set
3. Edit the ramp (characters from light to dark)
4. Click each character button to edit its bitmap pattern
5. Click **Save** to save your set

Your custom sets are stored in `%LocalAppData%\PixlPunkt\GlyphSets\`.

## File Format

Custom glyph sets use `.asciifont.json` files. Here's an example:

```json
{
  "name": "Custom Example",
  "ramp": " .oO@",
  "glyphWidth": 4,
  "glyphHeight": 4,
  "bitmaps": [
    "0",
    "40",
    "660",
    "6F6",
    "FFFF"
  ]
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `name` | string | Display name for the glyph set |
| `ramp` | string | Characters ordered from lightest (index 0) to darkest |
| `glyphWidth` | number | Width of each glyph bitmap (4, 6, or 8) |
| `glyphHeight` | number | Height of each glyph bitmap (4, 6, or 8) |
| `bitmaps` | string[] | Hexadecimal bitmap values for each character |

### Bitmap Format

Each bitmap is stored as a hexadecimal string representing a `ulong` value. Bits are stored in row-major order:
- Bit 0 = top-left pixel
- Bit 1 = second pixel from left, top row
- ...and so on

For a 4×4 glyph:
```
Bit positions:
 0  1  2  3
 4  5  6  7
 8  9 10 11
12 13 14 15
```

**Example**: A centered dot pattern `0x40` (binary: `0100 0000`) has bit 6 set, which is position (2,1) - the center of row 1.

### Calculating Bitmaps

To create a bitmap value:

1. Draw your pattern on a grid (1 = filled, 0 = empty)
2. Read bits left-to-right, top-to-bottom
3. Convert the binary to hexadecimal

**Example for a 4×4 checkerboard**:
```
1 0 1 0  → bits 0-3  = 1010 (0xA)
0 1 0 1  → bits 4-7  = 0101 (0x5)
1 0 1 0  → bits 8-11 = 1010 (0xA)
0 1 0 1  → bits 12-15= 0101 (0x5)

Combined: 0xA5A5
```

## Built-in Glyph Sets

PixlPunkt includes several built-in glyph sets:

| Name | Ramp | Description |
|------|------|-------------|
| Basic | ` .:-=+*#%@` | Clean density ramp |
| Blocks | ` ░▒▓█` | Terminal block shading |
| SharpSymbols | `` .'`^*+x%#@`` | Noisy/techy look |
| DFRunesLight | ` .,:;|/+=*#%@` | Dwarf Fortress-esque |
| Boxes | ` -=║╬█` | Structural/wall-ish |
| DebugNumbers | `0123456789` | For debugging |
| Gradient16 | ` .,:;!|[{#%&$@MW` | 16-level smooth gradient |

## Tips

1. **Ramp ordering**: Characters should go from lightest (most empty bitmap) to darkest (most filled bitmap) for proper brightness mapping.

2. **Bitmap consistency**: Keep your bitmap patterns consistent with the character they represent. Darker characters should have more filled pixels.

3. **Testing**: Use the preview in the Glyph Set Editor to see how your patterns look at different sizes.

4. **Cell sizes**: 4×4 works well for small effects, 8×8 provides more detail but larger output.

5. **Custom Glyphs**: When creating custom shapes for glyphs, consider how they will appear when scaled down.

## Sharing Glyph Sets

To share a glyph set:
1. Export the `.asciifont.json` file from the Glyph Set Editor
2. Share the file with others
3. They can import it via the **Import** button in the Glyph Set Editor

---

See also: [User Guide](USER_GUIDE.md) | [Quick Start](QUICK_START.md)
