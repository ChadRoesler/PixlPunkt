# Canvas Animation

Canvas Animation is PixlPunkt's professional, layer-based animation system. Each layer can have independent keyframes that store complete state snapshots - perfect for complex character animation, cutscenes, and any frame-by-frame work.

## Overview

| Aspect | Canvas Animation |
|--------|------------------|
| **Best for** | Complex animation, cutscenes, effects |
| **How it works** | Full keyframes with pixel data per layer |
| **Interpolation** | Hold (values stay constant between keyframes) |
| **Layer effects** | Fully animatable |
| **Camera** | Stage system for pan/zoom/rotate |

---

## The Timeline Panel

Open the Timeline with **View → Timeline** or press `T`.

```
┌─────────────────────────────────────────────────────────────────────┐
│ ⏮ ◀ ▶ ⏸ ▶ ⏭ ⏹ │ Frame: 0/24 │ FPS: 12 │ 👻 Onion │ ◆+ Add Key │
├─────────────────────────────────────────────────────────────────────┤
│ Layer 1    │ ◆─────────────◆─────────────◆────────────────────────│
│ Layer 2    │ ◆─────────────────────────◆──────────────────────────│
│ Background │ ◆───────────────────────────────────────────────────-│
├─────────────────────────────────────────────────────────────────────┤
│ 0    5    10    15    20    25    30    35    40    45    50      │
└─────────────────────────────────────────────────────────────────────┘
```

- **◆** = Keyframe (diamond icon)
- **─** = Hold (keyframe values persist)
- Each layer has its own keyframe track

---

## Timeline Toolbar Buttons

| Icon | Name | Action |
|:----:|------|--------|
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/previous_20.png" width="20"> | **First Frame** | Jump to frame 0 |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/previous_20.png" width="20"> | **Previous Frame** | Go back one frame |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_20.png" width="20"> | **Play/Pause** | Toggle animation playback |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/next_20.png" width="20"> | **Next Frame** | Advance one frame |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/next_20.png" width="20"> | **Last Frame** | Jump to the last frame |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/stop_20.png" width="20"> | **Stop** | Stop playback and return to frame 0 |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_20.png" width="20"> | **Add Keyframe** | Create a keyframe at the current frame |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/layer_diagonal_20.png" width="20"> | **Onion Skin** | Toggle onion skinning overlay |

---

## What Keyframes Store

Each keyframe (◆) captures the **complete state** of a layer:

| Property | Stored |
|----------|--------|
| Pixel data | ✅ Full layer content |
| Visibility | ✅ On/Off state |
| Opacity | ✅ 0-255 value |
| Blend mode | ✅ Normal, Multiply, etc. |
| Layer effects | ✅ All settings for all effects |
| Mask state | ✅ If layer has a mask |

---

## Hold-Frame Behavior

Unlike Stage keyframes which interpolate, layer keyframes use **hold** behavior:

```
Frame:  0    5    10    15    20
        ◆─────────◆─────────◆
        │         │         │
        │         │         └─ Keyframe C values shown frames 10-20
        │         └─ Keyframe B values shown frames 5-9
        └─ Keyframe A values shown frames 0-4
```

Values stay constant until the next keyframe. There's no automatic tweening - this gives you full artistic control over every frame.

---

## Creating Keyframes

### Method 1: Toolbar Button
1. Navigate to desired frame
2. Make changes to your layer
3. Click the <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> **Add Keyframe** button in the timeline toolbar

### Method 2: Context Menu
1. Navigate to desired frame
2. Make changes to your layer
3. Right-click in the timeline → **Add Keyframe**

### Method 3: Keyboard Shortcut
1. Navigate to desired frame
2. Make changes to your layer
3. Press `K` to add a keyframe

### Method 4: Copy/Paste
1. Select a keyframe
2. `Ctrl+C` to copy
3. Navigate to new frame
4. `Ctrl+V` to paste

---

## Playback Controls

| Icon | Button | Action | Shortcut |
|:----:|--------|--------|----------|
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/previous_16.png" width="16"> | First frame | Jump to frame 0 | `Home` |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/previous_16.png" width="16"> | Previous frame | Go back one | `,` |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> | Play/Pause | Toggle playback | `Space` |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/next_16.png" width="16"> | Next frame | Go forward one | `.` |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/next_16.png" width="16"> | Last frame | Jump to end | `End` |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/stop_16.png" width="16"> | Stop | Stop and go to frame 0 | |

### Playback Options
- **FPS** - Frames per second (6, 8, 12, 15, 24, 30, 60)
- **Loop Mode** - Loop, Ping-Pong, Play Once
- **Range** - Play full animation or selected range

---

## Onion Skinning

Shows ghost images of nearby frames to help with smooth animation.

### Enabling Onion Skin
Click the <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/layer_diagonal_16.png" width="16"> **Onion Skin** button in the timeline toolbar.

### Onion Skin Settings

| Setting | Description |
|---------|-------------|
| **Frames Before** | How many previous frames to show (blue/cyan tint) |
| **Frames After** | How many future frames to show (green/yellow tint) |
| **Opacity** | Ghost transparency (0-100%) |
| **Show on Current Layer Only** | Limit to active layer |

