# Changelog

All notable changes to PixlPunkt will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### Added
- **View flips and rotation** - `Ctrl+F` / `Ctrl+Shift+F` mirror the canvas, `Ctrl+Shift+MMB` drag rotates it (15° snaps), View → Reset View Rotation. Non-destructive, undoable, never dirty the document; flips show in timelapse exports.
- **New from Clipboard** - File menu and the New Canvas dialog create a canvas the size of the copied image and paste it at the origin.
- **Auto Crop** on per-layer image export - trims each layer's file to its non-transparent pixels.
- **Renumber Tiles** (Tiles menu) - closes id gaps (1, 3, 45 → 1, 2, 3) across every mapping; one undo step.
- **Layer preview size** - Off / Small / Normal / Large from the layers panel menu or Settings → General; rows resize to match.
- **Layer Previews** and other view options persist per document.
- Shortcuts the docs already promised: `Ctrl+0` / `Ctrl+1` for Fit / Actual Size (alongside `Ctrl+Home` / `Ctrl+End`), `Ctrl+E` Export Image, `Ctrl+D` Deselect.
- `docs/CUSTOM_ICONS.md` - end-to-end guide for adding glyphs to the app's icon font.

### Changed
- **Selection is a shape.** The marquee is carried as a mask that scales, rotates and flips with the pixels. Transparent pixels inside the outline stay selected, clicking them moves the float, and the outline previews the chosen rotation method live while dragging.
- Voxel edits share the document's undo stack (one Ctrl+Z, one dirty flag).
- Deleting a tile clears every mapping cell and voxel side-tile slot that referenced it.
- Add Folder nests inside the selected folder; new and duplicated layers scroll into view.
- Pasting switches to the rectangle select tool; pastes are kept on-canvas when they fit.
- Middle-button panning keeps going when the pointer leaves the canvas.
- History items coalesce (a run of nudges is one undo) and multi-step gestures undo as one.

### Fixed
- Crash on close from a focus event after the window was gone.
- Access violation on exit while the canvas carried a view transform.
- Save while a selection is floating writes what you see instead of a hole.
- Undo of a scale/rotate restored the pixels' silhouette instead of the marquee.
- Selection outline lost its off-canvas part after undo/redo, and its excess when scaled past the canvas.
- Static selection outline drew only its top edge while dragging.
- New Canvas "+" button sat below the tab strip after session restore.
- Autosave raced the UI thread; a throwing plugin effect no longer crashes the compositor.

## [1.0.0] - 2025-12-23

### Added

#### Core Features
- Modern cross-platform pixel art editor built on Uno Platform/Skia Desktop
- Tile-based canvas system with configurable tile and canvas sizes
- Multi-document support with tabbed interface
- Session recovery and auto-save functionality
- Recent documents tracking

#### Drawing Tools
- **Brush** - Configurable size, shape, opacity, and density
- **Eraser** - Remove pixels with configurable brush settings
- **Fill (Bucket)** - Flood fill with tolerance and contiguous mode
- **Gradient Brush** - Paint with cycling gradient colors
- **Color Replacer** - Replace specific colors while painting
- **Blur** - Soften pixels by averaging neighbors
- **Jumble** - Randomly rearrange pixels for scatter effects
- **Smudge** - Push and blend colors in brush direction

#### Gradient Fill Tool
- **Gradient Types** - Linear, Radial, Angular, Diamond
- **Ordered Dithering** - Bayer 2x2/4x4/8x8, Checker, Diagonal, Crosshatch, Blue Noise
- **Error Diffusion** - Floyd-Steinberg, Atkinson, Riemersma (Hilbert curve)
- **Multi-Color Gradients** - Custom gradients with palette-aware dithering
- **Controls** - Adjustable strength, scale, opacity, and reverse option

#### Selection Tools
- **Rectangle Select** - Marquee selection with add/subtract modifiers
- **Magic Wand** - Select by color similarity with tolerance
- **Lasso** - Freeform polygon selection
- **Paint Selection** - Brush-based selection mode
- **Selection Transforms** - Move, scale, rotate, flip operations

