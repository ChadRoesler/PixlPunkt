# Palette

Color palette management in PixlPunkt.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_16.png" width="16"> Palette Panel (`F6`)

The Palette Panel is your color management hub:

```
???????????????????????????????
?  [FG] [BG]  [? Swap (X)]   ?
???????????????????????????????
? ?????????????????????????  ?
? ?   ?   ?   ?   ?   ?   ?  ?
? ?????????????????????????  ?
? ?   ?   ?   ?   ?   ?   ?  ?  Palette
? ?????????????????????????  ?  Swatches
? ?   ?   ?   ?   ?   ?   ?  ?
? ?????????????????????????  ?
???????????????????????????????
? [+] Add  [?] Menu          ?
???????????????????????????????
```

---

## Foreground & Background Colors

### Foreground (FG)
- Main drawing color
- Used by Brush, Fill, Shapes
- Click to open Color Picker

### Background (BG)
- Secondary color
- Used by Eraser (to background mode)
- Shift+Click to sample to BG

### Swap Colors
- Press `X` to swap FG and BG
- Quick way to alternate between two colors

---

## Working with Swatches

### Select a Color
- **Left-click** swatch = Set as foreground
- **Right-click** swatch = Context menu

### Add Color to Palette
1. Set your foreground color
2. Click **[+] Add** or press `Ctrl+Shift+A`
3. Color is added to palette

### Remove Color
- Right-click swatch ? **Remove**
- Or drag swatch out of palette

### Rearrange Colors
- Drag swatches to reorder
- Group similar colors together

### Edit Color
- Double-click swatch to edit in Color Picker
- Changes update the palette swatch

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_16.png" width="16"> Preset Palettes

PixlPunkt includes classic palettes:

| Palette | Colors | Era |
|---------|--------|-----|
| **NES** | 54 | 8-bit Nintendo |
| **GameBoy** | 4 | Original GB green |
| **GameBoy Pocket** | 4 | GB Pocket gray |
| **CGA** | 16 | IBM PC CGA |
| **EGA** | 64 | IBM PC EGA |
| **PICO-8** | 16 | Fantasy console |
| **Commodore 64** | 16 | C64 |
| **Amstrad CPC** | 27 | Amstrad |
| **MSX** | 15 | MSX computers |
| **Sega Master System** | 64 | SMS |
| **DB16** | 16 | DawnBringer 16 |
| **DB32** | 32 | DawnBringer 32 |
| **AAP-64** | 64 | Adigun A. Polack |
| **Lospec500** | 500 | Lospec community |

### Loading a Preset

1. Click **[?] Menu** in Palette Panel
2. Select **Load Preset**
3. Choose from the list

---

## Custom Palettes

### Save Current Palette

1. **[?] Menu** ? **Save Palette**
2. Enter a name
3. Saved to your Palettes folder

### Palette File Location

- Windows: `%LocalAppData%\PixlPunkt\Palettes\`
- macOS: `~/Library/Application Support/PixlPunkt/Palettes/`
- Linux: `~/.local/share/PixlPunkt/Palettes/`

### Palette Formats

| Format | Extension | Notes |
|--------|-----------|-------|
| PixlPunkt | `.pxpp` | Native, includes name |
| GIMP | `.gpl` | Compatible with GIMP |
| Adobe ASE | `.ase` | Adobe Swatch Exchange |
| Hex List | `.hex` | One hex color per line |
| PAL | `.pal` | JASC/PSP format |
| PNG | `.png` | Extract colors from image |

### Import Palette

1. **[?] Menu** ? **Import Palette**
2. Select file in any supported format
3. Colors are loaded

### Export Palette

1. **[?] Menu** ? **Export Palette**
2. Choose format
3. Select destination

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eyedropper_16.png" width="16"> Extract Palette from Image

Pull colors from any image:

1. **[?] Menu** ? **Extract from Image**
2. Select an image file
3. Options:
   - **Max Colors:** Limit extracted colors
   - **Quantization:** How colors are selected
4. Extracted palette is loaded

### Quantization Methods

| Method | Best For |
|--------|----------|
| **Median Cut** | General purpose |
| **Octree** | Photographic images |
| **K-Means** | Precise color matching |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/data_sunburst_16.png" width="16"> Palette Ramps

Create color ramps (gradients) within your palette:

### Generate Ramp

1. Select start color (click swatch, hold Ctrl)
2. Select end color (Ctrl+click second swatch)
3. **[?] Menu** ? **Generate Ramp**
4. Choose number of steps
5. Intermediate colors are created

### Hue Shifting

For more interesting ramps:
- Enable **Hue Shift** option
- Warm colors shift toward orange
- Cool colors shift toward blue
- Creates more natural-looking gradients

---

## Color Sorting

Organize your palette:

### Sort Options

**[?] Menu** ? **Sort Palette**

| Sort By | Description |
|---------|-------------|
| **Hue** | Group by color family |
| **Saturation** | Gray ? Vibrant |
| **Luminosity** | Dark ? Light |
| **Red/Green/Blue** | By channel value |

### Manual Sorting

Drag swatches to create your own organization:
- Group skin tones together
- Separate shadows from highlights
- Organize by usage (character, environment, etc.)

---

## Palette Operations

### Clear Palette
- **[?] Menu** ? **Clear Palette**
- Removes all colors
- Keeps FG/BG colors

### Reset to Default
- **[?] Menu** ? **Reset to Default**
- Loads the default starter palette

### Duplicate Palette
- **[?] Menu** ? **Duplicate**
- Creates a copy for experimentation

---

## Per-Document Palettes

Each document stores its own palette:
- Save with the `.pxp` file
- Different projects can have different palettes
- Palette travels with the document

### Default Palette

Set a palette as default for new documents:
1. Load or create your preferred palette
2. **[?] Menu** ? **Set as Default**
3. New documents start with this palette

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `X` | Swap FG/BG |
| `Ctrl+Shift+A` | Add FG to palette |
| `1-9` | Quick select palette colors |
| `Right-click` | Pick color from canvas |

---

## Tips

### Limit Your Palette
- Fewer colors = More cohesive art
- Start with 8-16 colors
- Add only when necessary

### Plan Your Ramps
- 3-5 colors per ramp
- Include highlight, midtone, shadow
- Consider hue shifting for depth

### Name Your Palettes
- Use descriptive names
- Include project or style
- Example: "Forest_Environment_16col"

### Test on Canvas
- Try colors together before committing
- Use a test area on your canvas
- Check contrast and readability

---

## See Also

- [[Color Picker|Color-Picker]] - Detailed color selection
- [[Gradient Fill|Gradient-Fill]] - Gradient tool
- [[Dithering]] - Dithering techniques
- [[Quick Start|Quick-Start]] - Getting started
