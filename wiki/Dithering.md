# Dithering

Understanding and using dithering algorithms in PixlPunkt.

---

## What Is Dithering?

Dithering creates the **illusion of more colors** by arranging pixels in patterns. With just 2 colors, you can simulate many shades:

```
0%     25%     50%     75%    100%
?????  ? ? ?  ? ? ?  ?????  ?????
?????  ? ? ?  ? ? ?  ?????  ?????
?????   ? ?   ?????   ? ?   ?????
```

Essential for:
- Limited color palettes
- Retro game aesthetics
- Smooth gradients
- Anti-aliasing effects

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/data_sunburst_16.png" width="16"> Dithering in Gradient Fill Tool

Access dithering via the **Gradient Fill** tool (`Shift+G`).

### Dithering Categories

| Category | Algorithms | Best For |
|----------|------------|----------|
| **Ordered** | Bayer, Checker, etc. | Regular patterns |
| **Error Diffusion** | Floyd-Steinberg, Atkinson, etc. | Photographic |
| **Stochastic** | Blue Noise | Natural randomness |

---

## Ordered Dithering

Creates **regular, repeating patterns**. Predictable and clean.

### Bayer Matrix

Classic dithering with threshold matrices:

**Bayer 2×2**
```
?????????
? 0 ? 2 ?
?????????
? 3 ? 1 ?
?????????
```
- Smallest pattern
- Most visible
- Best for very small sprites

**Bayer 4×4**
```
?????????????????
? 0 ? 8 ? 2 ?10 ?
?????????????????
?12 ? 4 ?14 ? 6 ?
?????????????????
? 3 ?11 ? 1 ? 9 ?
?????????????????
?15 ? 7 ?13 ? 5 ?
?????????????????
```
- Good balance
- **Most commonly used**
- Works at most sizes

**Bayer 8×8**
- Larger pattern
- Smoother gradients
- Better for larger areas

### Checker Pattern

Simple 2×2 alternating:
```
? ? ? ?
 ? ? ? 
? ? ? ?
 ? ? ?
```
- 50% coverage only
- Iconic retro look
- Very fast to compute

### Diagonal

Angled line pattern:
```
?   ?   ?
 ?   ?   ?
  ?   ?  
   ?   ?
```
- Creates diagonal stripes
- Good for fabric/material textures

### Crosshatch

Intersecting lines:
```
? ? ? ? ?
 ?   ?   
? ? ? ? ?
   ?   ?
? ? ? ? ?
```
- Classic illustration style
- Good for shadows

---

## Error Diffusion Dithering

Distributes quantization error to neighboring pixels. Creates **organic, irregular patterns**.

### Floyd-Steinberg

The classic error diffusion algorithm:

```
Distribution pattern:
      [*] 7/16
3/16  5/16  1/16
```

**Characteristics:**
- Very smooth gradients
- Slightly serpentine patterns
- Best for photographs
- Can create "worm" artifacts

### Atkinson

Apple Macintosh style:

```
Distribution pattern:
      [*] 1/8  1/8
1/8   1/8  1/8
      1/8
```

**Characteristics:**
- Only distributes 6/8 of error
- Higher contrast
- More "stark" appearance
- Iconic Mac look

### Jarvis-Judice-Ninke

Wider error distribution:

```
        [*]  7/48  5/48
3/48  5/48  7/48  5/48  3/48
1/48  3/48  5/48  3/48  1/48
```

**Characteristics:**
- Very smooth
- Less artifacts
- Slower to compute
- Best quality

### Sierra

Simplified JJN:

```
      [*]  5/32  3/32
2/32  4/32  5/32  4/32  2/32
      2/32  3/32  2/32
```

**Characteristics:**
- Good balance of quality and speed
- Less artifacts than Floyd-Steinberg

### Stucki

Another variation:

```
        [*]  8/42  4/42
2/42  4/42  8/42  4/42  2/42
1/42  2/42  4/42  2/42  1/42
```

**Characteristics:**
- Very smooth
- Good for complex images

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/star_16.png" width="16"> Riemersma Dithering

**Hilbert curve-based** error diffusion:

```
Follows space-filling curve:
????  ????
?  ?  ?  ?
?  ????  ?
?        ?
??????????
```

**Characteristics:**
- No directional bias
- Very high quality
- Unique organic pattern
- Best for artistic work
- More computationally intensive

---

## Stochastic Dithering

### Blue Noise

Random-looking but carefully distributed:

**Characteristics:**
- No visible patterns
- Natural appearance
- Good for film grain effects
- Mimics photographic noise

**Uses:**
- Smooth gradients
- Atmospheric effects
- Breaking up banding

---

## Choosing a Dithering Algorithm

### For Retro Games

| Style | Algorithm |
|-------|-----------|
| NES/GB | Bayer 2×2 or Checker |
| SNES/Genesis | Bayer 4×4 |
| PS1/Saturn | Floyd-Steinberg |

### For Art Style

| Effect | Algorithm |
|--------|-----------|
| Clean patterns | Bayer |
| Organic look | Atkinson |
| Smooth gradients | Floyd-Steinberg or JJN |
| No visible pattern | Blue Noise |
| Artistic/unique | Riemersma |

### For Performance

| Priority | Algorithm |
|----------|-----------|
| Fastest | Checker, Bayer 2×2 |
| Balanced | Bayer 4×4 |
| Quality | Floyd-Steinberg |
| Best quality | Riemersma, JJN |

---

## Dithering Settings

### Strength

Controls how much dithering is applied:
- **0%** = No dithering (hard edges)
- **50%** = Partial dithering
- **100%** = Full dithering

### Pattern Scale

For ordered dithering:
- **1×** = Original pattern size
- **2×** = Double-size pattern
- **4×** = Quadruple-size pattern

Larger patterns work better at higher resolutions.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_line_16.png" width="16"> Using with Gradient Tool

1. Select **Gradient Fill** (`Shift+G`)
2. Choose gradient type (Linear, Radial, etc.)
3. Select **Dithering** algorithm
4. Adjust **Strength** and **Scale**
5. Draw gradient on canvas

### Multi-Color Gradients

With palette colors:
- PixlPunkt uses your palette colors
- Dithers between adjacent colors
- Respects palette limitations

---

## Tips & Techniques

### Combine with Limited Palette

Dithering + limited colors = authentic retro look:
1. Reduce to target palette (16 colors, etc.)
2. Apply dithering
3. Creates illusion of more colors

### Selective Dithering

Don't dither everything:
- Solid colors for outlines
- Dithering for gradients/shading
- Clean edges + dithered fills

### Check at 1:1 Scale

Always verify dithering at actual size:
- Zoom out to 100%
- Pattern should be subtle
- If too visible, try different algorithm

### Consider Animation

Some dithering patterns "shimmer" when animated:
- Use consistent pattern
- Or embrace the flicker (CRT aesthetic)

---

## Examples

### Sky Gradient (Ordered Bayer 4×4)
Clean, regular pattern good for backgrounds.

### Skin Shading (Atkinson)
High contrast, good for character art.

### Smoke/Fog (Blue Noise)
Organic, no pattern, atmospheric.

### Metal Sheen (Floyd-Steinberg)
Smooth gradients for reflective surfaces.

---

## See Also

- [[Gradient Fill|Gradient-Fill]] - Gradient tool details
- [[Palette]] - Color management
- [[Game Art|Game-Art]] - Retro game aesthetics
- [[Tools]] - All tools reference
