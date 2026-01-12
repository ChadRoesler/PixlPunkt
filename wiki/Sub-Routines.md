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
2. In the Timeline, click **Add Sub-Routine** <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16">
3. Select your tile animation reel
4. Position it on the canvas

### From Selection

1. Select frames in the timeline
2. Right-click → **Create Sub-Routine from Selection**
3. Frames become a reusable sub-routine

---

## Sub-Routine Properties

Each sub-routine instance has:

| Property | Description |
|----------|-------------|
| **Position** | X, Y coordinates on canvas |
| **Scale** | Size multiplier (0.1 - 10.0) |
| **Rotation** | Angle in degrees |
| **Opacity** | Transparency (0-255) |
| **Z-Order** | Layer ordering (front/back) |
| **Start Frame** | When sub-routine begins playing |
| **Loop** | Whether it repeats |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> Editing Sub-Routines

### Select a Sub-Routine

- Click on a sub-routine in the canvas
- Or click in the Timeline's sub-routine track
- Selected sub-routine shows transform handles

### Transform Handles

```
    ○─────────○
    │         │
    │    ●    │   ○ = Scale/Rotate handles
    │         │   ● = Center (move)
    ○─────────○
```

- **Corner handles** - Scale proportionally
- **Edge handles** - Scale in one direction
- **Outside corners** - Rotate
- **Center** - Move position

### Property Panel

With sub-routine selected:
- Adjust numeric values precisely
- Set exact position, scale, rotation
- Configure animation timing

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Sub-Routine Timing

### Start Frame

When the sub-routine begins playing relative to the main animation:
- **Frame 0** = Starts immediately
- **Frame 30** = Starts at frame 30 of main animation

### Duration

How long the sub-routine is visible:
- **Auto** = Matches source animation length
- **Custom** = Set specific frame count

### Looping

| Mode | Behavior |
|------|----------|
| **No Loop** | Plays once, then holds last frame |
| **Loop** | Repeats continuously |
| **Ping-Pong** | Plays forward, then backward |

---

## Z-Order (Layering)

Sub-routines have a Z-order relative to your layers:

```
Layer Stack:
─────────────
Foreground     ← Z: 3
Sub-Routine B  ← Z: 2.5 (between layers!)
Character      ← Z: 2
Sub-Routine A  ← Z: 1.5
Background     ← Z: 1
```

### Adjusting Z-Order

- Right-click sub-routine → **Move Forward** / **Move Backward**
- Or set exact Z value in properties
- Decimal values allow placement between layers

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/camera_16.png" width="16"> Animating Sub-Routines

Sub-routine properties can be **keyframed**:

### Adding Keyframes

1. Move playhead to desired frame
2. Adjust sub-routine property (position, scale, etc.)
3. Keyframe is automatically created

### Keyframeable Properties

- Position (X, Y)
- Scale
- Rotation
- Opacity

### Interpolation

Between keyframes, values are interpolated:
- **Linear** - Constant speed
- **Ease In/Out** - Smooth acceleration/deceleration
- **Hold** - Jump to next value (no interpolation)

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
├── Sub-Routine: Spark 1 (offset start, random position)
├── Sub-Routine: Spark 2 (different timing)
├── Sub-Routine: Smoke (delayed start, slower)
└── Sub-Routine: Flash (frame 0-5 only)
```

### UI Elements

```
Main Animation: Game HUD
├── Sub-Routine: Health Bar Pulse (when low)
├── Sub-Routine: Coin Spin (looping icon)
└── Sub-Routine: Alert Flash (triggered)
```

---

## Managing Sub-Routines

### Timeline View

Sub-routines appear as colored bars in the timeline:
- Bar position = Start frame
- Bar length = Duration
- Bar color = Sub-routine identity

### Sub-Routine List

In the Timeline panel:
- View all sub-routines
- Click to select
- Double-click to edit source
- Drag to reorder

### Editing Source

Double-click a sub-routine to edit its source animation:
- Opens the tile animation editor
- Changes affect ALL instances of this sub-routine

---

## Performance Tips

### Reuse Sub-Routines

Creating multiple instances of the same sub-routine is efficient:
- Only one source animation in memory
- Instances just store transform data

### Limit Active Sub-Routines

Too many simultaneous sub-routines can slow playback:
- Use `Start Frame` to stagger
- Remove when off-screen
- Consider baking to frames for export

### Resolution Matters

Sub-routine source should match your art scale:
- Don't use 256×256 sub-routine at 16×16 size
- Create appropriate resolution sources

---

## Exporting with Sub-Routines

When exporting animation:

### Baked Export (Default)

- Sub-routines are composited into final frames
- Output is flat frame sequence
- Compatible with all formats

### Sprite Sheet Export

- Each sub-routine can export separately
- Main animation + individual sub-routine sheets
- Combine in game engine for dynamic composition

---

## See Also

- [[Tile Animation|Tile-Animation]] - Creating source animations
- [[Canvas Animation|Canvas-Animation]] - Main animation system
- [[Stage]] - Camera and composition
- [[Animation Workflow|Animation-Workflow]] - Best practices
