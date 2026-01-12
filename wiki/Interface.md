# Interface Overview

A complete guide to the PixlPunkt user interface.

---

## Main Window Layout

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/screenshots/PixlPunktUi.png" width="16">

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/navigation_16.png" width="16"> Menu Bar

### File
| Menu Item | Shortcut | Description |
|-----------|----------|-------------|
| New | `Ctrl+N` | Create a new canvas |
| Open | `Ctrl+O` | Open an existing file |
| Save | `Ctrl+S` | Save current document |
| Save As | `Ctrl+Shift+S` | Save with a new name |
| Export | `Ctrl+E` | Export as image/animation |
| Recent Files | | Quick access to recent documents |

### Edit
| Menu Item | Shortcut | Description |
|-----------|----------|-------------|
| Undo | `Ctrl+Z` | Undo last action |
| Redo | `Ctrl+Y` | Redo undone action |
| Cut | `Ctrl+X` | Cut selection |
| Copy | `Ctrl+C` | Copy selection |
| Paste | `Ctrl+V` | Paste from clipboard |
| Select All | `Ctrl+A` | Select entire canvas |
| Deselect | `Ctrl+D` | Clear selection |

### View
| Menu Item | Shortcut | Description |
|-----------|----------|-------------|
| Zoom In | `Ctrl++` | Increase zoom |
| Zoom Out | `Ctrl+-` | Decrease zoom |
| Fit to Screen | `Ctrl+0` | Fit canvas in view |
| Actual Size | `Ctrl+1` | 1:1 pixel view |
| Toggle Grid | `Ctrl+'` | Show/hide pixel grid |
| Toggle Rulers | `Ctrl+R` | Show/hide rulers |

### Canvas
| Menu Item | Description |
|-----------|-------------|
| Resize Canvas | Change canvas dimensions |
| Crop to Selection | Crop canvas to current selection |
| Flip Horizontal | Mirror canvas horizontally |
| Flip Vertical | Mirror canvas vertically |
| Rotate | Rotate canvas 90°, 180°, 270° |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> Tool Rail

The vertical toolbar on the left contains all drawing tools.

### Tool Categories

| Icon | Tool | Shortcut | Category |
|:----:|------|----------|----------|
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> | Brush | `B` | Painting |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eraser_16.png" width="16"> | Eraser | `E` | Painting |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/paint_bucket_16.png" width="16"> | Fill | `G` | Painting |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/data_sunburst_16.png" width="16"> | Gradient Fill | `Shift+G` | Painting |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/select_object_16.png" width="16"> | Rectangle Select | `M` | Selection |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wand_16.png" width="16"> | Magic Wand | `W` | Selection |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lasso_16.png" width="16"> | Lasso | `L` | Selection |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_16.png" width="16"> | Rectangle | `Ctrl+U` | Shapes |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/circle_16.png" width="16"> | Ellipse | `O` | Shapes |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eyedropper_16.png" width="16"> | Dropper | `I` | Utility |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/zoom_in_16.png" width="16"> | Zoom | `Z` | Utility |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/hand_left_16.png" width="16"> | Pan | `H` | Utility |

See [[Tools]] for detailed documentation on each tool.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/options_16.png" width="16"> Tool Options Bar

Located below the canvas, shows options for the currently selected tool.

### Common Options

| Option | Description |
|--------|-------------|
| **Size** | Brush/tool size in pixels |
| **Opacity** | Transparency (0-255) |
| **Shape** | Brush shape (Circle, Square, Custom) |
| **Mode** | Blend mode or tool mode |

### Tool-Specific Options
Each tool has unique options. Select a tool to see its options appear.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/layer_16.png" width="16"> Panels

### Layers Panel (`F5`)

Manage your document's layers:

| Element | Description |
|---------|-------------|
| Layer List | All layers, top to bottom |
| Visibility <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_16.png" width="16"> | Toggle layer visibility |
| Lock <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lock_closed_16.png" width="16"> | Lock layer from editing |
| Opacity Slider | Layer transparency |
| Blend Mode | How layer blends with layers below |
| Add Layer <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> | Create new layer |
| Delete Layer <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/delete_16.png" width="16"> | Remove selected layer |
| Folder <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_16.png" width="16"> | Create layer group |

### Palette Panel (`F6`)

Color management:

| Element | Description |
|---------|-------------|
| Foreground/Background | Current colors (click to edit) |
| Swap (`X`) | Swap FG/BG colors |
| Palette Grid | Available colors |
| Add Color | Add current FG to palette |
| Palette Menu | Load, save, import palettes |

### Tiles Panel (`F7`)

For tile-based workflows:

| Element | Description |
|---------|-------------|
| Tile Set | Current tile set preview |
| Tile Grid | All available tiles |
| Tile Size | Configure tile dimensions |

### History Panel (`F8`)

Undo/redo history:

| Element | Description |
|---------|-------------|
| History List | All recorded actions |
| Click to Jump | Click any state to revert |
| Undo/Redo Buttons | Quick undo/redo |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/video_clip_16.png" width="16"> Timeline Panel (`T`)

Animation timeline for frame-by-frame animation.

### Timeline Elements

| Element | Description |
|---------|-------------|
| Frames | Individual animation frames |
| Tracks | Layer tracks for keyframes |
| Playhead | Current frame indicator |
| Play/Pause | Preview animation |
| FPS | Animation speed |
| Onion Skin | Show previous/next frames |

See [[Canvas Animation|Canvas-Animation]] for detailed animation documentation.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/image_16.png" width="16"> Canvas Area

The main drawing area.

### Navigation

| Action | Method |
|--------|--------|
| Pan | `Space+Drag` or Middle Mouse |
| Zoom | `Ctrl+Scroll` or `Z` tool |
| Fit to Screen | `Ctrl+0` |
| Actual Size | `Ctrl+1` |

### Guides & Rulers

- **Rulers** - Drag from rulers to create guides
- **Guides** - Snap points for alignment
- **Grid** - Pixel grid overlay at high zoom

### Status Bar

Shows at the bottom:
- Current mouse position (X, Y)
- Canvas dimensions
- Zoom level
- Current tool

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_16.png" width="16"> Color Picker

Click the foreground or background color swatch to open:

| Element | Description |
|---------|-------------|
| HSL Square | Saturation/Lightness picker |
| Hue Slider | Select hue |
| Ladder Controls | Fine-tune HSL values |
| Hex Input | Enter exact hex color |
| Recent Colors | Previously used colors |

See [[Color Picker|Color-Picker]] for more details.

---

## Customizing the Interface

### Panel Layout
- **Drag** panel headers to rearrange
- **Dock** panels to edges
- **Float** panels as separate windows
- **Collapse** panels to icons

### Themes
Go to **Edit → Preferences → Appearance**:
- Light theme
- Dark theme (default)
- Custom transparency stripe colors

### Keyboard Shortcuts
Customize in **Edit → Preferences → Keyboard**

See [[Settings]] for all customization options.

---

## See Also

- [[Quick Start|Quick-Start]] - Getting started
- [[Tools]] - All tools explained
- [[Shortcuts]] - Keyboard reference
- [[Settings]] - Customize PixlPunkt
