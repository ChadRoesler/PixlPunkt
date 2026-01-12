# Brushes

Everything about brush tools, custom brushes, and brush settings in PixlPunkt.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> Built-in Brush Shapes

### Circle
The default brush shape. Creates round strokes.
- Soft edges at larger sizes (with density < 100%)
- Perfect for organic drawing

### Square
Creates pixel-perfect square strokes.
- Hard edges always
- Great for architectural/geometric art
- Aligns perfectly to pixel grid

### Custom
Use any image as a brush tip.
- Import from `.mrk` files
- Create your own patterns
- Supports transparency

---

## Brush Settings

### Size
**Range:** 1-128 pixels

| Shortcut | Action |
|----------|--------|
| `[` | Decrease size by 1 |
| `]` | Increase size by 1 |
| `Shift+[` | Decrease size by 10 |
| `Shift+]` | Increase size by 10 |

### Opacity
**Range:** 0-255 (0-100%)

Controls how transparent the brush stroke is.
- **255** = Fully opaque
- **128** = 50% transparent
- **0** = Invisible

### Density
**Range:** 0-255 (0-100%)

Controls the brush falloff/hardness.
- **255** = Hard edge (no falloff)
- **128** = Soft edge with gradual falloff
- **0** = Maximum softness

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/star_16.png" width="16"> Custom Brushes

### Creating a Custom Brush

1. Create a new 16×16 canvas (**File → New**)
2. Draw your brush pattern
3. Go to **File → Export → Brush**
4. Enter brush name and author
5. Set the pivot point (brush center)
6. Save the `.mrk` file

### Brush File Format (.mrk)

Custom brushes are saved as `.mrk` (PixlPunkt Mark) files:
- Location: `%LocalAppData%\PixlPunkt\Brushes\`
- Format: Binary with 16×16 1-bit mask
- Includes: Author, name, pivot point, icon preview

### Installing Custom Brushes

1. Download or create a `.mrk` file
2. Copy to your Brushes folder:
   - Windows: `%LocalAppData%\PixlPunkt\Brushes\`
   - macOS: `~/Library/Application Support/PixlPunkt/Brushes/`
   - Linux: `~/.local/share/PixlPunkt/Brushes/`
3. Restart PixlPunkt or click **View → Refresh Brushes**

### Exporting Brushes

1. Create a 16×16 canvas with your brush pattern
2. Go to **File → Export → Brush**
3. Configure name, author, and pivot point
4. Save the `.mrk` file
5. Share your brush!

---

## Brush Behavior

### Pixel-Perfect Mode

Enable for clean single-pixel lines:
- Removes diagonal "stair-stepping"
- Creates smooth 1px lines
- Toggle in tool options

### Shift+Click Line Drawing

1. Click to place starting point
2. Hold `Shift` and click endpoint
3. Draws a straight line between points

### Constrained Angles

- Hold `Ctrl+Shift` while drawing
- Constrains to 15° angle increments
- Great for isometric art

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eraser_16.png" width="16"> Eraser Brush

The eraser uses the same brush system:
- Same shapes (Circle, Square, Custom)
- Same size controls
- Same density settings

**Shortcut:** `E`

### Eraser Options

| Option | Description |
|--------|-------------|
| Erase to transparent | When enabled, erases to full transparency |
| Erase to background | When disabled, erases to background color |

---

## Brush Tools Overview

| Tool | Icon | Shortcut | Description |
|------|:----:|----------|-------------|
| Brush | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> | `B` | Standard drawing brush |
| Eraser | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eraser_16.png" width="16"> | `E` | Erase pixels |
| Color Replacer | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/pen_sync_16.png" width="16"> | `R` | Replace one color with another |
| Gradient Brush | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_line_16.png" width="16"> | `O` | Paint with gradient |

---

## Tips & Tricks

### Quick Color Picking
- **Right-click** anywhere to pick that color
- Works with any brush tool active

### Brush Preview
- Brush outline shows on canvas
- Displays exact pixels that will be affected
- Orange outline = current brush shape

### Swap Foreground/Background
- Press `X` to swap colors
- Useful for two-color shading

### Lock Transparency
- In layer panel, click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lock_closed_16.png" width="16">
- Brush only affects existing pixels
- Great for shading without going outside lines

---

## Organizing Brushes

### Brush Files
Brushes are stored as individual `.mrk` files:
```
Brushes/
├── PixlPunkt.Star.mrk
├── PixlPunkt.Heart.mrk
├── Custom.MyBrush.mrk
└── Artist.SpecialBrush.mrk
```

Brush files follow the naming convention: `Author.BrushName.mrk`

---

## See Also

- [[Tools]] - All tool documentation
- [[Gradient Fill|Gradient-Fill]] - Gradient painting
- [[Layers]] - Layer management
- [[Quick Start|Quick-Start]] - Getting started
