# Changelog

All notable changes to PixlPunkt will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-12-23

### Added

#### Core Features
- Modern pixel art editor built with WinUI 3 for Windows 10/11
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
- **Custom Brushes** - Export and import brush definitions (.pbx)

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
