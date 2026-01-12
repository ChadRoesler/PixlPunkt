# PixlPunkt Quick Start Guide

Get up and running with PixlPunkt in 5 minutes!

---

## 1. Create Your First Canvas

1. Launch PixlPunkt
2. Press `Ctrl+N` or go to **File → New Canvas**
3. Enter these settings:
   - **Name**: `MyFirstSprite`
   - **Tile Size**: `16 × 16` (good for most pixel art)
   - **Canvas**: `1 × 1` tiles (for a single 16×16 sprite)
4. Click **Create**

You now have a blank 16×16 canvas!

---

## 2. Learn the Essential Tools

| Tool | Key | Icon | What it does |
|------|-----|:----:|--------------|
| **Brush** | `B` | <img src="assets/icons/edit_16.png" width="16"> | Paint pixels |
| **Eraser** | `E` | <img src="assets/icons/eraser_16.png" width="16"> | Remove pixels |
| **Fill** | `G` | <img src="assets/icons/paint_bucket_16.png" width="16"> | Fill areas |
| **Gradient Fill** | `Shift+G` | <img src="assets/icons/data_sunburst_16.png" width="16"> | Fill with dithered gradients |
| **Dropper** | Right-click | <img src="assets/icons/eyedropper_16.png" width="16"> | Pick colors |
| **Pan** | Space + drag | <img src="assets/icons/hand_left_16.png" width="16"> | Move around |
| **Zoom** | Ctrl + scroll | <img src="assets/icons/zoom_in_16.png" width="16"> | Zoom in/out |

### Quick Tips:
- Press `[` and `]` to change brush size
- Press `X` to swap foreground/background colors
- Hold `Shift` while drawing for straight lines

---

## 3. Pick Your Colors

### From the Palette
- **Left-click** a swatch → foreground color
- **Right-click** a swatch → background color

### From the Canvas
- **Right-click** anywhere → pick that color

### Custom Color
1. Click the large color square (foreground color)
2. Use the color picker to choose exactly what you want
3. Click <img src="assets/icons/add_16.png" width="16"> in the palette to save it

---

## 4. Draw Something or don't, I'm a quickstart not a cop.

Try making a simple character:

1. Select **Brush** (`B`) <img src="assets/icons/edit_16.png" width="16">
2. Pick a skin tone from the palette
3. Draw the face outline
4. Use **Fill** (`G`) <img src="assets/icons/paint_bucket_16.png" width="16"> to fill the inside
5. Add eyes, hair, and details
6. Use **Eraser** (`E`) <img src="assets/icons/eraser_16.png" width="16"> if you make mistakes

Remember: `Ctrl+Z` undoes mistakes!

---

## 5. Create a Sick Gradient Background

The **Gradient Fill** tool (`Shift+G`) <img src="assets/icons/data_sunburst_16.png" width="16"> is perfect for skies, sunsets, and atmospheric backgrounds:

1. Add a new layer (click <img src="assets/icons/add_16.png" width="16"> in Layers panel)
2. Drag it below your character layer
3. Press `Shift+G` for Gradient Fill
4. Choose your gradient type: **Linear**, **Radial**, **Angular**, or **Diamond**
5. Select colors: pick a preset or create a **Custom** gradient
6. Choose a dithering style (see below!)
7. Click and drag across your canvas to apply

### Gradient Types

| Type | Best For |
|------|----------|
| **Linear** | Skies, horizons, backgrounds |
| **Radial** | Explosions, light sources, vignettes |
| **Angular** | Color wheels, rainbow effects |
| **Diamond** | Spotlights, centered effects |

### Dithering Options Explained

Dithering creates smooth transitions between colors using patterns - essential for that authentic pixel art look!

#### Ordered Dithering (Consistent Patterns)
| Style | Pattern | Best For |
|-------|---------|----------|
| **Bayer 2×2** | Tiny checkerboard | Small sprites, tight spaces |
| **Bayer 4×4** | Classic retro pattern | General use, NES/SNES style |
| **Bayer 8×8** | Large, visible pattern | Dramatic backgrounds, large art |
| **Checker** | Simple alternating | Clean, minimal gradients |
| **Diagonal** | Angled lines | Stylized shading, rain effects |
| **Crosshatch** | Grid-like hatching | Textured, hand-drawn look |

