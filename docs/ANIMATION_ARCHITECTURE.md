# PixlPunkt Animation System - Technical Architecture

This document describes the internal architecture of PixlPunkt's animation system for developers and contributors.

---

## Overview

PixlPunkt provides two distinct animation systems:

| System | Purpose | Data Model |
|--------|---------|------------|
| **Canvas Animation** | Full layer-based animation | Keyframe snapshots per layer |
| **Tile Animation** | Sprite sheet sequencing | Tile coordinate references |

Both systems share common UI infrastructure but have different data models and use cases.

---

## Canvas Animation Architecture

### Core Classes

```
CanvasAnimationState (root state container)
├── Tracks: ObservableCollection<CanvasAnimationTrack>
│   └── Keyframes: ObservableCollection<LayerKeyframeData>
│       └── EffectStates: List<EffectKeyframeData>
├── Stage: StageSettings
├── StageTrack: StageAnimationTrack
│   └── Keyframes: ObservableCollection<StageKeyframeData>
├── PixelDataStorage: Dictionary<int, byte[]>
└── Playback state, timeline settings, onion skin settings
```

### CanvasAnimationState (`Core/Animation/CanvasAnimationState.cs`)

The root container for all canvas animation data. One instance per `CanvasDocument`.

**Key Responsibilities:**
- Manages timeline settings (frame count, FPS, loop)
- Maintains collection of animation tracks
- Stores pixel data snapshots (memory-efficient storage)
- Controls playback state and navigation
- Manages Stage (camera) settings and keyframes

**Key Methods:**
```csharp
// Track Management
void SyncTracksFromDocument(CanvasDocument document)
CanvasAnimationTrack? GetTrackForLayer(LayerBase layer)

// Keyframe Operations
void CaptureKeyframe(RasterLayer layer, int frameIndex)
void SetKeyframe(LayerBase layer, LayerKeyframeData keyframe)
bool RemoveKeyframe(LayerBase layer, int frameIndex)

// Frame Application
void ApplyFrameToDocument(CanvasDocument document, int frameIndex)

// Pixel Data Storage
int StorePixelData(byte[] pixels)
byte[]? GetPixelData(int pixelDataId)

// Playback
void Play() / Pause() / Stop()
void SetCurrentFrame(int index)
void NextFrame() / PreviousFrame()
```

### CanvasAnimationTrack (`Core/Animation/CanvasAnimationTrack.cs`)

Represents animation data for a single layer or folder.

**Properties:**
- `Id` - Track's unique GUID
- `LayerId` - Associated layer's GUID (for binding after load)
- `LayerName` - Cached display name
- `IsFolder` - Whether this tracks a folder vs. raster layer
- `Depth` - Nesting level for UI indentation
- `Keyframes` - Collection of `LayerKeyframeData`

**Key Methods:**
```csharp
LayerKeyframeData? GetKeyframeAt(int frameIndex)
LayerKeyframeData? GetEffectiveStateAt(int frameIndex)  // Hold-frame logic
void SetKeyframe(LayerKeyframeData keyframe)
bool RemoveKeyframeAt(int frameIndex)
```

### LayerKeyframeData (`Core/Animation/LayerKeyframeData.cs`)

Stores the complete state of a layer at a specific frame.

**Properties:**
```csharp
int FrameIndex          // Frame this keyframe is at
bool Visible            // Layer visibility
byte Opacity            // Layer opacity (0-255)
BlendMode BlendMode     // Blend mode enum
int PixelDataId         // Reference to stored pixel data (-1 = none)
List<EffectKeyframeData> EffectStates  // All effect settings
```

**Hold-Frame Behavior:**
When querying a frame that doesn't have a keyframe, `GetEffectiveStateAt()` returns the most recent keyframe at or before the requested frame. This implements the "hold frame" behavior common in traditional animation.

### EffectKeyframeData (`Core/Animation/EffectKeyframeData.cs`)

Captures the complete state of a single layer effect.

**Properties:**
```csharp
string EffectId                         // Matches LayerEffectBase.EffectId
bool IsEnabled                          // Effect on/off state
Dictionary<string, object?> PropertyValues  // All animatable properties
```

**Key Methods:**
```csharp
// Capture effect state using reflection
EffectKeyframeData(LayerEffectBase effect)

// Apply captured state back to an effect
void ApplyTo(LayerEffectBase effect)

// Deep copy for cloning
EffectKeyframeData Clone()
```

**Property Capture:**
Uses reflection to capture all public read/write properties that are:
- Value types (int, float, bool, byte, etc.)
- Strings
- Enums

Excludes `IsEnabled` (handled separately) and `EffectId` (identity).

### Pixel Data Storage

To avoid storing duplicate pixel data, `CanvasAnimationState` maintains a dictionary:

