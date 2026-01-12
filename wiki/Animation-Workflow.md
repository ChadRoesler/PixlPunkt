# Animation Workflow

Best practices and professional techniques for animation in PixlPunkt.

---

## Planning Your Animation

### Before You Draw

1. **Define the action** - What movement or sequence?
2. **Determine frame count** - How many frames?
3. **Set frame rate** - How fast (FPS)?
4. **Plan key poses** - Major positions
5. **Consider loops** - Does it repeat?

### Frame Rates

| FPS | Feel | Common Uses |
|-----|------|-------------|
| 6 | Choppy, stylized | Retro games, GBA |
| 8 | Limited animation | NES-style |
| 12 | Standard animation | Most pixel art |
| 15 | Smooth | Higher quality |
| 24 | Film standard | Cinematic |
| 30 | Very smooth | Modern games |

> ?? **Tip:** Start at 12 FPS - it's the sweet spot for pixel art.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Animation Principles

### 1. Timing & Spacing

**Timing** = Number of frames for action
**Spacing** = Distance moved per frame

```
Slow motion:    ?  ?  ?  ?  ?  ?  (many frames)
Fast motion:    ?     ?     ?     (few frames)
Ease in:        ?  ? ? ???        (accelerate)
Ease out:       ??? ? ?  ?        (decelerate)
```

### 2. Squash & Stretch

Exaggerate compression and extension:

```
Jump anticipation:   ???  (squash)
Jump apex:           ?    (stretch)
Land impact:         ??   (squash)
```

### 3. Anticipation

Prepare the viewer for action:

```
Frame 1-3:  Wind up (opposite direction)
Frame 4:    Hold (brief pause)
Frame 5+:   Action (main movement)
```

### 4. Follow-Through

Parts continue moving after main action:

```
Character stops:     ?
Hair/cape continues: ~?
Then settles:        ~
```

### 5. Secondary Action

Supporting movements that enhance main action:
- Character walks (primary) + arms swing (secondary)
- Jump (primary) + cape flows (secondary)

---

## Workflow Stages

### Stage 1: Keyframes

Draw the essential poses:

1. **Contact poses** - Where things touch/change
2. **Extreme poses** - Farthest positions
3. **Breakdown poses** - In-between extremes

```
Walk cycle keyframes:
[Contact] ? [Passing] ? [Contact] ? [Passing]
    ?         ?           ?          ?
```

### Stage 2: In-Betweens

Fill frames between keyframes:

1. Enable **onion skinning** to see previous/next
2. Draw intermediate positions
3. Check timing constantly

### Stage 3: Cleanup

Refine the animation:

1. Fix inconsistencies
2. Smooth movements
3. Add details
4. Polish timing

### Stage 4: Effects

Add final touches:

1. Smears/motion blur
2. Particles/dust
3. Impact frames
4. Screen shake (via Stage)

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/layer_diagonal_16.png" width="16"> Using Onion Skinning

### Enable Onion Skin

- Press `Ctrl+Shift+O`
- Or click onion icon in Timeline

### Onion Skin Settings

| Setting | Description |
|---------|-------------|
| **Previous** | How many frames back (1-5) |
| **Next** | How many frames forward (0-5) |
| **Opacity** | Ghost frame transparency |
| **Color** | Tint previous (red) / next (blue) |

### Effective Use

- Use 2-3 previous frames
- Next frames for timing check
- Reduce opacity if distracting
- Toggle quickly with shortcut

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/camera_16.png" width="16"> Camera Work (Stage)

### Why Use Camera?

- Add dynamism to static animations
- Screen shake for impacts
- Pan across large scenes
- Zoom for emphasis

### Basic Camera Moves

| Move | Use |
|------|-----|
| **Pan** | Follow action |
| **Zoom** | Draw attention |
| **Shake** | Impacts, explosions |
| **Rotate** | Dramatic effect |

See [[Stage]] for detailed camera documentation.

---

## Common Animation Types

### Walk Cycle

**Standard 4-frame walk:**
```
Frame 1: Contact (leg forward)
Frame 2: Passing (legs together)
Frame 3: Contact (other leg forward)
Frame 4: Passing (legs together)
```

**8-frame walk (smoother):**
Add breakdowns between each contact and passing.

### Run Cycle

- Faster timing (fewer frames)
- Body leans forward
- Both feet off ground at some point
- Arms pump opposite to legs

### Idle Animation

Keep characters alive:
- Subtle breathing
- Blinking (every 60-120 frames)
- Weight shifts
- Small movements

### Attack Animation

```
Anticipation (2-3 frames)
    ? Wind up
Attack (1-2 frames)
    ? Quick strike
Recovery (2-4 frames)
    ? Return to idle
```

### Jump Animation

```
Anticipation ? Crouch
Launch       ? Push off
Rise         ? Stretch
Apex         ? Hang time
Fall         ? Speed lines
Land         ? Squash
Recovery     ? Stand
```

---

## Layer Organization

### Animation Layer Structure

```
?? Character
??? ?? Head
?   ??? Face
?   ??? Hair
?   ??? Eyes (blink animation)
??? ?? Body
?   ??? Torso
?   ??? Front Arm
?   ??? Back Arm
??? ?? Legs
    ??? Front Leg
    ??? Back Leg
```

### Benefits

- Animate parts separately
- Reuse body parts
- Easy to adjust timing
- Cleaner workflow

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/video_clip_16.png" width="16"> Timeline Tips

### Frame Hold

Keep a frame visible longer:
- Right-click frame ? **Set Duration**
- Or drag frame edge
- Use for pauses, emphasis

### Copy/Paste Frames

- `Ctrl+C` / `Ctrl+V` in timeline
- Duplicate keyframes easily
- Reverse for ping-pong

### Link Frames

Share content between frames:
- Changes update everywhere
- Good for repeated poses
- Saves memory

---

## Testing Your Animation

### Preview Often

- Press `Space` frequently
- Check at intended speed
- Watch for jitters

### At Different Speeds

- Test at 50%, 100%, 200% speed
- Helps identify timing issues
- What feels right?

### Loop Point Check

- Does it loop seamlessly?
- Any jumps/pops?
- Hold positions match?

### Silhouette Test

- View as solid black
- Is action readable?
- Clear poses?

---

## Optimization

### Minimize Frames

- Every frame costs
- Can you achieve same effect with fewer?
- Strategic holds extend animation

### Efficient Layers

- Merge when animation done
- Too many layers = slow export
- Group related elements

### File Size

- Limit canvas size
- Indexed color when possible
- Remove unused frames

---

## Export Workflow

### For Games

1. Export as **sprite sheet**
2. Use consistent cell size
3. Include all states (idle, walk, attack, etc.)
4. Document frame timings

### For Web/Social

1. Export as **GIF** or **APNG**
2. Consider file size limits
3. Scale up (2x, 4x) for visibility
4. Optimize colors if needed

### For Video

1. Export as **PNG sequence** or **MP4**
2. Use high quality settings
3. Add audio in video editor
4. Render at appropriate resolution

---

## See Also

- [[Canvas Animation|Canvas-Animation]] - Animation system basics
- [[Stage]] - Camera and composition
- [[Sub-Routines]] - Nested animations
- [[Audio]] - Sound sync
- [[File Formats|Formats]] - Export options
