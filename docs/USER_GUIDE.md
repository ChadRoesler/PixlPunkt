# PixlPunkt User Guide

**PixlPunkt** is a modern pixel art editor for Windows, designed for creating sprites, tilesets, and pixel-based artwork. This guide covers all the features and tools available in the application.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Interface Overview](#interface-overview)
3. [Canvas & Documents](#canvas--documents)
4. [Tools](#tools)
   - [Painting Tools](#painting-tools)
   - [Selection Tools](#selection-tools)
   - [Effect Tools](#effect-tools)
   - [Shape Tools](#shape-tools)
   - [Tile Tools](#tile-tools)
   - [Utility Tools](#utility-tools)
5. [Gradient Fill Tool](#gradient-fill-tool)
6. [Layers](#layers)
7. [Layer Masks](#layer-masks)
8. [Layer Effects](#layer-effects)
9. [Reference Layers](#reference-layers)
10. [Palette](#palette)
11. [Tiles](#tiles)
12. [Animation](#animation)
13. [Stage (Camera System)](#stage-camera-system)
14. [Audio Reference Tracks](#audio-reference-tracks)
15. [Keyboard Shortcuts](#keyboard-shortcuts)
16. [Settings](#settings)
17. [File Formats](#file-formats)
18. [Advanced Topics](#advanced-topics)
    - [Dithering Deep Dive](#dithering-deep-dive)
    - [Animation Workflow](#animation-workflow)
    - [Tile-Based Game Art](#tile-based-game-art)
19. [Tips & Tricks](#tips--tricks)

---

## Getting Started

### Creating a New Canvas

1. Go to **File → New Canvas** or press `Ctrl+N`
2. In the New Canvas dialog:
   - Enter a **Document Name**
   - Set **Tile Size** (width × height in pixels) - typically 8×8, 16×16, or 32×32
   - Set **Canvas Size** (width × height in tiles)
   - The total pixel dimensions are calculated automatically
3. Click **Create** to open your new canvas

### Opening Existing Documents

- **File → Open** (`Ctrl+O`) to open `.pxp` (native) or import other formats
- PixlPunkt supports importing: `.pyxel`, `.ase/.aseprite`, `.ico`, `.cur`, `.tmx`, `.tsx`

### Saving Your Work

- **File → Save** (`Ctrl+S`) saves in native `.pxp` format
- **File → Save As** (`Ctrl+Shift+S`) to save with a new name
- **File → Export** to export as PNG, GIF, BMP, JPEG, or TIFF

---

## Interface Overview

```
┌─────────────────────────────────────────────────────────────────┐
│ File  Edit  View  Select  Tiles  Palette  Help                  │  ← Menu Bar
├─────────────────────────────────────────────────────────────────┤
│ [Tool] │  Tool Options Bar                                      │  ← Tool Options
├────────┼────────────────────────────────────────────────────────┤
│        │ Canvas Tab 1 │ Canvas Tab 2 │                │ Preview │
│  Tool  │ ┌───────────────────────────┐                ├─────────┤
│  Rail  │ │                           │                │ Palette │
│        │ │      Main Canvas Area     │                ├─────────┤
│  (B,E, │ │                           │                │ Layers  │
│  F,S,  │ │                           │                ├─────────┤
│  etc.) │ └───────────────────────────┘                │ Tiles   │
│        ├──────────────────────────────────────────────┼─────────┤
│        │                Animation Timeline Panel      │ History │
└────────┴──────────────────────────────────────────────┴─────────┘
```

### Main Panels

| Panel | Description |
|-------|-------------|
| **Tool Rail** | Left sidebar with all drawing and editing tools |
| **Tool Options** | Top bar showing options for the currently selected tool |
| **Canvas Area** | Main editing area with tabbed documents |
| **Preview Panel** | Live preview of your artwork at different zoom levels |
| **Palette Panel** | Color swatches for quick color selection |
| **Layers Panel** | Layer management with visibility, locking, and effects |
| **Tiles Panel** | Tile management for tileset-based editing |
| **History Panel** | Undo/Redo history of actions |
| **Animation Panel** | Timeline for canvas and tile-based animation |

---

## Canvas & Documents

### Canvas Navigation

| Action | Input |
|--------|-------|
| Pan | Hold `Space` + drag, or Middle Mouse drag |
| Zoom In | `Ctrl++` or Mouse Wheel Up |
| Zoom Out | `Ctrl+-` or Mouse Wheel Down |
| Fit to Screen | `Ctrl+0` |
| Actual Size (1:1) | `Ctrl+1` |

### View Options (View Menu)

- **Toggle Pixel Grid** - Show/hide the pixel-level grid
- **Toggle Tile Grid** - Show/hide the tile boundaries
- **Toggle Tile Mappings** - Show tile indices on the canvas
- **Toggle Rulers** - Show/hide measurement rulers
- **Toggle Guides** - Show/hide guide lines
- **Snap to Guides** - Enable snapping when moving selections

### Canvas Resize

1. Go to **Edit → Canvas Resize**
2. Adjust the tile dimensions
3. Choose an **Anchor Point** to determine where existing content is positioned
4. Click **Apply**

---

## Tools

### Tool Shortcuts Quick Reference

| Key | Tool | Category |
|-----|------|----------|
| `B` | Brush | Painting |
| `E` | Eraser | Painting |
| `G` | Fill (Bucket) | Painting |
| `D` | Gradient Brush | Painting |
| `Shift+G` | **Gradient Fill** | Painting |
| `R` | Color Replacer | Painting |
| `U` | Blur | Effects |
| `J` | Jumble | Effects |
| `Shift+S` | Smudge | Effects |
| `M` | Rectangle Select | Selection |
| `W` | Magic Wand | Selection |
| `L` | Lasso | Selection |
| `Shift+P` | Paint Selection | Selection |
| `I` | Dropper | Utility |
| `H` | Pan | Utility |
| `Z` | Zoom | Utility |
| `Ctrl+U` | Rectangle | Shapes |
| `O` | Ellipse | Shapes |
| `Shift+A` | Tile Stamper | Tiles |
| `Ctrl+T` | Tile Modifier | Tiles |

---

### Painting Tools

#### Brush (B)
Standard painting brush for applying the foreground color.

**Options:**
- **Brush** - Select brush shape (Circle, Square) or custom brush
- **Size** - Brush diameter (use `[` and `]` to adjust)
- **Opacity** - Transparency of brush strokes (0-255)
- **Density** - How filled the brush stroke is (0-255)
- **Pixel Perfect** - Eliminates diagonal stair-stepping for 1px brushes

**Modifiers:**
- `Shift + Drag` - Draw straight lines
- `Ctrl + Shift + Drag` - Constrain to isometric angles (0°, 22.5°, 45°, 67.5°, 90°)

**Custom Brushes:** Supports custom `.pbx` brush files loaded from your brushes folder.

#### Eraser (E)
Removes pixels by making them transparent.

**Options:**
- **Brush** - Select eraser shape (Circle, Square) or custom brush
- **Size** - Eraser diameter
- **Opacity** - Eraser transparency (0-255)
- **Density** - Eraser hardness (0-255)
- **Erase to transparent** - When enabled, erases to full transparency; when disabled, erases to background color

**Custom Brushes:** Supports custom `.pbx` brush files.

#### Fill (G)
Flood fills a contiguous area with the foreground color.

**Options:**
- **Tolerance** - Color matching threshold (0-255)
- **Contiguous** - Fill only connected pixels vs. all matching pixels

#### Gradient Brush (D)
Cycles through loaded gradient colors based on the pixel color under the brush.

**Usage:**
1. Load colors into the gradient (from palette or create custom)
2. Paint over existing pixels
3. The brush will shift colors along the gradient, creating smooth transitions

**Options:**
- **Shape** - Round or Square brush tip
- **Size** - Brush diameter
- **Density** - Brush hardness (0-255)
- **Ignore Alpha** - Skip transparent pixels
- **Loop** - Cycle back to the start after reaching the end

#### Color Replacer (R)
Replaces the background color with the foreground color wherever you paint.

**Options:**
- **Brush** - Select replacer shape (Circle, Square) or custom brush
- **Size** - Brush diameter
- **Opacity** - Transparency of replaced pixels (0-255)
- **Density** - Brush hardness (0-255)
- **Ignore Alpha** - Replace regardless of transparency and matches transparency

**Custom Brushes:** Supports custom `.pbx` brush files.

---

### Selection Tools

#### Rectangle Select (M)
Click and drag to create a rectangular selection.

**Modifiers:**
- `Shift + Drag` - Add to existing selection
- `Alt + Drag` - Subtract from selection
- `Shift` while dragging - Constrain to square

#### Magic Wand (W)
Select all pixels of similar color.

**Options:**
- **Tolerance** - How similar colors must be to be selected (0 = exact match, 255 = all colors)
- **Contiguous** - Select only connected pixels (flood-fill) vs. all matching pixels globally
- **Use Alpha** - Include transparency in color comparison
- **Diagonal** - Include diagonal neighbors (8-way connectivity) vs. orthogonal only (4-way)

**Modifiers:**
- `Shift + Click` - Add to selection
- `Alt + Click` - Subtract from selection

#### Lasso (L)
Draw a freeform selection boundary.

Click points to create a polygon selection. The shape closes automatically when you complete it.

**Options:**
- **Auto close** - Automatically close polygon when clicking near the start point
- **Close distance** - Distance threshold (in pixels) for auto-close detection (1-50)

#### Paint Selection (Shift+P)
Brush-based selection mode - paint to add to selection, right-click to subtract.

**Options:**
- **Shape** - Brush shape (Circle, Square)
- **Size** - Brush diameter

### Selection Operations

Once you have a selection:
- **Move** - Drag the selection to reposition
- **Arrow Keys** - Nudge by 1 pixel
- **Shift + Arrow Keys** - Nudge by 10 pixels
- **Scale** - Use handles or set exact percentage
- **Rotate** - Enter degrees or drag rotation handle
- **Flip** - Horizontal or Vertical flip options
- **Apply** (`Enter`) - Commit the transformation
- **Cancel** (`Esc`) - Revert changes

---

### Effect Tools

#### Blur (U)
Softens pixels by averaging neighboring colors.

**Options:**
- **Brush** - Select blur brush shape (Circle, Square) or custom brush
- **Size** - Blur brush diameter
- **Density** - Brush hardness (0-255)
- **Strength** - Intensity of the blur effect (0-100%)

**Custom Brushes:** Supports custom `.pbx` brush files.

#### Jumble (J)
Randomly rearranges pixels within the brush area for a scatter effect.

**Options:**
- **Brush** - Select jumble brush shape (Circle, Square) or custom brush
- **Size** - Brush diameter
- **Strength** - How much displacement (0-100%)
- **Falloff** - Intensity falloff from brush center (0.2-4.0)
- **Locality** - How far pixels can move (0-100%)
- **Include Transparent** - Whether to move transparent pixels

**Custom Brushes:** Supports custom `.pbx` brush files.

#### Smudge (Shift+S)
Pushes and blends colors in the direction you paint.

**Options:**
- **Brush** - Select smudge brush shape (Circle, Square) or custom brush
- **Size** - Brush diameter
- **Density** - Brush hardness (0-255)
- **Strength** - How much paint is picked up and moved (0-100%)
- **Falloff** - How quickly the effect diminishes (0.2-4.0)
- **Hard Edge** - Sharp vs. soft smudging
- **Blend on transparent** - Whether to blend colors onto transparent pixels

**Custom Brushes:** Supports custom `.pbx` brush files.

---

### Shape Tools

#### Rectangle (Ctrl+U)
Draw filled or outlined rectangles.

**Options:**
- **Shape** - Brush shape for stroke (Circle, Square)
- **Stroke** - Line thickness for outlines (1-128)
- **Opacity** - Shape transparency (0-255)
- **Density** - Stroke hardness (0-255)
- **Filled** - Solid fill vs. outline only

**Modifiers:**
- `Shift` - Constrain to square
- `Ctrl` - Draw from center

#### Ellipse (O)
Draw filled or outlined ellipses/circles.

**Options:**
- **Shape** - Brush shape for stroke (Circle, Square)
- **Stroke** - Line thickness for outlines (1-128)
- **Opacity** - Shape transparency (0-255)
- **Density** - Stroke hardness (0-255)
- **Filled** - Solid fill vs. outline only

**Modifiers:**
- `Shift` - Constrain to circle
- `Ctrl` - Draw from center

---

### Tile Tools

#### Tile Stamper (Shift+A)
Places tiles from your tileset onto the canvas.

**Actions:**
- `Left Click` - Stamp the selected tile
- `Ctrl + Click` - Create a new tile from the canvas region
- `Shift + Click` - Erase tile from the canvas
- `Right Click` - Remove tile mapping at position
- `Shift + Right Click` - Duplicate selected tile

#### Tile Modifier (Ctrl+T)
Offsets and transforms tile content within boundaries.

**Options:**
- **Offset X/Y** - Shift content within the tile
- **Wrap** - Wrap around when offsetting

---

### Utility Tools

#### Dropper (I) / Right-Click
Sample colors from the canvas.

- `Right Click` anywhere - Pick foreground color
- `Shift + Right Click` - Pick background color

#### Zoom (Z)
- `Left Click` - Zoom in
- `Right Click` - Zoom out
- `Scroll Wheel` - Zoom in/out

#### Pan (H)
- `Space + Drag` - Temporarily pan
- `Middle Mouse Drag` - Pan

---

## Gradient Fill Tool

The **Gradient Fill** tool (`Shift+G`) is a powerful feature for creating smooth color transitions with pixel-art-friendly dithering. This is perfect for skies, backgrounds, lighting effects, and stylized shading.

### Basic Usage

1. Select the **Gradient Fill** tool (`Shift+G`)
2. Choose your **Gradient Type** (Linear, Radial, Angular, Diamond)
3. Select your **Color Mode** or click **Custom...** to create a custom gradient
4. Click and drag on the canvas:
   - **Start point** = where the gradient begins
   - **End point** = where the gradient ends
5. Release to apply the gradient

### Gradient Types

| Type | Description | Best For |
|------|-------------|----------|
| **Linear** | Straight transition from start to end | Skies, horizons, flat surfaces |
| **Radial** | Circular gradient from center outward | Light sources, orbs, explosions |
| **Angular** | Rotates around center point | Conic effects, color wheels |
| **Diamond** | Diamond/rhombus shaped gradient | Stylized lighting, gem effects |

### Color Modes

| Mode | Description |
|------|-------------|
| **White → Black** | Simple grayscale gradient |
| **Black → White** | Inverted grayscale |
| **FG → BG** | Uses your current foreground and background colors |
| **BG → FG** | Reversed foreground/background |
| **Custom...** | Opens the gradient editor for multi-color gradients |

### The Gradient Preview Strip

The tool options bar includes a **live gradient preview strip** that shows exactly what colors will be used:

- Updates in real-time as you change settings
- Shows the effect of the **Reverse** toggle
- In **Custom** mode, click the preview to open the gradient editor

### Custom Gradient Editor

Click **Custom...** in the color mode dropdown to open the gradient editor window:

1. **Start Color** - Click to select or use dropper
2. **End Color** - Click to select or use dropper
3. **Steps** - Number of colors in the gradient
4. **Preview** - Shows all generated colors
5. **Add to Palette** - Saves colors to your palette

The editor supports **dropper mode** - click the dropper button then click anywhere on your canvas to sample colors.

### Dithering Styles

This is where the magic happens! Dithering creates smooth color transitions using patterns of discrete colors - essential for pixel art.

#### Ordered Dithering (Pattern-Based)

| Style | Pattern | Best For |
|-------|---------|----------|
| **None** | Smooth blend (no dithering) | High-color exports, previewing |
| **Bayer 2×2** | Tiny checkerboard | Very small sprites, subtle transitions |
| **Bayer 4×4** | Classic ordered dither | General purpose, retro game look |
| **Bayer 8×8** | Larger pattern | Bigger canvases, smoother gradients |
| **Checker** | Simple 50/50 checkerboard | Stylized, high-contrast look |
| **Diagonal** | Diagonal line pattern | Unique aesthetic, hatching style |
| **Crosshatch** | Cross pattern | Engraving/etching style |
| **Blue Noise** | Random but visually pleasing | Organic, film-like texture |

#### Error Diffusion Dithering

These algorithms spread quantization error to neighboring pixels for organic-looking results:

| Style | Algorithm | Character |
|-------|-----------|-----------|
| **Floyd-Steinberg** | Classic error diffusion | Smooth, organic, industry standard |
| **Atkinson** | Lighter diffusion (75%) | More contrast, Macintosh aesthetic |
| **Riemersma** | Hilbert curve traversal | Most organic, no directional artifacts |

##### What Makes Riemersma Special?

Riemersma dithering uses a **Hilbert curve** (a space-filling fractal) to traverse the image instead of going left-to-right, top-to-bottom. This creates:

- **No directional streaking** - F-S and Atkinson can create visible horizontal artifacts
- **More organic patterns** - The fractal path creates natural-looking noise
- **Better for pixel art** - Adjacent pixels receive more even error distribution

### Multi-Color Gradient Dithering

When using **Floyd-Steinberg**, **Atkinson**, or **Riemersma** with a **Custom** multi-color gradient, the dithering uses your **entire palette of colors**!

**Example:** If your custom gradient has 5 colors (Red → Orange → Yellow → Green → Blue), the error diffusion will pick from ALL those colors to create smooth transitions - not just dither between two colors at a time.

This creates beautiful, painterly gradients that use your exact color palette.

### Dither Controls

| Control | Range | Effect |
|---------|-------|--------|
| **Strength** | 0-100% | How much dithering is applied (0% = solid bands, 100% = full dither) |
| **Scale** | 1-8 | Pattern size multiplier (larger = chunkier pixels) |

### Additional Options

| Option | Description |
|--------|-------------|
| **Reverse** | Flips the gradient direction |
| **Opacity** | Overall transparency of the gradient (0-255) |

### Gradient Fill Tips

1. **Use with Selections** - Make a selection first, then fill only that area
2. **Layer it up** - Apply gradients on separate layers for non-destructive editing
3. **Combine dither styles** - Use Bayer for some areas, Riemersma for others
4. **Match your palette** - Custom gradients work best when colors are from your existing palette
5. **Scale for resolution** - Use Scale 1x for small sprites, 2-4x for larger canvases

---

## Layers

### Layer Types

1. **Raster Layers** - Standard pixel layers for drawing
2. **Layer Folders** - Organize layers into groups

### Layer Panel Operations

| Button/Action | Description |
|--------------|-------------|
| **+** | Add new raster layer |
| **Folder +** | Add new folder |
| **Trash Bin** | Delete selected layer/folder |
| **Eye** | Toggle visibility |
| **Lock** | Toggle lock |
| **Glasses** | Toggle layer effects |
| **Gear** | Open layer settings |

### Layer Settings

Double-click a layer or click the settings button to open:

- **Name** - Rename the layer
- **Opacity** - Layer transparency (0-255)
- **Blend Mode** - How layer combines with layers below:
  - Normal, Multiply, Screen, Overlay
  - Add, Subtract, Difference
  - Darken, Lighten, Hard Light, Invert
- **Solo** - Show only this layer
- **Effects** - Apply and configure layer effects

### Layer Blend Modes

| Mode | Effect |
|------|--------|
| **Normal** | Standard layering |
| **Multiply** | Darkens (great for shadows) |
| **Screen** | Lightens (great for highlights) |
| **Overlay** | Combines Multiply and Screen |
| **Add** | Brightens additively (glow effects) |
| **Subtract** | Darkens subtractively |
| **Difference** | Creates psychedelic color inversions |

### Layer Organization

- **Drag and Drop** - Reorder layers
- **Drag into Folder** - Move layer into a group
- **Drag out** - Move layer to root level
- **Merge Down** - Combine layer with the one below
- **Flatten Folder** - Merge all visible folder contents

---

## Layer Masks

Layer masks allow **non-destructive hiding** of portions of a layer. Instead of erasing pixels permanently, you paint on a mask to show or hide areas - and you can always change your mind!

### How Masks Work

A layer mask is a **grayscale image** where:

| Color | Effect |
|-------|--------|
| **White (255)** | Fully visible - layer content shows through |
| **Black (0)** | Fully hidden - layer content is masked out |
| **Gray (1-254)** | Partial transparency - softer transitions |

Think of it like a stencil - white areas let paint through, black areas block it.

### Creating a Mask

1. Select a layer in the Layers panel
2. Right-click → **Add Mask**, or use the mask button
3. A new mask appears as a white thumbnail next to the layer preview

New masks start as **all white** (fully visible) - nothing changes visually until you paint on the mask.

### Editing a Mask

To switch to **Mask Editing Mode**:

1. **Click the mask thumbnail** in the Layers panel
2. A red overlay appears on the canvas showing the mask
3. Your tools now paint on the mask instead of the layer

When editing a mask:
- **Paint with Black** → Hides areas (adds red overlay)
- **Paint with White** → Reveals areas (removes red overlay)
- **Paint with Gray** → Partial hide/reveal

The **red overlay** helps you see what you're doing:
- More red = more hidden
- No red = fully visible

To switch back to editing the **layer pixels**:
- Click the **layer preview thumbnail** (not the mask thumbnail)

### Mask Operations

Right-click a layer with a mask for these options:

| Operation | Description |
|-----------|-------------|
| **Apply Mask** | Permanently bakes the mask into the layer (destructive) |
| **Delete Mask** | Removes the mask, restoring full layer visibility |
| **Invert Mask** | Swaps white/black (hidden becomes visible and vice versa) |

### Mask Properties

Each mask has configurable properties:

| Property | Description |
|----------|-------------|
| **Enabled** | Toggle mask on/off without deleting |
| **Inverted** | Flip the mask effect (white hides, black reveals) |
| **Linked** | When on, moving the layer also moves the mask |
| **Density** | Overall mask strength (0-255) |
| **Feather** | Blur the mask edges for softer transitions |

### Mask Use Cases

#### Fading Edges
Paint a gradient on the mask to fade a layer's edges smoothly.

#### Non-Destructive Erasing
Instead of using the Eraser tool (permanent), paint black on a mask. Change your mind? Paint white to restore!

#### Complex Compositing
Combine elements from multiple layers with precise control over what shows where.

#### Vignettes and Spotlights
Use radial gradients on masks to create spotlight or vignette effects on specific layers.

### Mask Tips

1. **Soft brushes for soft edges** - Use a round brush with low density for smooth mask transitions
2. **Preview often** - Toggle mask visibility to see the actual result
3. **White reveals, black conceals** - Remember this mantra!
4. **Non-destructive first** - Try masks before permanently erasing
5. **Apply when done** - If you're happy with the mask, apply it to reduce file size

---

## Layer Effects

Layer effects are **non-destructive** filters applied to layers. They can be toggled on/off and animated!

### Available Effects

#### Stylize Effects

| Effect | Description |
|--------|-------------|
| **Drop Shadow** | Offset shadow behind the layer |
| **Outline** | Stroke around non-transparent pixels |
| **Glow/Bloom** | Soft light emanating from bright areas |
| **Chromatic Aberration** | RGB channel separation for retro/glitch look |
| **ASCII Art** | Convert to character-based art |

#### Filter Effects

| Effect | Description |
|--------|-------------|
| **Scanlines** | CRT-style horizontal lines |
| **Grain** | Film-like noise texture |
| **Vignette** | Darkened edges |
| **CRT** | Retro monitor curvature and effects |
| **Pixelate** | Reduce resolution for chunky look |

#### Color Effects

| Effect | Description |
|--------|-------------|
| **Color Adjust** | Hue, Saturation, Brightness, Contrast |
| **Palette Quantize** | Reduce to specific color palette |


### Using Layer Effects

1. Select a layer
2. Open **Layer Settings** (gear icon or double-click)
3. Scroll to **Effects** section
4. Check the box next to an effect to enable it
5. Expand the effect to configure parameters
6. Effects stack in order - drag to reorder

### Animating Effects

Layer effects are **fully animatable** in Canvas Animation mode:

1. Navigate to a frame
2. Configure effect settings
3. Add a keyframe
4. Navigate to another frame
5. Change effect settings
6. Add another keyframe
7. Effects will hold their values between keyframes

**Example Animation Ideas:**
- Pulsing glow on a power-up
- Scanlines that intensify during a flashback
- Chromatic aberration on damage
- Vignette that closes in during danger

---

## Reference Layers

Reference layers let you overlay external images on your canvas as **non-destructive guides**. Perfect for tracing, color sampling, or keeping a concept sketch visible while you work.

### Adding a Reference Image

1. Go to **View → Add Reference Image** or use the button in the Layers panel
2. Select an image file (PNG, JPG, BMP, GIF supported)
3. The image appears as a new reference layer

Reference layers appear in the Layers panel with a distinct icon and are separate from your raster layers.

### Manipulating Reference Layers

**Select** a reference layer by clicking on it in the canvas or Layers panel.

Once selected, you'll see transform handles:

| Handle | Action |
|--------|--------|
| **Corner handles** | Resize (maintains aspect ratio) |
| **Edge handles** | Resize from edges |
| **Outside corners** | Rotate |
| **Inside body** | Drag to move |

**Keyboard modifiers:**
- `Shift` while rotating - Snap to 15° increments

### Reference Layer Properties

Access settings via the Layers panel:

| Property | Description |
|----------|-------------|
| **Opacity** | Transparency of the reference image (0-100%) |
| **Visible** | Toggle visibility (eye icon) |
| **Locked** | Prevent accidental moves/resizes (lock icon) |
| **Scale** | Current scale factor |
| **Rotation** | Current rotation angle |
| **Position** | X/Y coordinates |
| **Image Path** | Full file path of the reference image |

### Reference Layer Actions

Right-click a reference layer for additional options:

| Action | Description |
|--------|-------------|
| **Reset Transform** | Return to original size/position |
| **Fit to Canvas** | Scale to fit within canvas bounds |
| **Delete** | Remove the reference layer |

### Tips for Using Reference Layers

1. **Lock when positioned** - Once your reference is where you want it, lock it to prevent accidental moves
2. **Lower opacity** - Set to 30-50% to see your work through the reference
3. **Use multiple references** - Add several images for different aspects (pose, color, detail)
4. **Color sample from references** - Use the Dropper tool to pick colors directly from reference images
5. **Hide during export** - Reference layers are not included in exports (they're guides only)

### Reference vs Raster Layers

| Aspect | Reference Layer | Raster Layer |
|--------|-----------------|--------------|
| **Purpose** | Visual guide | Actual artwork |
| **Editable pixels** | No | Yes |
| **Transform** | Non-destructive | Destructive |
| **Exported** | No | Yes |
| **Effects** | No | Yes |
| **Animated** | No | Yes (keyframes) |

---

## Palette

### Color Selection

- **Left Click** a swatch - Set as foreground color
- **Right Click** a swatch - Set as background color
- **X** - Swap foreground and background colors

### Palette Panel Operations

| Action | Description |
|--------|-------------|
| **+** | Add foreground color to palette |
| **Trash** | Remove selected color |
| **Gradient** | Create gradient between colors |

### Color Picker

Click the foreground/background color box to open the full color picker:

- **HSL Square** - Click to set saturation and lightness
- **Hue Bar** - Drag to select hue
- **Alpha Slider** - Set transparency
- **RGB Sliders** - Fine-tune red, green, blue
- **HSL Sliders** - Fine-tune hue, saturation, lightness
- **Hex Input** - Enter exact color code
- **Shade/Tint/Tone/Hue Bars** - Quick variations of current color
- **Old/New Preview** - Compare original and selected color

### Gradient Creator

1. Select a start color and end color
2. Go to **Palette → Create Gradient**
3. Set the number of steps
4. Choose falloff curve
5. Click **Add** to append gradient colors to palette

### Palette Management

- **Palette → Export** - Save palette to file (JSON, JASC, GPL, etc.)
- **Palette → Import** - Load palette from file
- **Palette → Reset to Default** - Restore the default PixlPunkt palette
- **Palette → Sort** - Organize by Hue, Saturation, Lightness, or Alpha

### Built-in Palettes

Access preset palettes via **Palette → Presets**:

| Category | Palettes |
|----------|----------|
| **Retro Hardware** | NES, GameBoy, C64, CGA, EGA, VGA, PICO-8 |
| **Aesthetic** | Cyberpunk, Vaporwave, Pastel, Earth Tones |
| **Grayscale** | Various gray ramps |
| **Custom** | Your saved palettes |

---

## Tiles

### Understanding Tiles

PixlPunkt uses a **tile-based** system:
- Your canvas is divided into a grid of equally-sized tiles
- Each tile can be reused across the canvas
- Changes to a tile update all instances

This is ideal for:
- Game tilesets and sprite sheets
- Repeating patterns
- Memory-efficient game assets

### Tile Panel Operations

| Button | Description |
|--------|-------------|
| **+** | Add new empty tile |
| **Duplicate** | Copy selected tile |
| **Tessellator** | Open tile tessellation window |
| **Zoom +/-** | Adjust tile preview size |
| **Delete** | Remove selected tile |

### Tile Tessellation Window

The Tessellator helps create seamlessly tiling patterns:

1. Select a tile and click **Tessellator**
2. View the tile repeated in a grid
3. Paint directly to see how edges connect
4. Use **Offset** controls to shift content
5. **Apply** when satisfied

### Tile Mapping

Each layer can have independent tile mappings:
- Use the **Tile Stamper (A)** tool to place tiles
- View mappings with **View → Toggle Tile Mappings**
- Tiles store references, not copies, for efficiency

---

## Animation

PixlPunkt supports **two distinct animation systems** for different workflows:

| Type | Best For | How It Works |
|------|----------|--------------|
| **Canvas Animation** | Complex animation, cutscenes | Full keyframes with pixel data, effects, camera |
| **Tile Animation** | Sprite sheets, game assets | Frame sequences from tile grid positions |

---

### Canvas Animation

Canvas Animation is a professional, layer-based animation system. Each layer can have independent keyframes that store complete state snapshots.

#### What Keyframes Store

Each keyframe (◆) captures:
- Complete pixel data for the layer
- Layer visibility (on/off)
- Layer opacity (0-255)
- Blend mode
- All layer effect settings (enabled state + every parameter)
- Mask state (if the layer has a mask)

#### Hold-Frame Behavior

Values are **held constant** between keyframes. If you set a keyframe at frame 0 and frame 10, frames 1-9 will display the frame 0 state.

#### Creating Keyframes

1. **Navigate** to the desired frame
2. **Make changes** to your layer
3. **Add Keyframe** via:
   - Click the **Add Keyframe** button (◆+)
   - Right-click timeline → **Add Keyframe**

#### Playback Controls

| Button | Action |
|--------|--------|
| ⏮ | First frame |
| ◀ | Previous frame |
| ⏸/▶ | Play/Pause |
| ▶ | Next frame |
| ⏭ | Last frame |
| ⏹ | Stop (return to frame 0) |

#### Onion Skinning

Shows ghost images of nearby frames:

1. Enable via the **Onion Skin** toggle
2. Configure:
   - **Frames Before** - Previous frames to show (blue tint)
   - **Frames After** - Future frames to show (green tint)
   - **Opacity** - Ghost transparency

---

### Tile Animation

For simpler sprite sheet workflows, Tile Animation sequences through tile grid positions.

#### Concepts

- **Reels** - Named animation sequences
- **Frames** - References to tile coordinates
- **Timing** - Global or per-frame duration

#### Creating a Tile Reel

1. Switch to **Tile** mode
2. Click **New Reel** (+)
3. Name it (e.g., "Walk", "Idle", "Attack")
4. Click tiles to add frames
5. Adjust timing as needed

---

## Stage (Camera System)

The Stage provides a **virtual camera** for Canvas Animation, enabling pan, zoom, and rotation without modifying artwork.

### Enabling the Stage

1. Open **Stage Settings** (camera icon)
2. Check **Enable Stage**
3. Set dimensions:
   - **Stage Size** - Viewport area on canvas
   - **Output Size** - Final rendered dimensions

### Stage Keyframes (◇)

Unlike layer keyframes, stage keyframes **interpolate** for smooth camera motion:

- **Position** (X, Y) - Camera center
- **Scale** (X, Y) - Zoom level
- **Rotation** - Camera angle
- **Easing** - Interpolation curve

### Easing Options

| Easing | Effect |
|--------|--------|
| Linear | Constant speed |
| EaseIn | Start slow |
| EaseOut | End slow |
| EaseInOut | Slow at both ends |
| Bounce | Bouncy overshoot |
| Elastic | Spring motion |

### Camera Workflow

1. Enable Stage
2. Navigate to frame 0
3. Position camera
4. Add Stage Keyframe (◇+)
5. Navigate to later frame
6. Move/zoom/rotate camera
7. Add another keyframe
8. Play to see smooth motion!

---

## Audio Reference Tracks

Add audio tracks to help sync your animation to music or dialogue.

### Adding Audio

1. Click the **Add Audio Track** button
2. Select an audio file (MP3, WAV, OGG, etc.)
3. The waveform appears in the timeline

### Audio Track Features

| Feature | Description |
|---------|-------------|
| **Waveform Display** | Visual representation of the audio |
| **Frame Offset** | Start audio at a specific frame |
| **Volume** | Adjust playback volume |
| **Mute** | Silence without removing |
| **Multiple Tracks** | Add several audio files |

### Audio Sync Tips

- Audio is **reference only** - not exported with animation by default
- Use waveform peaks to align keyframes to beats
- Scrub timeline to hear audio at any frame
- Mute during work, enable for review

---

## Keyboard Shortcuts

### File Operations
| Shortcut | Action |
|----------|--------|
| `Ctrl+N` | New Canvas |
| `Ctrl+O` | Open |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+E` | Export |

### Edit Operations
| Shortcut | Action |
|----------|--------|
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Ctrl+C` | Copy |
| `Ctrl+X` | Cut |
| `Ctrl+V` | Paste |
| `Ctrl+A` | Select All |
| `Ctrl+D` | Deselect |

### View
| Shortcut | Action |
|----------|--------|
| `Ctrl+0` | Fit to Screen |
| `Ctrl+1` | Actual Size (1:1) |
| `Ctrl++` | Zoom In |
| `Ctrl+-` | Zoom Out |

### Tools
| Shortcut | Action |
|----------|--------|
| `B` | Brush |
| `E` | Eraser |
| `G` | Fill |
| `D` | Gradient Brush |
| `Shift+G` | Gradient Fill |
| `R` | Replacer |
| `U` | Blur |
| `J` | Jumble |
| `Shift+S` | Smudge |
| `M` | Rectangle Select |
| `W` | Magic Wand |
| `L` | Lasso |
| `Shift+P` | Paint Selection |
| `Ctrl+U` | Rectangle |
| `O` | Ellipse |
| `I` | Dropper |
| `H` | Pan |
| `Z` | Zoom |
| `X` | Swap FG/BG colors |
| `[` | Decrease brush size |
| `]` | Increase brush size |

### Animation
| Shortcut | Action |
|----------|--------|
| `Space` | Play/Pause |
| `,` | Previous Frame |
| `.` | Next Frame |
| `Home` | First Frame |
| `End` | Last Frame |

---

## Settings

Access via **Edit → Preferences** or the gear icon.

### General Settings

| Setting | Description |
|---------|-------------|
| **Storage Folder** | Default save location |
| **Auto-backup Interval** | Minutes between backups |
| **Backups to Retain** | Number of backup files |
| **Transparency Pattern** | Checkerboard style (System/Light/Dark) |

### Palette Settings

| Setting | Description |
|---------|-------------|
| **Default Palette** | Palette loaded on startup |
| **Swatch Size** | Color swatch display size |

### Tile Settings

| Setting | Description |
|---------|-------------|
| **Tile View Size** | Tile preview size in panel |
| **Default Tile Set** | `.pxpt` file for new documents |

---

## File Formats

### Native Format

| Extension | Description |
|-----------|-------------|
| `.pxp` | PixlPunkt document (full feature preservation, including animation, masks) |

### Import Formats

| Extension | Source | Notes |
|-----------|--------|-------|
| `.pyxel` | PyxelEdit | Full layer support |
| `.ase` / `.aseprite` | Aseprite | Frame 0 layers |
| `.ico` | Windows Icon | Largest frame |
| `.cur` | Windows Cursor | Largest frame |
| `.tmx` | Tiled Map | Rendered layers |
| `.tsx` | Tiled Tileset | Tileset image |

### Export Formats

| Format | Alpha | Animation | Best For |
|--------|-------|-----------|----------|
| **PNG** | Yes | Sequence | Pixel art (recommended) |
| **GIF** | 1-bit | Yes | Web, simple animations |
| **MP4** | No | Yes | High quality video |
| **BMP** | No | No | Uncompressed |
| **JPEG** | No | Sequence | Not recommended for pixel art |

---

## Advanced Topics

### Dithering Deep Dive

Dithering is essential for pixel art because it lets you simulate more colors than your palette actually contains. Here's everything you need to know to master it.

#### Why Dither?

Limited color palettes can't represent smooth gradients directly. Dithering creates the **illusion** of intermediate colors by mixing patterns of available colors.

#### Ordered vs Error Diffusion

| Type | How It Works | Character |
|------|--------------|-----------|
| **Ordered** | Uses a fixed pattern (threshold matrix) | Regular, retro, predictable |
| **Error Diffusion** | Spreads quantization error to neighbors | Organic, smooth, painterly |

#### Choosing the Right Dither

| Use Case | Recommended Dither |
|----------|-------------------|
| Retro game aesthetic | Bayer 4×4 |
| Very small sprites | Bayer 2×2 or None |
| Large backgrounds | Bayer 8×8 or Blue Noise |
| Natural/painterly look | Floyd-Steinberg |
| High contrast | Atkinson |
| Most organic results | Riemersma |
| Film/photo feel | Blue Noise |

#### The Strength/Scale Dance

- **Low Strength + Low Scale** = Subtle transition, mostly solid colors
- **High Strength + Low Scale** = Full dither, fine patterns
- **High Strength + High Scale** = Chunky, stylized pixel look
- **Low Strength + High Scale** = Barely visible large patterns

#### Multi-Color Dithering Magic

When using error diffusion (F-S, Atkinson, Riemersma) with custom gradients:

1. Create a gradient with 3+ carefully chosen colors
2. Apply with error diffusion dithering
3. The algorithm picks from your ENTIRE palette at each pixel
4. Result: smooth transitions using only your chosen colors

This is **huge** for maintaining color consistency while getting smooth shading.

---

### Animation Workflow

Here's a professional workflow for creating polished animations in PixlPunkt.

#### Phase 1: Planning

1. **Thumbnail** key poses on paper or a separate layer
2. **Decide timing** - 12 FPS for standard, 6-8 for snappy/game feel
3. **Count frames** - Walk cycle = ~6-12 frames, idle = ~4-8 frames

#### Phase 2: Blocking

1. Create your character/object on a single layer
2. Navigate to frame 0, draw key pose #1, add keyframe
3. Jump ahead (e.g., frame 6), draw key pose #2, add keyframe
4. Jump ahead again for key pose #3, etc.
5. **Play back** to check timing and flow

#### Phase 3: Breakdown Frames

1. Go to frames BETWEEN your key poses
2. Draw the in-between positions
3. Add keyframes for each
4. These "breakdowns" show the arc of motion

#### Phase 4: Polish

1. Add secondary motion (hair bounce, cloth follow-through)
2. Adjust timing - move keyframes if something feels off
3. Add layer effects for punch (glow on impacts, screen shake)

#### Phase 5: Export

1. **File → Export Animation**
2. Choose format (GIF for web, MP4 for quality)
3. Set scale (2x or 4x for better visibility)
4. Enable Stage if using camera

#### Pro Tips

- **Use onion skin religiously** - You can't animate well without seeing context
- **Work rough first** - Silhouettes and blobs, then refine
- **Loop early** - Check your loop point before adding detail
- **Save versions** - Keep backups of major milestones

---

### Tile-Based Game Art

Creating game-ready tilesets in PixlPunkt.

#### Tileset Organization

1. **Set tile size wisely** - 16×16 is versatile, 8×8 for tiny, 32×32 for detailed
2. **Group similar tiles** - Ground tiles together, wall tiles together
3. **Use the Tessellator** - Test edge connections constantly

#### Essential Tiles

For a basic platformer tileset:

| Category | Tiles Needed |
|----------|--------------|
| **Ground** | Top, middle, bottom, left/right edges |
| **Corners** | Inner and outer corners (8 variations) |
| **Platforms** | Left cap, middle, right cap |
| **Background** | Solid fill, variations for interest |
| **Decorations** | Grass tufts, rocks, flowers |

#### Seamless Tiling Tips

1. **Match edges exactly** - The left edge of tile A must match the right edge of tile B
2. **Use the Tessellator** to test - See it repeated immediately
3. **Avoid tangents** - Don't let lines touch edges at exact same points
4. **Vary the middle** - Keep edges consistent but vary centers for interest

#### Per-Layer Tile Mappings

Each layer has its **own** tile mapping:
- Background layer → background tiles
- Foreground layer → foreground tiles
- Collision layer → collision tiles (invisible in game, visible in editor)

This lets you paint different tile types on separate layers while keeping everything organized.

#### Export for Game Engines

1. **File → Export** your tileset image (PNG)
2. In your game engine, slice by your tile size
3. Or export individual tiles via **Tiles → Export Tiles**

---

## Tips & Tricks

### Workflow Tips

1. **Use Layers** - Separate elements for easy editing
2. **Save Often** - `Ctrl+S` is your friend
3. **Work at 1x** - Paint at actual pixel size
4. **Preview Panel** - See how art looks at different sizes

### Pixel Art Techniques

1. **Anti-aliasing** - Add intermediate colors at edges
2. **Dithering** - Use patterns to simulate gradients
3. **Hue Shifting** - Shift hue when shading (cool shadows, warm highlights)
4. **Limited Palette** - Fewer colors = more cohesive art

### Gradient Fill Pro Tips

1. **Start simple** - Begin with 2-color gradients
2. **Match your palette** - Use colors already in your artwork
3. **Experiment with dithers** - Each style has unique character
4. **Use Riemersma** - Best for organic, natural-looking transitions
5. **Scale matters** - Larger Scale = chunkier, more retro
6. **Layer gradients** - Build complex effects with multiple gradient layers

### Mask Pro Tips

1. **Non-destructive first** - Always try a mask before erasing
2. **Soft brushes** - Use low-density brushes for smooth mask edges
3. **Invert to start** - Sometimes it's easier to reveal than conceal
4. **Apply when done** - Reduce file size by applying final masks

### Animation Pro Tips

1. **Key poses first** - Block major moments before details
2. **Use onion skin** - Essential for smooth motion
3. **Animate effects** - Layer effects bring life to static art
4. **Reference audio** - Sync to beats for music videos
5. **Camera work** - Stage keyframes add cinematic feel

---

## Getting Help

- **GitHub Issues** - Report bugs or request features
- **Plugin SDK** - See `PixlPunkt.PluginSdk` documentation
- **Logs** - Found in `%LocalAppData%\PixlPunkt\Logs`

---

<p align="center">
  <strong>Happy creating!</strong>
</p>
