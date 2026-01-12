# Gradient Fill

The **Gradient Fill** tool <img src="../docs/assets/icons/data_sunburst_20.png" width="20"> (`Shift+G`) is one of PixlPunkt's most powerful features. It creates smooth color transitions with pixel-art-friendly dithering - perfect for skies, backgrounds, lighting effects, and stylized shading.

## Basic Usage

1. Select the **Gradient Fill** tool (`Shift+G`) <img src="../docs/assets/icons/data_sunburst_16.png" width="16">
2. Choose your **Gradient Type** (Linear, Radial, Angular, Diamond)
3. Select your **Color Mode** or click **Custom...** for multi-color gradients
4. Click and drag on the canvas:
   - **Start point** = where the gradient begins
   - **End point** = where the gradient ends
5. Release to apply

---

## Gradient Types

| Type | Description | Best For |
|------|-------------|----------|
| **Linear** | Straight transition from start to end | Skies, horizons, flat surfaces |
| **Radial** | Circular gradient from center outward | Light sources, orbs, explosions |
| **Angular** | Rotates around center point | Conic effects, color wheels |
| **Diamond** | Diamond/rhombus shaped gradient | Stylized lighting, gem effects |

---

## Color Modes

| Mode | Description |
|------|-------------|
| **White → Black** | Simple grayscale gradient |
| **Black → White** | Inverted grayscale |
| **FG → BG** | Uses your current foreground and background colors |
| **BG → FG** | Reversed foreground/background |
| **Custom...** | Opens the gradient editor for multi-color gradients |

---

## The Gradient Preview Strip

The tool options bar includes a **live gradient preview strip** that shows exactly what colors will be used:

- Updates in real-time as you change settings
- Shows the effect of the **Reverse** toggle
- In **Custom** mode, click the preview to open the gradient editor

---

## Custom Gradient Editor

Click **Custom...** in the color mode dropdown to open the gradient editor window:

1. **Start Color** - Click to select or use <img src="../docs/assets/icons/eyedropper_16.png" width="16"> dropper
2. **End Color** - Click to select or use <img src="../docs/assets/icons/eyedropper_16.png" width="16"> dropper
3. **Steps** - Number of colors in the gradient
4. **Preview** - Shows all generated colors
5. <img src="../docs/assets/icons/add_16.png" width="16"> **Add to Palette** - Saves colors to your palette

The editor supports **dropper mode** - click the <img src="../docs/assets/icons/eyedropper_16.png" width="16"> dropper button then click anywhere on your canvas to sample colors.

---

## Dithering Styles

This is where the magic happens! Dithering creates smooth color transitions using patterns of discrete colors - essential for authentic pixel art.

### Ordered Dithering (Pattern-Based)

These use fixed threshold matrices to create predictable, repeatable patterns:

| Style | Pattern | Best For |
|-------|---------|----------|
| **None** | No dithering (smooth blend) | High-color exports, previewing |
| **Bayer 2×2** | Tiny checkerboard | Very small sprites, subtle transitions |
| **Bayer 4×4** | Classic ordered dither | General purpose, retro game look |
| **Bayer 8×8** | Larger pattern | Bigger canvases, smoother gradients |
| **Checker** | Simple 50/50 checkerboard | Stylized, high-contrast look |
| **Diagonal** | Diagonal line pattern | Unique aesthetic, hatching style |
| **Crosshatch** | Cross pattern | Engraving/etching style |
| **Blue Noise** | Random but visually pleasing | Organic, film-like texture |

### Error Diffusion Dithering

These algorithms spread quantization error to neighboring pixels for organic-looking results:

| Style | Algorithm | Character |
|-------|-----------|-----------|
| **Floyd-Steinberg** | Classic error diffusion | Smooth, organic, industry standard |
| **Atkinson** | Lighter diffusion (75%) | More contrast, Macintosh aesthetic |
| **Riemersma** | Hilbert curve traversal | Most organic, no directional artifacts |

---

## Deep Dive: Error Diffusion Algorithms

### Floyd-Steinberg

The most common error diffusion algorithm. When a pixel is quantized to the nearest palette color, the "error" (difference from the original) is distributed to neighboring pixels:

```
        X     7/16
  3/16  5/16  1/16
```

Where X is the current pixel. This creates smooth gradients but can produce visible horizontal streaking in some cases.

### Atkinson

Developed by Bill Atkinson for the original Macintosh. Only distributes 75% of the error (6/8 instead of 8/8), which creates:

- Higher contrast results
- Less "muddy" appearance
- More visible dithering pattern
- The classic Mac OS look

```
        X     1/8   1/8
  1/8   1/8   1/8
        1/8
```