```csharp
Dictionary<int, byte[]> PixelDataStorage
```

**Workflow:**
1. When capturing keyframe: `int id = StorePixelData(layer.Surface.Pixels)`
2. Keyframe stores just the `PixelDataId` reference
3. When applying frame: `byte[] pixels = GetPixelData(keyframe.PixelDataId)`

**Memory Management:**
- `CleanupUnusedPixelData()` removes entries not referenced by any keyframe
- Should be called after bulk keyframe deletions

---

## Stage (Camera) System

### StageSettings (`Core/Animation/StageSettings.cs`)

Static camera configuration (the "viewport" definition).

**Properties:**
```csharp
bool Enabled              // Whether stage is active
int StageX, StageY        // Viewport position on canvas
int StageWidth, StageHeight  // Viewport dimensions
int OutputWidth, OutputHeight  // Rendered output size
StageScalingAlgorithm ScalingAlgorithm  // NearestNeighbor, Bilinear, etc.
StageBoundsMode BoundsMode  // Free, Constrained, CenterLocked
```

### StageAnimationTrack (`Core/Animation/StageAnimationTrack.cs`)

Manages camera keyframes with **interpolation** (unlike layer tracks).

**Key Difference from Layer Tracks:**
- Layer tracks use **hold-frame** behavior (values stay constant)
- Stage track uses **interpolation** (smooth transitions between keyframes)

**Key Methods:**
```csharp
StageKeyframeData? GetInterpolatedStateAt(int frameIndex)
void CaptureKeyframe(StageSettings settings, int frameIndex)
```

### StageKeyframeData (`Core/Animation/StageKeyframeData.cs`)

Camera transform at a specific frame.

**Properties:**
```csharp
int FrameIndex
float PositionX, PositionY  // Camera center
float ScaleX, ScaleY        // Zoom (1.0 = 100%)
bool UniformScale           // Lock X/Y scale together
float Rotation              // Degrees
EasingType PositionEasing   // Easing for position interpolation
EasingType ScaleEasing      // Easing for scale interpolation
EasingType RotationEasing   // Easing for rotation interpolation
```

**Interpolation:**
The `Lerp()` static method handles interpolation with per-property easing:
```csharp
static StageKeyframeData Lerp(StageKeyframeData from, StageKeyframeData to, float t)
```

---

## Tile Animation Architecture

### Core Classes

```
TileAnimationState (root state container)
├── Reels: ObservableCollection<TileAnimationReel>
│   └── Frames: ObservableCollection<ReelFrame>
├── SelectedReel: TileAnimationReel?
└── Onion skin settings, playback state
```

### TileAnimationState (`Core/Animation/TileAnimationState.cs`)

Container for all tile animation data. One instance per `CanvasDocument`.

**Key Properties:**
- `Reels` - Collection of animation reels
- `SelectedReel` - Currently active reel
- Onion skin settings

### TileAnimationReel (`Core/Animation/TileAnimationReel.cs`)

A named animation sequence referencing tile positions.

**Properties:**
```csharp
Guid Id                     // Unique identifier
string Name                 // Display name (e.g., "Walk", "Idle")
int DefaultFrameTimeMs      // Default duration per frame
bool Loop                   // Loop at end
bool PingPong               // Reverse at ends
ObservableCollection<ReelFrame> Frames
```

### ReelFrame

A single frame in a reel, referencing a tile position.

**Properties:**
```csharp
int TileX, TileY           // Grid position in tileset
int? DurationMs            // Override duration (null = use default)
```

---

## UI Components

### AnimationPanel (`UI/Animation/AnimationPanel.xaml`)

Main container that switches between Canvas and Tile animation modes.

**Structure:**
```
AnimationPanel
├── Mode tabs (Canvas | Tile)
├── Canvas Animation Content
│   ├── Toolbar (playback, frame count, FPS, stage toggle, etc.)
│   ├── Timeline Grid
│   │   ├── Layer names column
│   │   ├── Keyframe grid (scrollable)
│   │   └── Playhead overlay
│   └── StagePreviewPanel (right side)
└── Tile Animation Content
    ├── ReelListPanel (left)
    ├── FrameEditPanel (center)
    └── PlaybackPanel (right)
```

### StagePreviewPanel (`UI/Animation/StagePreviewPanel.xaml`)

Shows live preview of what the Stage/Camera sees.

**Features:**
- Win2D canvas for efficient rendering
- Auto-updates on frame change or stage settings change
- Expand/collapse for larger view
- Shows stage dimensions and position info

### PlaybackPanel (`UI/Animation/PlaybackPanel.xaml`)

Preview panel for Tile Animation with transport controls.

---

## Serialization

### File Format (DocumentIO)

