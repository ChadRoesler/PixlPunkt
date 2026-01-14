# Stage (Camera System)

The Stage <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/camera_20.png" width="20"> provides a virtual camera for Canvas Animation, enabling cinematic pan, zoom, and rotation effects without modifying your artwork. Perfect for cutscenes, dramatic reveals, and dynamic presentations.

## Overview

The Stage is essentially a "window" into your canvas that can be animated. While your artwork stays put, the camera moves around it.

| Feature | Description |
|---------|-------------|
| **Position** | Where the camera is centered |
| **Scale** | Zoom level |
| **Rotation** | Camera angle |
| **Interpolation** | Smooth motion between keyframes |
| **Output Size** | Final render dimensions |

---

## Enabling the Stage

1. Open **Stage Settings** (click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/camera_16.png" width="16"> camera icon in Timeline toolbar)
2. Check **Enable Stage**
3. Configure dimensions:
   - **Stage Size** - How much of the canvas is visible
   - **Output Size** - Final export resolution

### Stage vs Canvas

<img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/screenshots/CanvasAnimation_Stage.png">

Only content within the Stage viewport is rendered/exported.

---

## Stage Dimensions

### Stage Size
The viewport dimensions on your canvas (in pixels).

Example: A 256�256 canvas with a 128�128 stage means the camera sees half the canvas at a time.

### Output Size
The final rendered dimensions (in pixels).

Example: A 128�128 stage with 512�512 output means 4x upscaling.

### Relationship

| Stage Size | Output Size | Result |
|------------|-------------|--------|
| 128�128 | 128�128 | 1:1 (no scaling) |
| 128�128 | 256�256 | 2x upscale |
| 128�128 | 512�512 | 4x upscale |
| 256�256 | 128�128 | 0.5x downscale |

---

## Stage Keyframes

Unlike layer keyframes (which hold values), stage keyframes **interpolate** for smooth camera motion.

### Creating Stage Keyframes

1. Enable Stage
2. Navigate to frame 0
3. Position the camera (drag viewport or use settings)
4. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> **Add Stage Keyframe**
5. Navigate to a later frame
6. Move/zoom/rotate the camera
7. Add another keyframe
8. Press <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Play to see smooth motion!

### Keyframe Properties

Each stage keyframe stores:

| Property | Description |
|----------|-------------|
| **Position X/Y** | Camera center point |
| **Scale X/Y** | Zoom level (1.0 = 100%) |
| **Rotation** | Angle in degrees |
| **Easing** | Interpolation curve |

---

## Manipulating the Camera

### On-Canvas Controls

When Stage is enabled:
- **Drag inside viewport** - Pan camera
- **Corner handles** - Scale/zoom
- **Rotate handle** (outside corners) - Rotate camera
- **Double-click** - Reset to default

### Settings Panel

| Control | Description |
|---------|-------------|
| **Position X** | Horizontal camera position |
| **Position Y** | Vertical camera position |
| **Scale X** | Horizontal zoom (%) |
| **Scale Y** | Vertical zoom (%) |
| **Rotation** | Camera angle (degrees) |
| **Lock Aspect** <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/link_16.png" width="16"> | Keep scale X/Y equal |

### Keyboard Modifiers

| Modifier | Effect |
|----------|--------|
| `Shift` | Constrain to axis (pan) or 15� (rotate) |
| `Ctrl` | Scale from center |
| `Alt` | Duplicate keyframe while dragging |

---

## Easing Options

Easing controls how the camera moves between keyframes.

### Available Easing Types

| Easing | Motion | Best For |
|--------|--------|----------|
| **Linear** | Constant speed | Mechanical, robotic |
| **EaseIn** | Start slow, speed up | Objects falling |
| **EaseOut** | Start fast, slow down | Objects stopping |
| **EaseInOut** | Slow at both ends | Natural motion |
| **EaseInQuad** | Gentle ease in | Subtle acceleration |
| **EaseOutQuad** | Gentle ease out | Subtle deceleration |
| **EaseInOutQuad** | Gentle both ends | Most natural |
| **Bounce** | Bouncy overshoot | Playful, cartoony |
| **Elastic** | Spring motion | Impact, wobbly |
| **Back** | Slight overshoot | Anticipation |

### Setting Easing

1. Select a stage keyframe
2. Right-click ? **Easing**
3. Choose easing type
4. Easing applies to motion TOWARD this keyframe

### Per-Property Easing

Different properties can have different easing:
- Position: EaseInOut (smooth travel)
- Scale: EaseOut (zoom settles gradually)
- Rotation: Linear (constant spin)

---

## Common Camera Techniques

### Pan Across Scene

1. Frame 0: Camera at left side
2. Frame 60: Camera at right side
3. Easing: EaseInOut

Result: Smooth horizontal scroll.

### Zoom In for Drama

1. Frame 0: Wide shot (scale 0.5) <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/zoom_out_16.png" width="16">
2. Frame 30: Close-up (scale 1.5), centered on subject <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/zoom_in_16.png" width="16">
3. Easing: EaseIn

Result: Dramatic zoom-in.

### Reveal Pan

1. Frame 0: Camera shows sky/ceiling
2. Frame 45: Camera shows ground/subject
3. Easing: EaseOut

Result: Slow reveal pan down.

### Screen Shake

1. Frame 0: Normal position
2. Frame 2: Offset +5, -3
3. Frame 4: Offset -4, +6
4. Frame 6: Offset +3, -2
5. Frame 8: Normal position
6. Easing: Linear (for all)

Result: Quick shake effect.

### Rotation Spin

1. Frame 0: Rotation 0�
2. Frame 30: Rotation 360�
3. Easing: EaseInOut

Result: Smooth full rotation.

---

## Stage in Timeline

The Stage has its own track in the timeline:

*Image to come*

- **?** = Stage keyframe (diamond shape, hollow)
- **?** = Layer keyframe (diamond shape, filled)
- Drag keyframes to retime camera motion
- Select multiple keyframes to move together

---

## Export with Stage

When Stage is enabled, exports render through the camera:

### What Changes
- Only Stage viewport content is exported
- Output matches Stage Output Size
- Camera motion is baked into the export
- Content outside Stage is cropped

### Export Settings

| Setting | Effect |
|---------|--------|
| **Output Size** | Final dimensions |
| **Maintain Aspect** <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/link_16.png" width="16"> | Lock aspect ratio |
| **Background Color** | Color for areas outside canvas |

---

## Tips and Tricks

### Design for Camera
- Make your canvas larger than final output
- Leave "runway" for camera movement
- Plan key camera positions before animating

### Combine with Layer Animation
Stage <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/camera_16.png" width="16"> movement + layer keyframes = complex scenes:
- Character animates (layer keyframes)
- Camera follows character (stage keyframes)
- Background scrolls via camera
- Effects <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/image_sparkle_16.png" width="16"> animate independently

### Use for Composition
Even without animation, Stage helps framing:
- Try different compositions
- Lock on best framing
- Export at exact size needed

### Test Early
- Preview camera motion early in production with <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Play
- Easier to adjust before all animation is done
- Check timing against audio if using reference tracks

---

## Troubleshooting

### Camera Won't Move
- Is Stage enabled? (Check Stage Settings <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/camera_16.png" width="16">)
- Are you on the Stage track in timeline?
- Is there a keyframe at this frame?

### Jerky Motion
- Check easing settings
- Add more keyframes for complex paths
- Ensure keyframes are spaced appropriately

### Export Looks Wrong
- Verify Output Size settings
- Check that Stage viewport covers intended area
- Preview full animation before exporting

### Rotation is Weird
- Rotation is around the viewport center
- For off-center rotation, combine position + rotation keyframes
- Consider your anchor point

---

## See Also

- [[Canvas Animation|Canvas-Animation]] - Frame animation basics
- [[Audio]] - Syncing to audio
- [[Sub-Routines]] - Nested animations