#### Error Diffusion (Organic, Smooth)
| Style | Characteristics | Best For |
|-------|-----------------|----------|
| **Floyd-Steinberg** | Balanced diffusion, slight horizontal bias | Smooth gradients, photographs |
| **Atkinson** | Lighter, more contrast | High-contrast art, Mac classic style |
| **Riemersma** | Follows Hilbert curve, no streaks | Natural gradients, best quality |

#### Stochastic (Random)
| Style | Characteristics | Best For |
|-------|-----------------|----------|
| **Blue Noise** | Evenly distributed randomness | Subtle gradients, film grain look |

### Dithering Strength & Scale
- **Strength** (0-100%): Controls how much dithering is applied
  - Lower = smoother transitions, more colors
  - Higher = more visible pattern, fewer colors
- **Scale** (1x-4x): Size of the dithering pattern
  - 1x = fine detail
  - 4x = chunky, bold patterns

### Quick Gradient Recipes

**Sunset Sky:**
- Type: Linear (top to bottom)
- Colors: Orange → Pink → Purple
- Dithering: Bayer 4×4 at 70%

**Retro Vignette:**
- Type: Radial (center out)
- Colors: Transparent → Black
- Dithering: Blue Noise at 50%

**Ocean Depth:**
- Type: Linear (top to bottom)
- Colors: Light blue → Dark blue → Black
- Dithering: Riemersma at 60%

---

## 6. Use Layers

Layers let you separate parts of your artwork:

1. Look at the **Layers panel** (right side)
2. Click <img src="assets/icons/add_16.png" width="16"> to add a new layer
3. Draw the background on one layer, character on another
4. Toggle visibility with the <img src="assets/icons/eye_16.png" width="16"> eye icon
5. Reorder by dragging

**Why use layers?**
- Edit parts independently
- Try different backgrounds easily
- Apply effects to specific elements

---

## 7. Add Some Effects

Make your art pop with layer effects:

1. Double-click a layer to open settings
2. Scroll to **Effects**
3. Enable **Outline** - adds a border around your sprite
4. Enable **Drop Shadow** - gives depth
5. Tweak the settings to your liking

Effects are non-destructive - toggle them on/off anytime with <img src="assets/icons/glasses_16.png" width="16">!

---

## 8. Animate Your Sprites

PixlPunkt has two animation systems for different needs:

### Canvas Animation (Frame-by-Frame)
Best for: Character animations, complex scenes, cutscenes

This is traditional frame-by-frame animation like Aseprite:

1. Open the **Timeline** panel (View → Timeline or press `T`)
2. You'll see your layers on the left, frames across the top
3. Click the <img src="assets/icons/add_16.png" width="16"> button to add a new frame
4. Draw your next pose on the new frame
5. Use **Onion Skinning** <img src="assets/icons/layer_diagonal_16.png" width="16"> to see previous/next frames
6. Press **Play** <img src="assets/icons/play_16.png" width="16"> to preview your animation
7. Adjust frame timing by right-clicking a frame

#### Canvas Animation Tips:
- **Duplicate frames**: Right-click → Duplicate for small changes
- **Onion skin opacity**: Adjust how visible ghost frames are
- **Loop modes**: Set to loop, ping-pong, or play once
- **Layer keyframes**: Animate layer properties like opacity and effects!

#### Stage System (Camera Animation)
The Stage <img src="assets/icons/camera_16.png" width="16"> lets you animate camera movement:

1. Enable **Stage Mode** in the Timeline
2. Add **Stage Keyframes** at different points
3. Set **Position**, **Zoom**, and **Rotation** for each keyframe
4. PixlPunkt interpolates smoothly between them!

Great for: Panning across a scene, zoom-ins for emphasis, screen shake

#### Audio Tracks
Sync your animation to music or sound effects:

1. Click **Add Audio Track** in the Timeline
2. Import an audio file (.mp3, .wav, .ogg)
3. See the **waveform** displayed on the timeline
4. Align your keyframes to beats and sound cues

