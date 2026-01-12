# Tile Animation

Tile Animation <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_lightning_20.png" width="20"> is PixlPunkt's sprite-sheet-based animation system. Instead of storing full frames of pixel data, it sequences through tile coordinates - making it perfect for game sprites, memory-efficient animations, and sprite sheet workflows.

## Overview

| Aspect | Tile Animation |
|--------|----------------|
| **Best for** | Game sprites, repeated animations, sprite sheets |
| **How it works** | References tile positions, not full pixel data |
| **Memory** | Very efficient - reuses tile graphics |
| **Export** | GIF, video, sprite strips, image sequences |

---

## Key Concepts

### Tiles
Individual sprite frames arranged in a grid on your canvas. Each tile occupies a grid position (tileX, tileY).

### Reels
Named animation sequences. One document can have many reels:
- "Idle" reel: references tiles at positions (0,0), (1,0), (2,0), (1,0) (looping)
- "Walk" reel: references tiles at positions (0,1), (1,1), (2,1), (3,1)
- "Attack" reel: references tiles at positions (0,2), (1,2), (2,2)

### Frames
References to specific tile positions with optional custom timing.

---

## Creating a Tileset for Animation

### Draw in PixlPunkt
1. **File → New Canvas**
2. Set tile size (e.g., 32×32)
3. Set canvas to hold all frames (e.g., 8×4 tiles for 32 frames)
4. Draw each animation frame in a separate tile position

### Recommended Layout

```
┌────┬────┬────┬────┬────┬────┬────┬────┐
│Idle│Idle│Idle│Idle│Walk│Walk│Walk│Walk│  Row 0
├────┼────┼────┼────┼────┼────┼────┼────┤
│Walk│Walk│Walk│Walk│Run │Run │Run │Run │  Row 1
├────┼────┼────┼────┼────┼────┼────┼────┤
│Run │Run │Jump│Jump│Jump│Fall│Fall│Land│  Row 2
├────┼────┼────┼────┼────┼────┼────┼────┤
│Atk1│Atk2│Atk3│Atk4│Atk5│Hurt│Die1│Die2│  Row 3
└────┴────┴────┴────┴────┴────┴────┴────┘
```

---

## The Tile Animation Tool

Use the **Tile Animation Tool** to select tile positions for your reel:

**Shortcut:** Available in the Tiles tool group

### Using the Tile Animation Tool

1. Select the Tile Animation tool
2. **Click and drag** on the canvas to select a range of tiles
3. Tiles are added in **row-major order** (left-to-right, top-to-bottom)
4. Hold `Shift` while dragging to **add to existing** frames instead of replacing

### Adding Frames
- **Drag to select** tile positions on the canvas
- Selected tiles become frames in the current reel
- Frames reference the tile grid position (tileX, tileY)

---

## Creating and Managing Reels

### Creating a Reel

1. If no reel exists, one is created automatically when you select tiles
2. New reels can be created from the Tile Animation panel
3. Name your reel (e.g., "Walk_Right")

### Reel Operations

- **Add Reel** - Create a new empty reel
- **Rename** - Change the reel name
- **Delete** - Remove a reel
- **Duplicate** - Copy a reel with all its frames

---

## Frame Timing

Each frame has a duration in milliseconds:

### Default Frame Time
Each reel has a **DefaultFrameTimeMs** (default: 100ms) that applies to all frames unless overridden.

### Per-Frame Duration
Individual frames can have custom durations that override the default.

### Common Timings

| Feel | Duration | At 12 FPS |
|------|----------|-----------|
| Snappy | 50-80ms | ~1 frame |
| Normal | 80-120ms | ~1-1.5 frames |
| Slow | 120-200ms | ~1.5-2 frames |
| Hold | 200-500ms | ~2-6 frames |

---

## Reel Properties

### Loop
When enabled (default), the animation repeats forever: 1→2→3→1→2→3...

### Ping-Pong
When enabled, the animation bounces back and forth: 1→2→3→2→1→2→3...

