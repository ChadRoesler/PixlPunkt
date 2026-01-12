# Layer Effects

Layer effects <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/image_sparkle_20.png" width="20"> are non-destructive filters applied to layers. They can be toggled on/off, adjusted anytime, and fully animated - making them incredibly powerful for both static art and animation.

## Overview

Effects are applied per-layer and render in real-time. The original pixel data is never modified - effects are computed on top.

**Key benefits:**
- ✅ Non-destructive (always reversible)
- ✅ Animatable (change settings over time)
- ✅ Stackable (multiple effects per layer)
- ✅ Reorderable (drag to change effect order)

---

## Accessing Layer Effects

1. **Double-click** a layer to open settings
2. Scroll to **Effects** section
3. Check the box next to an effect to enable it
4. Expand the effect to configure parameters

Or:
- **Right-click layer → Effects**
- Click the <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_16.png" width="16"> **gear icon** on the layer

---

## Toggling Effects

| Icon | State | Description |
|:----:|-------|-------------|
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/glasses_20.png" width="20"> | Effects On | Layer effects are rendering |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/glasses_off_20.png" width="20"> | Effects Off | Layer effects disabled (faster editing) |

Click the <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/glasses_16.png" width="16"> icon on a layer row to quickly toggle all its effects.

---

## Available Effects

### Stylize Effects

#### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_shadow_20.png" width="20"> Drop Shadow
Adds a shadow behind the layer content.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Offset X** | -64 to 64 | Horizontal displacement |
| **Offset Y** | -64 to 64 | Vertical displacement |
| **Opacity** | 0-1 | Shadow transparency |
| **Blur** | 0-64 | Shadow softness |
| **Color** | Any | Shadow color (default: black) |

**Tips:**
- Offset in the direction opposite your light source
- Small blur (1-3) for pixel art, larger for soft shadows
- Use colored shadows (dark blue, purple) for more depth

---

#### Outline
Adds a stroke around non-transparent pixels.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Color** | Any | Outline color |
| **Thickness** | 1-32 | Stroke width in pixels |
| **Outside Only** | On/Off | Draw outline only outside the shape |

**Tips:**
- 1px thickness is classic pixel art style
- Use contrasting colors for readability

---

#### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/sparkle_20.png" width="20"> Glow / Bloom
Creates a soft light emission effect.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Intensity** | 0-2 | Glow brightness |
| **Radius** | 0-64 | How far glow extends |
| **Threshold** | 0-1 | Brightness level to start glowing |

**Tips:**
- Low threshold = more pixels glow
- High intensity can blow out to white
- Great for magic, fire, UI highlights

---

#### Chromatic Aberration
Separates RGB channels for a glitch/retro look.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Offset** | 0-64 | Radial channel separation in pixels |
| **Strength** | 0-1 | Blend strength between original and shifted |

**Tips:**
- Small values (1-3) for subtle retro CRT feel
- Large values for glitch/damage effects
- Animate for impact moments

---

### Filter Effects

#### Scanlines
Adds horizontal CRT-style lines.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Intensity** | 0-1 | Line darkness |
| **Thickness** | 1-64 | Line height in pixels |
| **Spacing** | 0-64 | Gap between lines |
| **Color** | Any | Line color (default: black) |
| **Apply on Transparent** | On/Off | Draw lines over transparent pixels |

**Tips:**
- Spacing 2, Thickness 1 for classic CRT
- Animate offset for scrolling effect
- Combine with CRT effect for full retro

---

#### Grain
Adds film-like noise texture.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Amount** | 0-1 | Noise intensity |
| **Monochrome** | On/Off | Grayscale vs. color grain |
| **Animated** | On/Off | Grain changes each frame |

**Tips:**
- 0.1-0.2 amount for subtle texture
- Enable Animated for film look in animations
- Combine with Color Adjust for vintage feel

---

#### Vignette
Darkens edges of the frame.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Intensity** | 0-1 | How dark the edges get |
| **Radius** | 0-2 | Size of the clear center |
| **Softness** | 0-1 | Edge falloff smoothness |

