# Tile Animation

Tile Animation <img src="../docs/assets/icons/table_lightning_20.png" width="20"> is PixlPunkt's sprite-sheet-based animation system. Instead of storing full frames of pixel data, it sequences through tile coordinates - making it perfect for game sprites, memory-efficient animations, and sprite sheet workflows.

## Overview

| Aspect | Tile Animation |
|--------|----------------|
| **Best for** | Game sprites, repeated animations, sprite sheets |
| **How it works** | References tile positions, not full pixel data |
| **Memory** | Very efficient - reuses tile graphics |
| **Export** | Sprite sheets for game engines |

---

## Key Concepts

### Tiles
Individual sprite frames arranged in a grid (your tileset).

### Reels
Named animation sequences. One tileset can have many reels:
- "Idle" reel: tiles 0, 1, 2, 1 (looping)
- "Walk" reel: tiles 3, 4, 5, 6, 7, 8
- "Jump" reel: tiles 9, 10, 11, 12
- "Attack" reel: tiles 13, 14, 15, 16, 17

### Frames
References to specific tiles with timing information.

---

## Creating a Tileset for Animation

### Method 1: Draw in PixlPunkt
1. **File ? New Canvas**
2. Set tile size (e.g., 32×32)
3. Set canvas to hold all frames (e.g., 8×4 tiles for 32 frames)
4. Draw each animation frame in a separate tile

### Method 2: Import Existing
1. **File ? Import Tileset**
2. Select your sprite sheet image
3. Set the tile size to match your frames
4. PixlPunkt slices it automatically

### Recommended Layout

```
?????????????????????????????????????????
?Idle?Idle?Idle?Idle?Walk?Walk?Walk?Walk?  Row 1
?????????????????????????????????????????
?Walk?Walk?Walk?Walk?Run ?Run ?Run ?Run ?  Row 2
?????????????????????????????????????????
?Run ?Run ?Jump?Jump?Jump?Fall?Fall?Land?  Row 3
?????????????????????????????????????????
?Atk1?Atk2?Atk3?Atk4?Atk5?Hurt?Die1?Die2?  Row 4
?????????????????????????????????????????
```

---

## The Tile Animation Panel

### Opening
- **View ? Tile Animation** or use the Tiles panel toggle

### Interface

```
???????????????????????????????????????????????????????????????????
? Reel: [Walk ?] ? + New ? ? Rename ? ?? Delete ? ? Preview      ?
???????????????????????????????????????????????????????????????????
? Frame 1 ? Frame 2 ? Frame 3 ? Frame 4 ? Frame 5 ? Frame 6 ? + ?
? [tile]  ? [tile]  ? [tile]  ? [tile]  ? [tile]  ? [tile]  ?   ?
?  100ms  ?  100ms  ?  100ms  ?  100ms  ?  100ms  ?  100ms  ?   ?
???????????????????????????????????????????????????????????????????
? Tileset Preview (click to add frames)                          ?
? ?????????????????????????                                      ?
? ?0 ?1 ?2 ?3 ?4 ?5 ?6 ?7 ?                                      ?
? ?????????????????????????                                      ?
? ?8 ?9 ?10?11?12?13?14?15?                                      ?
? ?????????????????????????                                      ?
???????????????????????????????????????????????????????????????????
```

---

## Creating a Reel

1. Click <img src="../docs/assets/icons/add_16.png" width="16"> **+ New** to create a new reel
2. Name it (e.g., "Walk_Right")
3. Click tiles in the tileset preview to add frames
4. Adjust timing for each frame
5. Click <img src="../docs/assets/icons/play_16.png" width="16"> **Preview** to test

---

## Frame Operations

### Adding Frames
- **Click a tile** in the tileset preview to add it to the reel
- Frames are added at the end by default
- Hold `Shift` to insert at current position

### Removing Frames
- **Right-click a frame** ? Delete
- Or select and press `Delete`

### Reordering Frames
- **Drag frames** left/right to reorder

### Duplicating Frames
- **Right-click** ? Duplicate
- Useful for holds (same tile, different timing)

---

## Frame Timing

Each frame has an independent duration:

| Timing Type | Description |
|-------------|-------------|
| **Milliseconds** | Exact time (e.g., 100ms) |
| **Frames** | At target FPS (e.g., 2 frames at 12fps = 167ms) |

### Setting Timing
1. Click the timing field under a frame
2. Enter duration in ms
3. Or right-click ? Set Duration

### Common Timings

| Feel | Duration | At 12 FPS |
|------|----------|-----------|
| Snappy | 50-80ms | ~1 frame |
| Normal | 80-120ms | ~1-1.5 frames |
| Slow | 120-200ms | ~1.5-2 frames |
| Hold | 200-500ms | ~2-6 frames |

---

## Reel Properties

### Loop Mode

| Mode | Behavior |
|------|----------|
| **Loop** <img src="../docs/assets/icons/arrow_repeat_all_16.png" width="16"> | Repeats forever (1?2?3?1?2?3...) |
| **Ping-Pong** | Bounces back and forth (1?2?3?2?1?2?3...) |
| **Once** | Plays once and stops |