### Color Coding
- **Blue/Cyan tint** = Previous frames (where you came from)
- **Green/Yellow tint** = Future frames (where you're going)

---

## Animating Layer Effects

All layer effects are fully animatable! This opens up tons of possibilities.

### How to Animate Effects

1. Navigate to frame 0
2. Open layer settings (click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_16.png" width="16"> gear icon), enable an effect (e.g., Glow)
3. Configure the effect settings
4. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> **Add Keyframe**
5. Navigate to a later frame (e.g., frame 10)
6. Change the effect settings (e.g., increase glow intensity)
7. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> **Add Keyframe** again
8. Press <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> **Play** to see the effect animate!

### Animation Ideas

| Effect | Animation Idea |
|--------|----------------|
| **Glow** | Pulsing power-ups, breathing light |
| **Drop Shadow** | Shadow movement with light source |
| **Outline** | Thickness change on impact |
| **Scanlines** | Intensity during flashback |
| **Chromatic Aberration** | Glitch on damage |
| **Vignette** | Close in during danger |

---

## Frame Operations

Right-click a frame or keyframe for these options:

| Operation | Description |
|-----------|-------------|
| **Add Keyframe** | Create keyframe at current frame |
| **Delete Keyframe** | Remove selected keyframe |
| **Copy Keyframe** | Copy to clipboard |
| **Paste Keyframe** | Paste from clipboard |
| **Duplicate Frame** | Copy keyframe to next frame |
| **Insert Frame** | Add blank frame, shift others right |
| **Delete Frame** | Remove frame, shift others left |
| **Clear Frame** | Clear content, keep keyframe |

---

## Multi-Layer Animation

### Independent Keyframes
Each layer animates independently:
- Character layer: keyframes at 0, 4, 8, 12
- Effect layer: keyframes at 0, 6, 12
- Background: single keyframe at 0 (static)

### Layer Visibility Animation
Toggle layers on/off over time using the <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_16.png" width="16"> **visibility** icon:
1. Set visibility ON at frame 0, add keyframe
2. Navigate to frame where you want it to disappear
3. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_16.png" width="16"> to toggle visibility OFF, add keyframe
4. Layer appears frames 0-N, disappears after

### Blend Mode Animation
Change blend modes over time for effects like:
- Flash to white (Add mode)
- Dramatic shadows (Multiply mode)
- Transitions between looks

---

## Working with Audio

See [[Audio]] for full details, but the basics:

1. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> **Add Audio Track** in the timeline
2. Import your audio file
3. Waveform appears in the timeline
4. Align keyframes to beats and cues

Audio is **reference only** - helps you sync but isn't exported by default.

---

## Working with the Stage

See [[Stage]] for full details. The Stage <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/camera_16.png" width="16"> provides:

- Camera pan, zoom, rotation
- Smooth interpolation between keyframes (◇ diamond for stage, ◆ for layers)
- Easing options (linear, ease in/out, bounce, elastic)

The Stage is perfect for:
- Panning across a scene
- Zoom-ins for emphasis
- Screen shake effects
- Cinematic camera work

---

## Animation Workflow Tips

### Phase 1: Key Poses
1. Draw the most important poses first (extremes)
2. Space them out on the timeline
3. Test timing by clicking <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> **Play**

### Phase 2: Breakdowns
1. Add frames between key poses
2. Draw the in-between positions
3. Focus on arcs and motion paths

### Phase 3: In-Betweens
1. Fill remaining frames
2. Smooth out the motion
3. Adjust timing as needed

### Phase 4: Polish
1. Add secondary animation (hair, cloth, etc.)
2. Apply layer effects <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/image_sparkle_16.png" width="16">
3. Fine-tune timing
4. Add Stage camera movement if needed

### Pro Tips

- **Use <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/layer_diagonal_16.png" width="16"> onion skin constantly** - You can't animate well without seeing context
- **Work rough first** - Silhouettes and blobs, then refine details
- **Loop early** - Check your loop point before adding detail
- **Save versions** - Keep backups of major milestones
- **Test at speed** - Animations often look different at full speed

---

## Exporting Canvas Animation

### As GIF
1. **File → Export Animation → GIF**
2. Set loop count (0 = infinite)
3. Adjust color depth if needed
4. Choose scale (1x, 2x, 4x)

### As Video (MP4/AVI/WMV)
1. **File → Export Animation → Video**
2. Choose format and quality
3. Set resolution and frame rate

### As Image Sequence
1. **File → Export Animation → Sequence**
2. Choose format (PNG recommended)
3. Files are numbered automatically

### With Stage Camera
When Stage is enabled, exports render through the camera:
- Only the Stage viewport is exported
- Camera movements are applied
- Output matches Stage Output Size

---

## See Also

- [[Tile Animation|Tile-Animation]] - Alternative animation system
- [[Sub-Routines]] - Nesting animations
- [[Stage]] - Camera system
- [[Audio]] - Audio sync
- [[Effects]] - Layer effects reference
