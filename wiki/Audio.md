# Audio

Working with audio reference tracks in PixlPunkt animations.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/speaker_16.png" width="16"> Overview

PixlPunkt supports **audio reference tracks** for animation:

- Sync animation to music or dialogue
- See waveform visualization
- Scrub through audio with timeline
- Audio is for **reference only** - not included in exports

---

## Adding Audio

### Import Audio Track

1. Open the **Timeline** panel (`T`)
2. Click **Add Audio Track** <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16">
3. Select an audio file

### Supported Formats

| Format | Extension |
|--------|-----------|
| WAV | `.wav` |
| MP3 | `.mp3` |
| OGG | `.ogg` |
| FLAC | `.flac` |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/waveform_16.png" width="16"> Waveform Display

The audio track shows a waveform visualization:

```
Timeline:
┌────────────────────────────────────────────┐
│ Frame:  0    15    30    45    60    75    │
├────────────────────────────────────────────┤
│ Layers  ████████████████████████████████   │
├────────────────────────────────────────────┤
│ Audio   ▁▂▃▅▆▇█▇▆▅▃▂▁▂▃▅▇██▇▅▃▂▁▁▂▃▄▅▆▇█ │
└────────────────────────────────────────────┘
```

### Waveform Features

- **Peaks** = Loud moments (hits, beats)
- **Valleys** = Quiet moments
- **Stereo** = Left/right channels shown

---

## Audio Track Properties

### Offset

Shift audio relative to animation start:
- **Positive** = Audio starts later
- **Negative** = Audio starts before frame 0
- Useful for aligning to specific beats

### Volume

Adjust playback volume (doesn't affect source file):
- **100%** = Original volume
- **0%** = Muted
- Preview volume only

### Loop

Whether audio loops when animation loops:
- **On** = Audio repeats with animation
- **Off** = Audio plays once

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Playback

### Play with Audio

- Press `Space` to play animation with audio
- Audio syncs to current frame rate
- Scrubbing (dragging playhead) scrubs audio

### Mute Audio

- Click the speaker icon <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/speaker_16.png" width="16"> on audio track
- Or press `M` to toggle mute
- Useful when focusing on visuals

### Solo Audio

- Play audio without visual playback
- Right-click audio track → **Solo**
- Useful for studying timing

---

## Syncing Animation to Audio

### Beat Markers

Add markers at important audio moments:

1. Play/scrub to the beat
2. Press `Ctrl+M` to add marker
3. Marker appears in timeline
4. Snap frames to markers

### Frame Rate Matching

For music sync, match frame rate to tempo:

| BPM | Suggested FPS |
|-----|---------------|
| 60 | 12 fps (1 beat = 12 frames) |
| 120 | 12 fps (1 beat = 6 frames) |
| 120 | 24 fps (1 beat = 12 frames) |
| 140 | 14 fps (1 beat = 6 frames) |

### Lip Sync Workflow

1. Import dialogue audio
2. Identify phoneme timing in waveform
3. Create mouth shape keyframes
4. Align to audio peaks

---

## Multiple Audio Tracks

You can have multiple audio tracks:

```
┌─────────────────────────────────────────┐
│ Audio 1: Music.mp3                      │
│ ▁▂▃▅▆▇█▇▆▅▃▂▁▂▃▅▇██▇▅▃▂▁               │
├─────────────────────────────────────────┤
│ Audio 2: SFX.wav                        │
│ ▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▇█▁▁▁▁▁▁▁▁▁▁          │
├─────────────────────────────────────────┤
│ Audio 3: Voice.mp3                      │
│ ▁▁▁▂▃▄▅▆▅▄▃▂▁▁▂▃▄▅▆▇▆▅▄▃▂▁             │
└─────────────────────────────────────────┘
```

### Managing Tracks

- Mute/solo individual tracks
- Adjust volume per track
- Different offsets for each
- Drag to reorder

---

## Exporting

### Audio is Reference Only

**Important:** Audio tracks are NOT included in exports.

Exports produce:
- Silent GIF/APNG
- Silent MP4/AVI (unless you add audio in post)
- PNG sequences (no audio possible)

### For Final Audio

Combine your exported animation with audio in:
- Video editing software (Premiere, DaVinci, etc.)
- Game engine (Unity, Godot, etc.)
- FFmpeg command line

### FFmpeg Example

```bash
ffmpeg -i animation.mp4 -i music.mp3 -c:v copy -c:a aac output.mp4
```

---

## Tips

### Use WAV for Precision

WAV files have accurate waveform display:
- MP3 may have slight timing variations
- WAV recommended for precise sync work

### Zoom for Detail

Zoom in on timeline to see waveform detail:
- Scroll wheel on timeline
- Or use zoom slider
- Helps identify exact beat positions

### Mark Before Animating

1. Import audio first
2. Listen through completely
3. Add markers at key moments
4. Then start animating

### Preview at Final FPS

Ensure your preview FPS matches export FPS:
- Audio sync may differ at different frame rates
- Test at final settings before finishing

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Space` | Play/Pause with audio |
| `M` | Mute audio |
| `Ctrl+M` | Add marker at playhead |
| `.` / `,` | Next/Previous frame |
| `Shift+.` / `Shift+,` | Jump to next/previous marker |

---

## Troubleshooting

### No Sound

- Check system volume
- Check track isn't muted
- Verify audio file plays in other apps
- Try different audio format

### Audio Out of Sync

- Check audio offset setting
- Verify frame rate is correct
- Try WAV format instead of MP3

### Waveform Not Showing

- File may still be loading
- Very long files take time to process
- Try shorter clip for testing

---

## See Also

- [[Canvas Animation|Canvas-Animation]] - Animation basics
- [[Animation Workflow|Animation-Workflow]] - Best practices
- [[Sub-Routines]] - Nested animations
- [[File Formats|Formats]] - Export options