---

## Playback Controls

### Playback States
- **Stopped** - Animation is at frame 0
- **Playing** - Animation is running
- **Paused** - Animation is paused at current frame

### Controls
- **Play** - Start or resume animation
- **Pause** - Pause at current frame
- **Stop** - Stop and return to frame 0
- **Next Frame** - Advance one frame
- **Previous Frame** - Go back one frame
- **First Frame** - Jump to frame 0
- **Last Frame** - Jump to final frame

---

## Onion Skinning

View previous and next frames as ghost overlays:

| Setting | Description | Default |
|---------|-------------|---------|
| **Enabled** | Toggle onion skin display | Off |
| **Frames Before** | Number of previous frames to show | 2 |
| **Frames After** | Number of next frames to show | 1 |
| **Opacity** | Ghost frame transparency | 30% |

---

## Reusing Tiles

One of Tile Animation's biggest advantages: **tile reuse**.

### Same Tile, Multiple Reels
The "idle" standing tile can appear in:
- Idle reel (repeated)
- Walk reel (start/end)
- Jump reel (ground frame)
- Any transition

Changes to the tile pixels update ALL reels automatically.

---

## Exporting Tile Animations

### Single Animation Export

**File → Export → Animation**

Select "Tile Animation" mode to export the currently selected reel:

| Format | Description |
|--------|-------------|
| **GIF** | Animated GIF with loop option |
| **MP4/WebM/MKV/AVI** | Video formats |
| **PNG/JPG Sequence** | Individual frame files |
| **Sub-routine (.pxpr)** | Reusable animation file with embedded pixels |

### Batch Export

**File → Export → Batch Tile Animation**

Export multiple reels at once:

1. Select which reels to export
2. Choose format:
   - **GIF** - Animated GIF per reel
   - **MP4** - Video per reel
   - **PNG Sequence** - Subfolder per reel with frame files
   - **Sprite Strip** - Horizontal PNG strip per reel
   - **Sub-routine (.pxpr)** - Portable animation file per reel

3. Configure options:
   - **Scale** - Output pixel scale (1x, 2x, etc.)
   - **Loop** - For GIF format
   - **Quality** - For video formats
   - **Override FPS** - Custom framerate

4. Select output folder
5. Files are named after reel names

### Sprite Strip Layout

Frames are arranged horizontally in a single PNG:
```
┌────┬────┬────┬────┬────┬────┐
│ F1 │ F2 │ F3 │ F4 │ F5 │ F6 │
└────┴────┴────┴────┴────┴────┘
```

### Sub-Routine Export (.pxpr)

Sub-routines contain embedded pixel data and can be:
- Imported into canvas animations
- Shared between projects
- Used without the original document

---

## Tips for Tile Animation

### Plan Your Tileset
- Group related animations together
- Leave room for expansion
- Use consistent tile grid positions

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
- Preview animations with Play
- Check loops for jarring transitions
- Verify timing feels right

### Reuse Everything
- Same idle frame for multiple states
- Shared anticipation frames
- Common impact frames

---

## Tile Animation vs Canvas Animation

| Feature | Tile Animation | Canvas Animation |
|---------|----------------|------------------|
| Memory usage | Low (reuses tiles) | High (full frames) |
| Frame independence | Each frame is a tile position | Each frame can be unique |
| Layer effects | Applied to whole canvas | Yes, per-keyframe |
| Stage/Camera | No | Yes |
| Best for | Game sprites | Cutscenes, complex animation |
| Export | Sprite sheets, GIF, video | Video, GIF, sequences |

**Use both together!** 
- Tile Animation <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_lightning_16.png" width="16"> for character sprites
- Canvas Animation <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> for cinematics
- Sub-routines to embed tile animations in canvas animations

---

## See Also

- [[Canvas Animation|Canvas-Animation]] - Frame-by-frame animation
- [[Tiles]] - Tileset management
- [[Tools]] - Tool documentation
