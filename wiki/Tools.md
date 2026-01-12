# Tools

PixlPunkt includes a comprehensive set of drawing, selection, and editing tools. This page covers every tool in detail.

## Tool Shortcuts Quick Reference

| Key | Tool | Icon | Category |
|-----|------|:----:|----------|
| `B` | Brush | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> | Painting |
| `E` | Eraser | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eraser_16.png" width="16"> | Painting |
| `G` | Fill (Bucket) | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/paint_bucket_16.png" width="16"> | Painting |
| `D` | Gradient Brush | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_line_16.png" width="16"> | Painting |
| `Shift+G` | **Gradient Fill** | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/data_sunburst_16.png" width="16"> | Painting |
| `R` | Color Replacer | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/pen_sync_16.png" width="16"> | Painting |
| `U` | Blur | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/drop_16.png" width="16"> | Effects |
| `J` | Jumble | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/tap_double_16.png" width="16"> | Effects |
| `Shift+S` | Smudge | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/hand_draw_16.png" width="16"> | Effects |
| `M` | Rectangle Select | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/select_object_16.png" width="16"> | Selection |
| `W` | Magic Wand | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wand_16.png" width="16"> | Selection |
| `L` | Lasso | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lasso_16.png" width="16"> | Selection |
| `Shift+P` | Paint Selection | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/border_none_16.png" width="16"> | Selection |
| `I` | Dropper | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eyedropper_16.png" width="16"> | Utility |
| `H` | Pan | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/hand_left_16.png" width="16"> | Utility |
| `Z` | Zoom | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/zoom_in_16.png" width="16"> | Utility |
| `Ctrl+U` | Rectangle | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_16.png" width="16"> | Shapes |
| `O` | Ellipse | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/circle_16.png" width="16"> | Shapes |
| `Shift+A` | Tile Stamper | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_edit_16.png" width="16"> | Tiles |
| `Ctrl+T` | Tile Modifier | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_cell_edit_16.png" width="16"> | Tiles |

---

## Painting Tools

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_20.png" width="20"> Brush (B)

Standard painting brush for applying the foreground color.

**Options:**
| Option | Description |
|--------|-------------|
| **Brush** | Select brush shape (<img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/circle_16.png" width="16"> Circle, <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_16.png" width="16"> Square) or custom brush |
| **Size** | Brush diameter (use `[` and `]` to adjust) |
| **Opacity** | Transparency of brush strokes (0-255) |
| **Density** | How filled the brush stroke is (0-255) |
| **Pixel Perfect** | Eliminates diagonal stair-stepping for 1px brushes |

**Modifiers:**
- `Shift + Drag` - Draw straight lines
- `Ctrl + Shift + Drag` - Constrain to isometric angles (0�, 22.5�, 45�, 67.5�, 90�)

**Custom Brushes:** Supports custom `.pbx` brush files loaded from your brushes folder.

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eraser_20.png" width="20"> Eraser (E)

Removes pixels by making them transparent.

**Options:**
| Option | Description |
|--------|-------------|
| **Brush** | Select eraser shape (<img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/circle_16.png" width="16"> Circle, <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_16.png" width="16"> Square) or custom brush |
| **Size** | Eraser diameter |
| **Opacity** | Eraser transparency (0-255) |
| **Density** | Eraser hardness (0-255) |
| **Erase to transparent** | When enabled, erases to full transparency; when disabled, erases to background color |

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/paint_bucket_20.png" width="20"> Fill (G)

Flood fills a contiguous area with the foreground color.

**Options:**
| Option | Description |
|--------|-------------|
| **Tolerance** | Color matching threshold (0-255) |
| **Contiguous** | Fill only connected pixels vs. all matching pixels globally |

**Tip:** Hold `Shift` while clicking to fill all matching colors globally, regardless of the Contiguous setting.

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_line_20.png" width="20"> Gradient Brush (D)

Cycles through loaded gradient colors based on the pixel color under the brush.

**Usage:**
1. Load colors into the gradient (from palette or create custom)
2. Paint over existing pixels
3. The brush will shift colors along the gradient, creating smooth transitions

**Options:**
| Option | Description |
|--------|-------------|
| **Shape** | Round or Square brush tip |
| **Size** | Brush diameter |
| **Density** | Brush hardness (0-255) |
| **Ignore Alpha** | Skip transparent pixels |
| **Loop** | Cycle back to the start after reaching the end |

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/pen_sync_20.png" width="20"> Color Replacer (R)

Replaces the background color with the foreground color wherever you paint.

**Options:**
| Option | Description |
|--------|-------------|
| **Brush** | Select replacer shape (<img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/circle_16.png" width="16"> Circle, <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_16.png" width="16"> Square) or custom brush |
| **Size** | Brush diameter |
| **Opacity** | Transparency of replaced pixels (0-255) |
| **Density** | Brush hardness (0-255) |
| **Ignore Alpha** | Replace regardless of transparency |

---

## Selection Tools

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/select_object_20.png" width="20"> Rectangle Select (M)

Click and drag to create a rectangular selection.

**Modifiers:**
- `Shift + Drag` - Add to existing selection
- `Alt + Drag` - Subtract from selection
- `Shift` while dragging - Constrain to square

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wand_20.png" width="20"> Magic Wand (W)

Select all pixels of similar color.

**Options:**