#### Shape Tools
- **Rectangle** - Filled or outlined rectangles/squares
- **Ellipse** - Filled or outlined ellipses/circles

#### Layer System
- Multiple raster layers with opacity and blend modes
- Layer folders for organization
- Layer visibility and locking
- Layer masks for non-destructive editing
- Merge down and flatten operations
- Blend modes: Normal, Multiply, Screen, Overlay, Add, Subtract, Difference, Darken, Lighten, Hard Light, Invert

#### Layer Effects (Non-Destructive)
- **Stylize** - Drop Shadow, Outline, Glow/Bloom, Chromatic Aberration
- **Filter** - Scan Lines, Grain, Vignette, CRT, Pixelate
- **Color** - Color Adjust, Palette Quantize, ASCII Art
- All effects are fully animatable with keyframe support

#### Animation System
- **Canvas Animation** - Full layer-based animation with keyframes
  - Per-layer keyframes storing complete state (pixels, visibility, opacity, blend mode, effects)
  - Hold-frame behavior between keyframes
  - Onion skinning with configurable frames before/after
  - Playback controls with adjustable FPS
- **Tile Animation** - Sprite sheet sequencing
  - Named animation reels
  - Per-frame timing control
  - Loop and ping-pong modes
- **Stage (Camera) System** - Virtual camera with interpolated transforms
  - Position, scale, rotation keyframes
  - Multiple easing options (Linear, EaseIn, EaseOut, EaseInOut, Bounce, Elastic)
- **Audio Reference Tracks** - Sync animations to music
  - Waveform visualization
  - Multiple audio track support
  - Frame offset and volume controls

#### Tile System
- Create and manage tile sets
- Per-layer tile mappings
- Tile Stamper and Tile Modifier tools
- Tile tessellation window for seamless pattern creation
- Import Tiled (.tmx/.tsx) files

#### Color Management
- HSL color picker with ladder controls
- Shade, tint, tone, and hue variation bars
- Gradient generator
- Palette presets: NES, GameBoy, C64, CGA, EGA, VGA, PICO-8, and more
- Custom palette import/export (JSON, JASC, GPL formats)
- Color extraction from images

#### File Support
- **Native Format** - `.pxp` with full feature preservation (layers, animation, masks, effects)
- **Import** - PNG, BMP, JPEG, TIFF, Aseprite (.ase/.aseprite), PyxelEdit (.pyxel), ICO, CUR, Tiled (.tmx/.tsx)
- **Export** - PNG, GIF (animated), MP4/AVI/WMV (video), BMP, JPEG, TIFF, ICO, CUR
- **Custom Brushes** - Export and import brush definitions (.mrk)

#### User Interface
- Dockable panels (Preview, Palette, Layers, Tiles, History, Animation)
- Customizable keyboard shortcuts
- Rulers and guides with snap support
- Multiple zoom levels with fit-to-screen option
- Pixel and tile grid overlays

#### Plugin System
- Extensible architecture via Plugin SDK
- Create custom tools, effects, and import/export handlers
- Fluent builder APIs for easy registration
- Dynamic UI generation for tool options

### Plugin SDK (v1.0.0)
- `IPlugin` interface for plugin entry points
- `ToolBuilders` for brush, shape, selection, tile, and utility tools
- `EffectBuilders` for layer effects
- `ImportBuilders` and `ExportBuilders` for file format handlers
- `IToolOption` system for dynamic toolbar UI
- Plugin window support via `PluginWindowDescriptor`
- Full XML documentation

---

## Version History

Future releases will follow this format:

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
- New features

### Changed
- Changes in existing functionality

### Deprecated
- Soon-to-be removed features

### Removed
- Removed features

### Fixed
- Bug fixes

### Security
- Security fixes
```

[1.0.0]: https://github.com/ChadRoesler/PixlPunkt/releases/tag/v1.0.0