### Riemersma (The Secret Weapon)

This is the **best** dithering algorithm for pixel art, and here's why:

Instead of scanning left-to-right, top-to-bottom (which causes directional artifacts), Riemersma traverses the image using a **Hilbert curve** - a space-filling fractal that visits every pixel while staying "local."

**Why it matters:**
- **No directional streaking** - Floyd-Steinberg can create visible horizontal lines
- **More organic patterns** - The fractal path creates natural-looking noise
- **Better for pixel art** - Adjacent pixels receive more even error distribution
- **No "worm" artifacts** - Common in F-S when dithering large areas

**When to use it:**
- Natural gradients (skies, water, skin tones)
- Large background areas
- When other algorithms look "streaky"
- When you want the most pleasing result

---

## Multi-Color Gradient Dithering

When using **Floyd-Steinberg**, **Atkinson**, or **Riemersma** with a **Custom** multi-color gradient, something magical happens:

The dithering algorithm uses your **entire palette of colors**, not just pairs!

**Example:** If your custom gradient has 5 colors (Red → Orange → Yellow → Green → Blue), the error diffusion will pick from ALL those colors at every pixel to create the smoothest possible transition.

This creates beautiful, painterly gradients that perfectly match your color palette.

---

## Dither Controls

| Control | Range | Effect |
|---------|-------|--------|
| **Strength** | 0-100% | How much dithering is applied |
| **Scale** | 1-8 | Pattern size multiplier |

### Strength Examples

| Strength | Effect |
|----------|--------|
| 0% | Solid color bands, no dithering |
| 25% | Subtle transition, mostly solid |
| 50% | Balanced dithering |
| 75% | Heavy dithering, retro feel |
| 100% | Maximum dithering pattern |

### Scale Examples

| Scale | Effect |
|-------|--------|
| 1x | Fine detail, subtle dithering |
| 2x | Visible pattern, balanced |
| 4x | Chunky, bold, very retro |
| 8x | Massive pixels, stylized |

---

## Additional Options

| Option | Description |
|--------|-------------|
| **Reverse** | Flips the gradient direction |
| **Opacity** | Overall transparency (0-255) |

---

## Gradient Fill Tips

### Working with Selections
Make a selection first (use <img src="../docs/assets/icons/select_object_16.png" width="16"> Rectangle Select or <img src="../docs/assets/icons/wand_16.png" width="16"> Magic Wand), then the gradient only fills that area. Great for:
- Gradient text
- Shaped backgrounds
- Masking effects

### Layering Gradients
Apply gradients on separate layers for non-destructive editing:
1. Click <img src="../docs/assets/icons/add_16.png" width="16"> to create new layer
2. Apply gradient
3. Adjust layer opacity/blend mode via <img src="../docs/assets/icons/settings_16.png" width="16">
4. Stack multiple gradients for complex effects

### Combining Dither Styles
Use different dithering in different areas:
- Bayer for geometric shapes
- Riemersma for organic areas
- Blue Noise for subtle textures

### Matching Your Palette
Custom gradients work best when colors are from your existing palette <img src="../docs/assets/icons/color_16.png" width="16">. This ensures color consistency across your artwork.

### Scale for Resolution
- **Small sprites (16×16, 32×32):** Scale 1x-2x
- **Medium art (64×64, 128×128):** Scale 2x-3x
- **Large canvases (256+):** Scale 3x-4x

---

## Quick Recipes

### Sunset Sky
- **Type:** Linear (top to bottom)
- **Colors:** Custom (Orange → Pink → Purple → Dark Blue)
- **Dithering:** Bayer 4×4
- **Strength:** 70%

### Retro Vignette
- **Type:** Radial (center out)
- **Colors:** Transparent → Black
- **Dithering:** Blue Noise
- **Strength:** 50%

### Ocean Depth
- **Type:** Linear (top to bottom)
- **Colors:** Custom (Light Blue → Blue → Dark Blue → Black)
- **Dithering:** Riemersma
- **Strength:** 60%

### Fire/Explosion
- **Type:** Radial (center out)
- **Colors:** Custom (White → Yellow → Orange → Red → Black)
- **Dithering:** Floyd-Steinberg
- **Strength:** 80%

### Metallic Sheen
- **Type:** Linear (45° angle)
- **Colors:** Custom (Dark Gray → Light Gray → White → Light Gray → Dark Gray)
- **Dithering:** Bayer 2×2
- **Strength:** 40%

---

## See Also

- [[Dithering]] - Even deeper dive into dithering theory
- [[Tools]] - All tools reference
- [[Palette]] - Color management
