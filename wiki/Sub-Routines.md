# Animation Sub-Routines

Nested animations and reusable animation clips in PixlPunkt.

---

## What Are Sub-Routines?

Sub-routines are **embedded animations within your main animation**:

- A character animation that can be placed anywhere
- A particle effect that loops independently
- A UI element that animates separately
- Any reusable animated element

Think of them as "animation instances" - define once, use many times.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/video_clip_16.png" width="16"> Creating a Sub-Routine

### From Tile Animation

1. Create a [[Tile Animation|Tile-Animation]] reel
2. Export as Sub-Routine (.pxpr) via **File → Export → Animation**
3. Import the .pxpr file into your canvas animation
4. Position it on the canvas

### Importing a Sub-Routine

1. In Canvas Animation mode, use **File → Import → Sub-Routine**
2. Select a .pxpr file
3. Sub-routine appears on canvas at the import position

---

## Sub-Routine Properties

Each sub-routine instance has:

| Property | Description |
|----------|-------------|
| **Position** | X, Y coordinates on canvas |
| **Progress** | Playback position (0.0 - 1.0) |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> Editing Sub-Routines

### Select a Sub-Routine

- Click on a sub-routine in the canvas
- Selected sub-routine shows selection handles

### Moving Sub-Routines

```
┌─────────────┐
│             │
│      ●      │   ● = Center (drag to move)
│             │
└─────────────┘
```

- **Drag** to reposition the sub-routine on the canvas

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Sub-Routine Timing

### Playback

Sub-routines contain their own frame timing from the original tile animation:
- Each frame has its duration in milliseconds
- Loop and Ping-Pong settings are preserved from the source reel

### Progress

The progress value (0.0 - 1.0) controls where in the animation the sub-routine is:
- **0.0** = Start of animation
- **0.5** = Middle of animation
- **1.0** = End of animation

---

## Use Cases

### Character with Separate Parts

```
Main Animation: Character Body
├── Sub-Routine: Blinking Eyes (loops every 3 seconds)
├── Sub-Routine: Breathing Chest (subtle loop)
└── Sub-Routine: Swaying Hair (physics-like loop)
```

### Particle Effects

```
Main Animation: Explosion
├── Sub-Routine: Spark 1 (offset position)
├── Sub-Routine: Spark 2 (different timing)
├── Sub-Routine: Smoke (delayed start)
└── Sub-Routine: Flash (quick burst)
```

### UI Elements

```
Main Animation: Game HUD
├── Sub-Routine: Health Bar Pulse
├── Sub-Routine: Coin Spin (looping icon)
└── Sub-Routine: Alert Flash
```

---

## Sub-Routine File Format (.pxpr)

Sub-routines are saved as `.pxpr` files containing:

| Data | Description |
|------|-------------|
| **Frame Dimensions** | Width and height of each frame |
| **Embedded Pixels** | BGRA pixel data for each frame |
| **Frame Timing** | Duration in milliseconds per frame |
| **Loop Settings** | Loop and Ping-Pong flags |
| **Reel Name** | Original animation name |

This format makes sub-routines **portable** - they can be used in any document without needing the original source file.

---

## Performance Tips

### Reuse Sub-Routines

Creating multiple instances of the same sub-routine is efficient:
- Source animation is loaded once
- Instances share the pixel data

### Resolution Matters

Sub-routine frame size should match your intended display size:
- Don't import large sub-routines if displaying small
- Create appropriate resolution sources

---

## Exporting with Sub-Routines

When exporting canvas animation:

### Baked Export (Default)

- Sub-routines are composited into final frames
- Output is flat frame sequence
- Compatible with all export formats (GIF, video, image sequence)

---

## See Also

- [[Tile Animation|Tile-Animation]] - Creating source animations
- [[Canvas Animation|Canvas-Animation]] - Main animation system
- [[Stage]] - Camera and composition
