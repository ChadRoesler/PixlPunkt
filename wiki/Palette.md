# Palette

Color palette management in PixlPunkt.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_16.png" width="16"> Palette Panel (`F6`)

The Palette Panel is your color management hub:

<img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/screenshots/PalettePanel.png">

---

## Foreground & Background Colors

### Foreground (FG)
- Main drawing color
- Used by Brush, Fill, Shapes
- Click to open Color Picker

### Background (BG)
- Secondary color
- Used by Eraser (to background mode)
- Used as gradient endpoint

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
2. Click **[+] Add** button
3. Color is added to palette

### Remove Color
- Right-click swatch → **Remove**

### Edit Color
- Double-click swatch to edit in Color Picker
- Changes update the palette swatch

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_16.png" width="16"> Preset Palettes

PixlPunkt includes 24 curated preset palettes across four categories:

### Retro Hardware (7 palettes)

| Palette | Colors | Description |
|---------|--------|-------------|
| **Game Boy (DMG)** | 4 | Classic green-tinted LCD |
| **NES (Compact 16)** | 16 | Nintendo Entertainment System |
| **Commodore 64 (16)** | 16 | C64 Pepto colors |
| **CGA Mode 4 (Merged 16)** | 16 | IBM PC CGA graphics |
| **PICO-8 (16)** | 16 | Fantasy console palette |
| **EGA / VGA (16)** | 16 | Standard PC graphics |
| **ZX Spectrum (16)** | 16 | Sinclair home computer |

### Artistic Themes (11 palettes)

| Palette | Colors | Description |
|---------|--------|-------------|
| **Pastelly (16)** | 16 | Soft pastel tones |
| **Dystopian (16)** | 16 | Dark, muted atmosphere |
| **Brutal(ist) (16)** | 16 | High-contrast with accents |
| **Steampunk (16)** | 16 | Bronze and teal Victorian |
| **Dieselpunk (16)** | 16 | Industrial grays and earth |
| **Cyberpunk Neon (16)** | 16 | Fluorescent high-tech |
| **Vaporwave (16)** | 16 | Pastel and neon retro-future |
| **Earthy Naturals (16)** | 16 | Natural browns and greens |
| **WaterWorld (16)** | 16 | Aquatic blues and aqua |
| **Kingdom Death Monster (16)** | 16 | Muted horror/dark fantasy |
| **Cosmic (16)** | 16 | Deep space and nebula |

### Community Standards (3 palettes)

| Palette | Colors | Description |
|---------|--------|-------------|
| **DawnBringer (DB16)** | 16 | Popular pixel art palette |
| **Seren-12 (Midnight Orchard)** | 12 | Rich dramatic colors |
| **Solarized (16)** | 16 | Eye-friendly coding palette |

### Utility (3 palettes)

| Palette | Colors | Description |
|---------|--------|-------------|
| **PixlPunkt Default** | 100 | Comprehensive general-purpose |
| **Grayscale (16)** | 16 | Smooth black to white |
| **Skin Tones (16)** | 16 | Diverse human skin tones |

### Loading a Preset

1. Click **[≡] Menu** in Palette Panel
2. Select **Presets** submenu
3. Choose from the list
4. Select **Add** (append) or **Replace** (clear and load)

---

## Custom Palettes

### Save Current Palette

1. **[≡] Menu** → **Export** → **Save Custom Palette**
2. Enter a name and optional description
3. Saved to your Palettes folder

### Palette File Location

- Windows: `%LocalAppData%\PixlPunkt\Palettes\`
- macOS: `~/Library/Application Support/PixlPunkt/Palettes/`
- Linux: `~/.local/share/PixlPunkt/Palettes/`

### Import/Export Formats

PixlPunkt supports these palette file formats:

| Format | Extension | Import | Export | Notes |
|--------|-----------|:------:|:------:|-------|
| Hex List | `.hex` | ✓ | ✓ | One `#RRGGBB` per line |
| GIMP Palette | `.gpl` | ✓ | ✓ | GIMP/Inkscape compatible |
| Microsoft PAL | `.pal` | ✓ | — | RIFF palette format |
| Adobe Color | `.aco` | ✓ | — | Photoshop swatches (RGB only) |
| PixlPunkt JSON | `.json` | — | ✓ | Native format with metadata |

### Import Palette from File

1. **[≡] Menu** → **Import** → **From File**
2. Select file in any supported format
3. Colors are loaded with Add/Replace option

### Export Palette to File

1. **[≡] Menu** → **Export** → **To File**
2. Choose format
3. Select destination

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eyedropper_16.png" width="16"> Extract Palette from Image

Pull unique colors from any image:

1. **[≡] Menu** → **Import** → **From Image**
2. Select an image file (PNG, BMP, JPG, GIF)
3. Preview shows extracted colors
4. Use **Merge near colors** slider to reduce similar colors
5. Choose **Add** or **Replace**

You can also extract from:
- **Current Layer** → Import → From Layer
- **Entire Document** → Import → From Document

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/data_sunburst_16.png" width="16"> Gradient Swatches

Create intermediate colors between two palette colors:

### Generate Gradient Swatches

1. Click the **Gradient** button <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/data_sunburst_16.png" width="16"> in the palette toolbar
2. Start color defaults to your foreground
3. End color defaults to your background
4. Adjust the number of steps
5. Preview shows the gradient
6. Click **Add** to add colors to palette

---

## Color Sorting

Organize your palette:

### Sort Options

**[≡] Menu** → **Sort By**

| Sort By | Description |
|---------|-------------|
| **Hue** | Group by color family (rainbow order) |
| **Saturation** | Gray → Vibrant |
| **Lightness** | By HSL lightness value |
| **Luminance** | By perceived brightness |
| **Red** | By red channel value |
| **Green** | By green channel value |
| **Blue** | By blue channel value |
| **Reverse** | Flip current order |

---

## Palette Operations

### Clear Palette
- **[≡] Menu** → **Clear Palette**
- Shows confirmation dialog
- Removes all colors

### Reset to Default
- **[≡] Menu** → **Reset**
- Loads the configured default palette

---

## Per-Document Palettes

Each document stores its own palette:
- Saved with the `.pxp` file
- Different projects can have different palettes
- Palette travels with the document

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `X` | Swap FG/BG |
| `Right-click` (canvas) | Pick color as foreground |

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