### Tile Animation (Sprite Sheets)
Best for: Game sprites, repeated animations, memory-efficient art

Tile animation uses coordinates in a tileset rather than full frames:

1. Create or import a **Tileset** (a grid of animation frames)
2. Open the **Tile Animation** panel <img src="assets/icons/table_lightning_16.png" width="16">
3. Create a new **Reel** (an animation sequence)
4. Add frames by clicking tiles in your tileset
5. Set **duration** for each frame (in milliseconds)
6. Preview with the **Play** <img src="assets/icons/play_16.png" width="16"> button

#### Tile Animation Tips:
- **Reuse frames**: The same tile can appear in multiple animations
- **Variable timing**: Each frame can have different duration
- **Export**: Generate sprite sheets for game engines
- **Multiple reels**: Create walk, run, jump, idle from one tileset

### Animation Quick Reference

| Task | Canvas Animation | Tile Animation |
|------|------------------|----------------|
| Add frame | Timeline <img src="assets/icons/add_16.png" width="16"> button | Click tile in reel editor |
| Preview | <img src="assets/icons/play_16.png" width="16"> Play button | <img src="assets/icons/play_16.png" width="16"> Play button |
| Set timing | Right-click frame | Duration field |
| Loop | Loop mode dropdown | Loop toggle |
| Export | File → Export Animation | File → Export Sprite Sheet |

### Exporting Animations

**As GIF:**
1. File → Export Animation → GIF
2. Set loop count (0 = infinite)
3. Adjust quality/colors if needed

**As Video (MP4/AVI):**
1. File → Export Animation → Video
2. Choose format and quality
3. Great for sharing on social media!

**As Sprite Sheet:**
1. File → Export Sprite Sheet
2. Configure rows/columns
3. Perfect for game engines

---

## 9. Save Your Work

### Quick Save
Press `Ctrl+S` to save as `.pxp` (PixlPunkt format)

### Export Image
1. Press `Ctrl+E` or **File → Export**
2. Choose **PNG** format (best for pixel art)
3. Set **Scale** (1x, 2x, 4x, etc.)
4. Pick a location and save

---

## 10. Keyboard Shortcuts Cheat Sheet

```
File                    Tools
═════════════          ═════════════════
Ctrl+N  New            B        Brush
Ctrl+O  Open           E        Eraser  
Ctrl+S  Save           G        Fill
Ctrl+E  Export         Shift+G  Gradient Fill
                       U        Blur
Edit                   J        Jumble
═════════════          M        Select Rect
Ctrl+Z  Undo           
Ctrl+Y  Redo           Animation
Ctrl+C  Copy           ═════════════
Ctrl+V  Paste          T        Toggle Timeline
                       Space    Play/Pause
View                   ,        Previous Frame
═════════════          .        Next Frame
Ctrl+0  Fit screen     
Ctrl+1  Actual size    Brush Size
                       ═════════════
Colors                 [  Smaller
═════════════          ]  Larger
X  Swap FG/BG          
Right-click  Pick      
```

---

## 11. Next Steps

Now that you know the basics:

- **Read the full [User Guide](USER_GUIDE.md)** for all features
- **Master Gradient Fill** <img src="assets/icons/data_sunburst_16.png" width="16"> - experiment with all dithering styles
- **Try layer effects** <img src="assets/icons/image_sparkle_16.png" width="16"> like Glow, Scanlines, and CRT
- **Create an animation** - start with a simple 4-frame walk cycle
- **Use the Stage** <img src="assets/icons/camera_16.png" width="16"> for cinematic camera movements
- **Import Aseprite files** - bring in existing work

---

## Need Help?

- **Undo everything**: Press `Ctrl+Z` repeatedly <img src="assets/icons/arrow_undo_16.png" width="16">
- **Reset tool**: Press the tool key again (e.g., `B` for Brush)
- **Stuck zoomed in**: Press `Ctrl+0` to fit canvas to screen
- **Animation not playing**: Check frame durations aren't set to 0
- **Lost your work**: Check **File → Recent** for auto-backups
- **Full documentation**: [User Guide](USER_GUIDE.md)

---

<p align="center">
  <strong>Now go make some pixel art magic! ✨</strong>
</p>