| Option | Description |
|--------|-------------|
| **Tolerance** | How similar colors must be (0 = exact, 255 = all) |
| **Contiguous** | Select only connected pixels vs. all matching globally |
| **Use Alpha** | Include transparency in color comparison |
| **Diagonal** | Include diagonal neighbors (8-way) vs. orthogonal only (4-way) |

**Modifiers:**
- `Shift + Click` - Add to selection
- `Alt + Click` - Subtract from selection

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lasso_20.png" width="20"> Lasso (L)

Draw a freeform selection boundary.

Click points to create a polygon selection. The shape closes automatically when you complete it.

**Options:**

| Option | Description |
|--------|-------------|
| **Auto close** | Automatically close when clicking near start point |
| **Close distance** | Threshold in pixels for auto-close (1-50) |

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/border_none_20.png" width="20"> Paint Selection (Shift+P)

Brush-based selection mode - paint to add, right-click to subtract.

**Options:**

| Option | Description |
|--------|-------------|
| **Shape** | Brush shape (<img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/circle_16.png" width="16"> Circle, <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_16.png" width="16"> Square) |
| **Size** | Brush diameter |

---

### Selection Operations

Once you have a selection:

| Action | How |
|--------|-----|
| **Move** | Drag the selection |
| **Nudge 1px** | Arrow keys |
| **Nudge 10px** | Shift + Arrow keys |
| **Scale** | Use handles or set percentage |
| **Rotate** | Enter degrees or drag rotation handle |
| **Flip** | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/flip_horizontal_16.png" width="16"> Horizontal or <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/flip_vertical_16.png" width="16"> Vertical options |
| **Apply** | `Enter` - commit changes |
| **Cancel** | `Esc` - revert changes |

---

## Effect Tools

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/drop_20.png" width="20"> Blur (U)

Softens pixels by averaging neighboring colors.

**Options:**

| Option | Description |
|--------|-------------|
| **Brush** | Blur brush shape |
| **Size** | Brush diameter |
| **Density** | Brush hardness (0-255) |
| **Strength** | Intensity of blur (0-100%) |

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/tap_double_20.png" width="20"> Jumble (J)

Randomly rearranges pixels within the brush area for a scatter effect.

**Options:**

| Option | Description |
|--------|-------------|
| **Brush** | Brush shape |
| **Size** | Brush diameter |
| **Strength** | Displacement amount (0-100%) |
| **Falloff** | Intensity falloff from center (0.2-4.0) |
| **Locality** | How far pixels can move (0-100%) |
| **Include Transparent** | Move transparent pixels too |

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/hand_draw_20.png" width="20"> Smudge (Shift+S)

Pushes and blends colors in the direction you paint.

**Options:**

| Option | Description |
|--------|-------------|
| **Brush** | Brush shape |
| **Size** | Brush diameter |
| **Density** | Brush hardness (0-255) |
| **Strength** | How much paint is picked up (0-100%) |
| **Falloff** | How quickly effect diminishes (0.2-4.0) |
| **Hard Edge** | Sharp vs. soft smudging |
| **Blend on transparent** | Blend onto transparent pixels |

---

## Shape Tools

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_20.png" width="20"> Rectangle (Ctrl+U)

Draw filled or outlined rectangles.

**Options:**

| Option | Description |
|--------|-------------|
| **Shape** | Brush shape for stroke |
| **Stroke** | Line thickness (1-128) |
| **Opacity** | Shape transparency (0-255) |
| **Density** | Stroke hardness (0-255) |
| **Filled** | Solid fill vs. outline only |

**Modifiers:**

- `Shift` - Constrain to square
- `Ctrl` - Draw from center

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/circle_20.png" width="20"> Ellipse (O)

Draw filled or outlined ellipses/circles.

**Options:** Same as Rectangle.

**Modifiers:**
- `Shift` - Constrain to circle
- `Ctrl` - Draw from center

---

## Tile Tools

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_edit_20.png" width="20"> Tile Stamper (Shift+A)

Places tiles from your tileset onto the canvas.

**Actions:**

| Input | Action |
|-------|--------|
| `Left Click` | Stamp selected tile |
| `Ctrl + Click` | Create new tile from canvas region |
| `Shift + Click` | Erase tile from canvas |
| `Right Click` | Remove tile mapping at position |
| `Shift + Right Click` | Duplicate selected tile |

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_cell_edit_20.png" width="20"> Tile Modifier (Ctrl+T)

Offsets and transforms tile content within boundaries.

**Options:**

| Option | Description |
|--------|-------------|
| **Offset X/Y** | Shift content within tile |
| **Wrap** | Wrap around when offsetting |

---

## Utility Tools

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eyedropper_20.png" width="20"> Dropper (I) / Right-Click

Sample colors from the canvas.

- `Right Click` anywhere - Pick foreground color
- `Shift + Right Click` - Pick background color

**Pro tip:** Right-click works from ANY tool - you never need to switch to dropper!

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/zoom_in_20.png" width="20"> Zoom (Z)

- `Left Click` - Zoom in
- `Right Click` - Zoom out
- `Scroll Wheel` - Zoom in/out

---

### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/hand_left_20.png" width="20"> Pan (H)

- `Space + Drag` - Temporarily pan (works from any tool!)
- `Middle Mouse Drag` - Pan

---

## See Also

- [[Gradient Fill|Gradient-Fill]] - Deep dive on the gradient fill tool
- [[Brushes]] - Custom brush creation and management
- [[Shortcuts]] - Complete keyboard reference