**Tips:**
- Creates focus on center content
- Animate intensity for dramatic moments
- Use colored vignettes for mood

---

#### CRT
Simulates old CRT monitor effects.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Curvature** | 0-1 | Screen bend amount |
| **Scanline Intensity** | 0-1 | Built-in scanlines |
| **Bloom** | 0-1 | Glow around bright areas |

**Tips:**
- Full retro look in one effect
- Subtle settings for tasteful nostalgia
- Heavy settings for broken TV aesthetic

---

#### Pixelate
Reduces apparent resolution.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Block Size** | 1-64 | Pixel cluster size |

**Tips:**
- Creates chunky pixel look within pixel art (meta!)
- Use for distance/blur simulation
- Animate for "loading" or "glitch" effect

---

### Color Effects

#### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_20.png" width="20"> Color Adjust
Modifies hue, saturation, brightness, and contrast.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Hue Shift** | -180° to 180° | Rotate all colors |
| **Saturation** | -100 to 100 | Color intensity |
| **Brightness** | -100 to 100 | Lighten/darken |
| **Contrast** | -100 to 100 | Tonal range |

**Tips:**
- Hue shift for palette swaps (different team colors)
- Desaturate for flashback/death effects
- Animate brightness for flash impacts

---

#### Palette Quantize
Reduces colors to a specific palette.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Palette** | Dropdown | Target color palette |
| **Dithering** | None/Bayer/F-S | How to handle transitions |
| **Dither Strength** | 0-100% | Dithering intensity |

**Tips:**
- Force art into retro palette (NES, GameBoy)
- Apply to photo imports for pixel art conversion
- Different palettes create different moods

---

#### ASCII Art
Converts layer to character-based representation.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Glyph Set** | Various | Which character set to use |
| **Foreground Color** | Any | Character color |
| **Background Color** | Any | Background fill color |
| **Show Background** | On/Off | Fill background |

**Tips:**
- Fun stylistic effect
- Works best on high-contrast images
- Use different glyph sets for unique looks

---

## Effect Stacking

Effects apply in order from top to bottom:

```
Layer "Character"
├─ Effect: Outline (applies first)
├─ Effect: Drop Shadow (applies second)
└─ Effect: Glow (applies last)
```

**Reordering:** Drag effects up/down in the effects list.

**Order matters!** For example:
- Glow → Outline = Glow around outline
- Outline → Glow = Outline around glow

---

## Animating Effects

All effect parameters are animatable in Canvas Animation mode!

### How to Animate

1. Open Timeline (`T`)
2. Navigate to starting frame
3. Configure effect settings via <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_16.png" width="16">
4. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> Add keyframe to the layer
5. Navigate to ending frame
6. Change effect settings
7. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> Add another keyframe
8. Press <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Play to see animated effect!

### Animation Ideas

| Effect | Animation | Use Case |
|--------|-----------|----------|
| **Glow** | Intensity pulses | Power-ups, magic |
| **Drop Shadow** | Offset animates | Moving light source |
| **Outline** | Thickness pulses | Selection highlight |
| **Chromatic Aberration** | Offset spikes | Damage, glitch |
| **Vignette** | Intensity increases | Danger, focus |
| **Brightness** | Flash to white | Impacts, explosions |
| **Saturation** | Desaturate | Death, flashback |

---

## Performance Considerations

Effects are computed in real-time, which can impact performance:

### Performance Tips
- Disable effects while drawing (toggle with <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/glasses_16.png" width="16"> glasses icon)
- Fewer effects = faster rendering
- Large blur radii are expensive
- Animated grain is CPU-intensive

### Heavy Effects
Most demanding:
1. Large blur (Drop Shadow, Glow with big radius)
2. CRT (multiple sub-effects)
3. Animated Grain

Lightest:
1. Outline
2. Color Adjust
3. Scanlines

---

## See Also

- [[Layers]] - Layer management
- [[Canvas Animation|Canvas-Animation]] - Animation system
- [[Palette]] - Color palettes for Palette Quantize