### Playback Direction
- **Forward** - Normal order
- **Reverse** - Backwards
- **Random** - Random frame each cycle (for variety)

---

## Reusing Tiles

One of Tile Animation's biggest advantages: **tile reuse**.

### Same Tile, Multiple Reels
The "idle" standing frame can appear in:
- Idle reel (repeated)
- Walk reel (start/end)
- Jump reel (ground frame)
- Any transition

Changes to the tile update ALL reels automatically.

### Mirrored Animations
Instead of drawing Walk_Left separately:
1. Draw Walk_Right
2. Create Walk_Left reel with same tiles
3. Set <img src="../docs/assets/icons/flip_horizontal_16.png" width="16"> **Flip Horizontal** on the reel
4. Half the work, same result

---

## Previewing Animations

### In-Panel Preview
Click <img src="../docs/assets/icons/play_16.png" width="16"> **Preview** to loop the current reel in the panel.

### On-Canvas Preview
1. Use the Tile Stamper tool <img src="../docs/assets/icons/table_edit_16.png" width="16"> (`Shift+A`)
2. Select your reel
3. Stamp on canvas
4. Animation plays live on the canvas

### Preview Options
- **Speed multiplier** - 0.5x, 1x, 2x playback
- **Loop** <img src="../docs/assets/icons/arrow_repeat_all_16.png" width="16"> - Toggle looping
- **Show frame number** - Overlay current frame

---

## Exporting Tile Animations

### As Sprite Sheet

1. **File ? Export Sprite Sheet**
2. Configure layout:
   - **Columns** - Frames per row
   - **Padding** - Space between frames
   - **Include all reels** - Or select specific ones
3. Choose format (PNG recommended)
4. Export

### Sprite Sheet Layouts

**Horizontal Strip:**
```
???????????????????????????????
? F1 ? F2 ? F3 ? F4 ? F5 ? F6 ?
???????????????????????????????
```

**Grid:**
```
?????????????????????
? F1 ? F2 ? F3 ? F4 ?
?????????????????????
? F5 ? F6 ? F7 ? F8 ?
?????????????????????
```

### As Animation File

1. **File ? Export Animation**
2. Choose format:
   - **GIF** - Simple, widely supported
   - **APNG** - PNG with animation, better quality
   - **WebP** - Modern, good compression

### As Individual Frames

1. **File ? Export Frames**
2. Files named: `reel_name_001.png`, `reel_name_002.png`, etc.

### Reel Data Export

Export timing data for game engines:

```json
{
  "walk": {
    "frames": [3, 4, 5, 6, 7, 8],
    "durations": [100, 100, 100, 100, 100, 100],
    "loop": true
  }
}
```

Supported formats:
- JSON (Unity, Godot, custom engines)
- XML (various engines)
- Aseprite (.ase)

---

## Game Engine Integration

### Unity

1. Export as sprite sheet + JSON
2. Import sprite sheet, set to Multiple
3. Use Sprite Editor to slice
4. Create Animation Clips from the JSON timing

### Godot

1. Export as sprite sheet
2. Create AnimatedSprite node
3. Add SpriteFrames resource
4. Import frames with timing

### GameMaker

1. Export as horizontal sprite strip
2. Create Sprite, set frames
3. Set frame timing in sprite properties

### Pygame / Custom

1. Export as sprite sheet + JSON
2. Load sprite sheet as single image
3. Parse JSON for frame rects and timing
4. Blit appropriate region each frame

---

## Tips for Tile Animation

### Plan Your Tileset
- Group related animations together
- Leave room for expansion
- Consider mirroring needs upfront

### Consistent Frame Counts
Standard frame counts make looping easier:
- Walk: 6-8 frames
- Run: 6-8 frames
- Idle: 4-6 frames
- Attack: 4-8 frames

### Variable Timing > More Frames
Instead of adding frames for slow motion:
- Increase frame duration for holds
- Use fewer frames with strategic timing
- Saves memory and work

### Test Early and Often
- Preview animations constantly with <img src="../docs/assets/icons/play_16.png" width="16">
- Check loops for jarring transitions
- Verify timing feels right at game speed

### Reuse Everything
- Same idle frame for multiple states
- Shared anticipation frames
- Common impact frames

---

## Tile Animation vs Canvas Animation

| Feature | Tile Animation | Canvas Animation |
|---------|----------------|------------------|
| Memory usage | Low (reuses tiles) | High (full frames) |
| Frame independence | Each frame is a tile | Each frame can be unique |
| Layer effects | No | Yes, fully animatable |
| Camera/Stage | No | Yes |
| Best for | Game sprites | Cutscenes, complex animation |
| Export | Sprite sheets | Video, GIF, sequences |

**Use both together!** 
- Tile Animation <img src="../docs/assets/icons/table_lightning_16.png" width="16"> for character sprites
- Canvas Animation <img src="../docs/assets/icons/play_16.png" width="16"> for cinematics
- Sub-Routines to embed tile animations in canvas animations

---

## See Also

- [[Canvas Animation|Canvas-Animation]] - Frame-by-frame animation
- [[Sub-Routines]] - Embedding tile animations in canvas animations
- [[Tiles]] - Tileset management
- [[Game Art|Game-Art]] - Creating game-ready assets
