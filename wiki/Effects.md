# Layer Effects

Layer effects <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/image_sparkle_20.png" width="20"> are non-destructive filters applied to layers. They can be toggled on/off, adjusted anytime, and fully animated - making them incredibly powerful for both static art and animation.

## Overview

Effects are applied per-layer and render in real-time. The original pixel data is never modified - effects are computed on top.

**Key benefits:**
- ? Non-destructive (always reversible)
- ? Animatable (change settings over time)
- ? Stackable (multiple effects per layer)
- ? Reorderable (drag to change effect order)

---

## Accessing Layer Effects

1. **Double-click** a layer to open settings
2. Scroll to **Effects** section
3. Check the box next to an effect to enable it
4. Expand the effect to configure parameters

Or:
- **Right-click layer ? Effects**
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
| **Color** | Any | Shadow color (default: black) |
| **Offset X** | -100 to 100 | Horizontal displacement |
| **Offset Y** | -100 to 100 | Vertical displacement |
| **Blur** | 0-50 | Shadow softness |
| **Spread** | 0-50 | Shadow expansion before blur |
| **Opacity** | 0-100% | Shadow transparency |

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
| **Thickness** | 1-10 | Stroke width in pixels |
| **Position** | Inside/Center/Outside | Where the outline draws |
| **Opacity** | 0-100% | Outline transparency |

**Tips:**
- 1px outside outline is classic pixel art style
- Use contrasting colors for readability
- Inside position preserves sprite size

---

#### <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/sparkle_20.png" width="20"> Glow / Bloom
Creates a soft light emission effect.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Color** | Any | Glow color (or use source colors) |
| **Radius** | 1-50 | How far glow extends |
| **Intensity** | 0-200% | Glow brightness |
| **Threshold** | 0-255 | Brightness level to start glowing |
| **Use Source Color** | On/Off | Glow matches pixel colors |

**Tips:**
- Low threshold = more pixels glow
- High intensity can blow out to white
- Great for magic, fire, UI highlights

---

#### Chromatic Aberration
Separates RGB channels for a glitch/retro look.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Offset X** | -20 to 20 | Horizontal channel separation |
| **Offset Y** | -20 to 20 | Vertical channel separation |
| **Red Offset** | -10 to 10 | Red channel specific offset |
| **Blue Offset** | -10 to 10 | Blue channel specific offset |

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
| **Spacing** | 1-10 | Pixels between lines |
| **Thickness** | 1-5 | Line thickness |
| **Opacity** | 0-100% | Line darkness |
| **Color** | Any | Line color (default: black) |
| **Offset** | 0-10 | Vertical position offset |

**Tips:**
- Spacing 2, Thickness 1 for classic CRT
- Animate offset for scrolling effect
- Combine with CRT effect for full retro

---

#### Grain
Adds film-like noise texture.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Amount** | 0-100% | Noise intensity |
| **Size** | 1-5 | Grain particle size |
| **Monochrome** | On/Off | Color or grayscale grain |
| **Animated** | On/Off | Grain changes each frame |

**Tips:**
- 10-20% amount for subtle texture
- Enable Animated for film look in animations
- Combine with Color Adjust for vintage feel

---

#### Vignette
Darkens edges of the frame.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Intensity** | 0-100% | How dark the edges get |
| **Radius** | 0-100% | Size of the clear center |
| **Softness** | 0-100% | Edge falloff smoothness |
| **Shape** | Circle/Square | Vignette shape |
| **Color** | Any | Vignette color (default: black) |

**Tips:**
- Creates focus on center content
- Animate intensity for dramatic moments
- Use colored vignettes for mood

---

#### CRT
Simulates old CRT monitor effects.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Curvature** | 0-50% | Screen bend amount |
| **Scanline Intensity** | 0-100% | Built-in scanlines |
| **RGB Mask** | On/Off | Subpixel pattern |
| **Bloom** | 0-50% | Glow around bright areas |
| **Flicker** | 0-20% | Brightness variation |

**Tips:**
- Full retro look in one effect
- Subtle settings for tasteful nostalgia
- Heavy settings for broken TV aesthetic

---

#### Pixelate
Reduces apparent resolution.

| Parameter | Range | Description |
|-----------|-------|-------------|
| **Block Size** | 2-32 | Pixel cluster size |
| **Preserve Edges** | On/Off | Keeps hard edges sharper |

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
| **Hue Shift** | -180� to 180� | Rotate all colors |
| **Saturation** | -100% to 100% | Color intensity |
| **Brightness** | -100% to 100% | Lighten/darken |
| **Contrast** | -100% to 100% | Tonal range |
| **Gamma** | 0.1-3.0 | Midtone adjustment |

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
| **Character Set** | Various | Which characters to use |
| **Cell Size** | 4-16 | Size of each character |
| **Color Mode** | Mono/Color | Preserve colors or not |
| **Background** | On/Off | Fill background |

**Tips:**
- Fun stylistic effect
- Works best on high-contrast images
- Animate character set for glitch effect

---

## Effect Stacking

Effects apply in order from top to bottom:

```
Layer "Character"
?? Effect: Outline (applies first)
?? Effect: Drop Shadow (applies second)
?? Effect: Glow (applies last)
```

**Reordering:** Drag effects up/down in the effects list.

**Order matters!** For example:
- Glow ? Outline = Glow around outline
- Outline ? Glow = Outline around glow

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
| **Glow** | Color cycles | Energy effects |
| **Drop Shadow** | Offset animates | Moving light source |
| **Outline** | Thickness pulses | Selection highlight |
| **Scanlines** | Offset scrolls | Retro TV effect |
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

## Effect Presets

Save and load effect combinations:

### Saving a Preset
1. Configure effects the way you want
2. Right-click effects header ? **Save Preset**
3. Name your preset

### Loading a Preset
1. Right-click effects header ? **Load Preset**
2. Select from your saved presets
3. Effects are applied to current layer

### Built-in Presets
- "Classic Sprite" - 1px outline
- "Retro CRT" - Scanlines + CRT
- "Neon Glow" - Bright glow effect
- "Cinematic" - Vignette + Color Adjust

---

## See Also

- [[Layers]] - Layer management basics
- [[Canvas Animation|Canvas-Animation]] - Animating layers and effects
- [[Masks]] - Layer masking