Animation data is stored in the `.pxp` file format (Version 6+).

**Canvas Animation Section:**
```
[Timeline Settings]
  FrameCount: Int32
  FramesPerSecond: Int32
  Loop: Boolean
  
[Onion Skin Settings]
  Enabled: Boolean
  FramesBefore: Int32
  FramesAfter: Int32
  Opacity: Single

[Tracks]
  TrackCount: Int32
  For each track:
    Id: Guid (16 bytes)
    LayerId: Guid (16 bytes)
    LayerName: String
    IsFolder: Boolean
    Depth: Int32
    KeyframeCount: Int32
    For each keyframe:
      FrameIndex: Int32
      Visible: Boolean
      Opacity: Byte
      BlendMode: Int32
      PixelDataId: Int32
      EffectStateCount: Int32  (Version 6+)
      For each effect state:
        EffectId: String
        IsEnabled: Boolean
        PropertyCount: Int32
        For each property:
          Name: String
          Value: (type-tagged serialization)

[Pixel Data Storage]
  EntryCount: Int32
  For each entry:
    Id: Int32
    Length: Int32
    Data: Byte[]

[Stage Settings] (Version 5+)
  Enabled, dimensions, scaling algorithm, bounds mode...

[Stage Track] (Version 5+)
  Keyframes with transform and easing data...
```

**Version History:**
- Version 1-2: Initial format, tile animation
- Version 3: Canvas animation added
- Version 4: Layer IDs for track binding
- Version 5: Stage (camera) system
- Version 6: Effect keyframes

---

## Export Pipeline

### AnimationExportService (`Core/Export/AnimationExportService.cs`)

Handles rendering animation frames for export.

**Key Methods:**
```csharp
Task<List<RenderedFrame>> RenderCanvasAnimationAsync(
    CanvasDocument document, 
    ExportOptions options)

Task<List<RenderedFrame>> RenderTileAnimationAsync(
    CanvasDocument document,
    TileAnimationReel reel,
    ExportOptions options)
```

**Export Options:**
```csharp
class ExportOptions
{
    int Scale           // Pixel multiplier
    bool UseStage       // Apply camera transforms
    bool SeparateLayers // Export layers separately
    int FrameDelayMs    // Override frame timing
}
```

**Rendered Frame:**
```csharp
class RenderedFrame
{
    byte[] Pixels       // BGRA pixel data
    int Width, Height   // Dimensions
    int DurationMs      // Frame duration
}
```

---

## Integration Points

### Document ? Animation Binding

Each `CanvasDocument` owns:
- `TileAnimationState TileAnimationState { get; }`
- `CanvasAnimationState CanvasAnimationState { get; }`

Layer IDs (`LayerBase.Id`) are used to bind tracks to layers. This persists across save/load.

### Layer ? Track Sync

When layers are added/removed/reordered:
```csharp
canvasAnimationState.SyncTracksFromDocument(document);
```

This updates track names, depths, and creates/removes tracks as needed.

### Effect ? Keyframe Binding

Effects are matched by `EffectId` (string). When applying keyframes:
1. Find effect with matching ID in layer's Effects collection
2. Call `effectKeyframeData.ApplyTo(effect)`

---

## Future Considerations

### Potential Enhancements

1. **Auto-Interpolation for Layer Properties**
   - Currently hold-frame only
   - Could add tweening for opacity, position (if we add transforms)

2. **Audio Track Support**
   - Waveform visualization in timeline
   - Sync markers for lip-sync/timing

3. **Timeline Recording (Timelapse)**
   - Capture from undo history
   - Export as animation

4. **Graph Editor**
   - Visual curve editing for easing
   - Per-property animation curves

5. **Nested Compositions**
   - Pre-compose layers into reusable animated components

---

## Common Patterns

### Adding a New Animatable Property

1. Add property to `LayerKeyframeData`
2. Update `CaptureKeyframe()` to capture it
3. Update `ApplyFrameToDocument()` to apply it
4. Update `WriteLayerKeyframe()` / `ReadLayerKeyframe()` in DocumentIO
5. Increment file format version if breaking change

### Adding a New Effect Property for Animation

Effect properties are automatically captured via reflection if they are:
- Public
- Have both getter and setter
- Are value types, strings, or enums

No code changes needed unless special handling required.

---

## Testing Checklist

When modifying animation code:

- [ ] Create keyframe, navigate away, navigate back - data preserved?
- [ ] Save document, reload - animation intact?
- [ ] Add/remove layers - tracks sync correctly?
- [ ] Rename layer - track name updates?
- [ ] Export GIF - frames render correctly?
- [ ] Stage enabled - preview shows correct viewport?
- [ ] Effect animation - settings change between keyframes?
- [ ] Old file version - loads without errors?
