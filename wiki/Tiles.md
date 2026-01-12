# Tiles

Tile-based workflows for game art and pattern creation in PixlPunkt.

---

## What Are Tiles?

Tiles are fixed-size squares that can be:
- Arranged in grids to create larger images
- Reused to build levels, maps, and environments
- Animated for game sprites
- Exported as tile sets for game engines

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/grid_16.png" width="16"> Setting Up Tiles

### Configure Tile Size

1. Go to **Canvas → Tile Settings** or open the **Tiles Panel** (`F7`)
2. Set tile dimensions:
   - **Width:** Tile width in pixels (e.g., 16)
   - **Height:** Tile height in pixels (e.g., 16)
3. Common sizes:
   - **8×8** - Retro NES-style
   - **16×16** - Standard pixel art
   - **32×32** - Detailed tiles
   - **Custom** - Any size

### Show Tile Grid

- Press `Ctrl+Shift+'` to toggle tile grid
- Or **View → Toggle Tile Grid**
- Grid lines show tile boundaries

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_edit_16.png" width="16"> Tile Stamper Tool

**Shortcut:** `Shift+T`

### Using Tile Stamper

1. Select the Tile Stamper tool
2. In the Tiles Panel, click a tile to select it
3. Click on the canvas to stamp the tile
4. Tiles snap to the grid automatically

### Tile Stamper Options

| Option | Description |
|--------|-------------|
| **Auto-Tile** | Automatically select connecting tiles |
| **Random** | Randomly pick from selected tile variants |
| **Flip H/V** | Flip tile while stamping |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_cell_edit_16.png" width="16"> Tile Modifier Tool

**Shortcut:** `Ctrl+T`

Modify existing tiles on the canvas:

| Action | Description |
|--------|-------------|
| **Click** | Select tile at position |
| **Drag** | Move tile |
| **Delete** | Remove tile |
| **Replace** | Swap with different tile |

---

## Tiles Panel (`F7`)

### Panel Layout

```
┌─────────────────────────────┐
│ Tile Set: [tileset.png ▼]   │
├─────────────────────────────┤
│ ┌───┬───┬───┬───┐          │
│ │ 0 │ 1 │ 2 │ 3 │  Tile    │
│ ├───┼───┼───┼───┤  Grid    │
│ │ 4 │ 5 │ 6 │ 7 │          │
│ └───┴───┴───┴───┘          │
├─────────────────────────────┤
│ Selected: Tile #5           │
│ Size: 16×16                 │
└─────────────────────────────┘
```

### Loading a Tile Set

1. Click **Load Tile Set** in the Tiles Panel
2. Select an image file (PNG recommended)
3. Specify tile dimensions if different from default
4. Tiles are automatically sliced from the image

### Creating Tiles from Canvas

1. Draw your tiles on the canvas
2. **Select All** (`Ctrl+A`) or select specific area
3. Right-click → **Create Tile Set from Selection**
4. Tiles are added to the Tiles Panel

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_lightning_16.png" width="16"> Tile Tessellation

Create seamless repeating patterns:

### Tessellation Window

1. Go to **Canvas → Tile Tessellation**
2. Select a tile or area
3. Preview shows how tile repeats
4. Adjust to eliminate seams

### Making Seamless Tiles

1. Draw your base tile
2. Open Tessellation preview
3. Use the **Offset View** to see edges
4. Paint to blend edges together
5. When preview shows no seams, you're done!

---

## Per-Layer Tile Mapping

Each layer can have its own tile mapping:

### Why Use Layer Mapping?

- **Background layer** - Terrain tiles
- **Collision layer** - Invisible collision tiles
- **Decoration layer** - Props and details
- **Foreground layer** - Objects in front of player

### Setting Layer Tile Map

1. Select a layer
2. In Tiles Panel, click **Set Layer Tile Map**
3. Choose or create a tile set for that layer
4. Each layer can use different tile sets

---

## Importing Tiles

### From Tiled (.tmx/.tsx)

PixlPunkt can import Tiled map editor files:

1. **File → Import → Tiled Map (.tmx)**
2. Select your `.tmx` file
3. Associated tile sets (`.tsx`) are loaded automatically
4. Each layer becomes a PixlPunkt layer with tiles

### From Other Formats

| Format | Support |
|--------|---------|
| PNG | Full - slice into tiles |
| Aseprite | Tags become tile animations |
| PyxelEdit | Full tile support |

---

## Exporting Tiles

### Export Tile Set

1. **File → Export → Tile Set**
2. Options:
   - **PNG** - Single image with all tiles
   - **Individual** - Separate file per tile
   - **JSON** - Tile data with image

### Export for Game Engines

| Engine | Recommended Format |
|--------|-------------------|
| Unity | PNG + JSON metadata |
| Godot | PNG atlas |
| GameMaker | PNG sprite strip |
| Tiled | TSX tile set |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Animated Tiles

Tiles can be animated! See [[Tile Animation|Tile-Animation]] for:

- Creating tile animation reels
- Setting frame timing
- Exporting animated tiles
- Using animated tiles in maps

---

## Tips for Tile Art

### Consistency
- Use consistent lighting direction
- Keep color palette unified
- Match detail level across tiles

### Variety
- Create multiple variants of common tiles
- Use random selection for natural look
- Mix and match for interest

### Edge Matching
- Plan tile edges carefully
- Use tessellation preview often
- Create transition tiles for different terrain types

### Performance
- Reuse tiles when possible
- Smaller tile sets = smaller game size
- Use tile flipping to multiply apparent variety

---

## Auto-Tiling (Coming Soon)

Automatic tile selection based on neighbors:

```
┌───┬───┬───┐
│ ╔═╤═╤═╗   │  Auto-tile automatically
│ ║ │ │ ║   │  picks corner, edge, and
│ ╟─┼─┼─╢   │  center pieces based on
│ ║ │ │ ║   │  surrounding tiles.
│ ╚═╧═╧═╝   │
└───┴───┴───┘
```

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Shift+T` | Tile Stamper tool |
| `Ctrl+T` | Tile Modifier tool |
| `Ctrl+Shift+'` | Toggle tile grid |
| `F7` | Toggle Tiles Panel |

---

## See Also

- [[Tile Animation|Tile-Animation]] - Animated tiles
- [[Game Art|Game-Art]] - Game development workflow
- [[Canvas Animation|Canvas-Animation]] - Frame animation
- [[File Formats|Formats]] - Export options
