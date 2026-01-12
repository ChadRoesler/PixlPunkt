# File Formats

Import and export formats supported by PixlPunkt.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/document_16.png" width="16"> Native Format (.pxp)

### PixlPunkt Project

**Extension:** `.pxp`

The native format preserves everything:
- All layers with names and properties
- Full animation data
- Keyframes and timing
- Stage (camera) keyframes
- Layer effects
- Tile mappings
- Palette
- Undo history (optional)

**Always save as `.pxp` while working!**

### Related Formats

| Extension | Description |
|-----------|-------------|
| `.pxp` | Full project |
| `.pxpt` | Project template |
| `.pxpr` | Tile animation reel |
| `.pbx` | Custom brush |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/image_16.png" width="16"> Image Formats

### PNG (Recommended)

**Import:** ? Full support
**Export:** ? Full support

| Feature | Support |
|---------|---------|
| Transparency | ? Full alpha |
| Bit depth | 8-bit indexed, 24/32-bit |
| Animation | ? APNG export |
| Metadata | Preserved |

**Best for:**
- Final exports
- Game assets
- Web graphics
- Transparency needed

### GIF

**Import:** ? Full support (animation)
**Export:** ? Full support

| Feature | Support |
|---------|---------|
| Transparency | 1-bit only |
| Colors | 256 max |
| Animation | ? Yes |
| Optimization | Frame diff, LZW |

**Best for:**
- Simple animations
- Social media
- Retro aesthetic
- Small file sizes

### JPEG

**Import:** ? Full support
**Export:** ? Full support

| Feature | Support |
|---------|---------|
| Transparency | ? No |
| Quality | Lossy compression |
| Colors | 24-bit |

**Best for:**
- Photographic references
- NOT recommended for pixel art (lossy)

### BMP

**Import:** ? Full support
**Export:** ? Full support

| Feature | Support |
|---------|---------|
| Transparency | Limited |
| Compression | None |
| Compatibility | Very high |

**Best for:**
- Legacy compatibility
- Uncompressed archival

### TIFF

**Import:** ? Full support
**Export:** ? Full support

| Feature | Support |
|---------|---------|
| Bit depth | Multiple |
| Layers | ? Flattened |
| Compression | Optional |

**Best for:**
- Print workflow
- High-quality archival

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Animation Formats

### APNG (Animated PNG)

**Export:** ? Full support

Better than GIF:
- Full alpha transparency
- More than 256 colors
- Smaller files often
- Falls back to static PNG

### GIF Animation

**Export:** ? Full support

Options:
- Frame delay (per-frame)
- Loop count
- Transparency index
- Dithering
- Color optimization

### Video Formats

**Export:** ? Via FFmpeg

| Format | Codec | Best For |
|--------|-------|----------|
| MP4 | H.264 | Universal playback |
| AVI | Various | Windows legacy |
| WMV | WMV9 | Windows |
| WebM | VP9 | Web |

**Requirements:**
- FFmpeg installed or auto-downloaded
- First video export may take longer

### PNG Sequence

**Export:** ? Full support

Exports each frame as separate PNG:
```
animation_0001.png
animation_0002.png
animation_0003.png
...
```

**Best for:**
- Video editing software
- Game engines
- Maximum quality

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/arrow_import_16.png" width="16"> Import Formats

### Aseprite (.ase, .aseprite)

**Import:** ? Full support

Imports:
- Layers
- Frames
- Tags ? Separate animations
- Palette
- Cel timing

### PyxelEdit (.pyxel)

**Import:** ? Full support

Imports:
- Layers
- Tiles
- Tile animations
- Palette

### Tiled (.tmx, .tsx)

**Import:** ? Full support

Imports:
- Tile sets (.tsx)
- Tile maps (.tmx)
- Layer structure
- Object layers (as reference)

### ICO / CUR

**Import:** ? Full support
**Export:** ? Full support

Icon formats:
- Multi-resolution support
- Import largest size
- Export all sizes

Options for export:
- 16×16
- 32×32
- 48×48
- 256×256

### Adobe ASE (Palette)

**Import:** ? Palette only
**Export:** ? Palette only

Adobe Swatch Exchange for palettes.

### GIMP GPL (Palette)

**Import:** ? Palette only
**Export:** ? Palette only

GIMP palette format.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_16.png" width="16"> Sprite Sheet Export

### Layout Options

| Layout | Description |
|--------|-------------|
| **Horizontal** | All frames in one row |
| **Vertical** | All frames in one column |
| **Grid** | Rows × Columns |
| **Packed** | Optimal packing (irregular) |

### Settings

| Setting | Description |
|---------|-------------|
| **Cell Width** | Width of each frame cell |
| **Cell Height** | Height of each frame cell |
| **Padding** | Space between frames |
| **Border** | Space around entire sheet |
| **Trim** | Remove empty space |

### Metadata Export

Export alongside sprite sheet:
- **JSON** - Frame coordinates, timing
- **XML** - Similar data, XML format
- **Custom** - Define your own format

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/arrow_export_16.png" width="16"> Export Options

### Scale

Export at multiple sizes:
- **1×** - Original size
- **2×** - Double (common for pixel art)
- **4×** - Quadruple
- **Custom** - Any integer multiplier

### Interpolation

| Mode | Description |
|------|-------------|
| **Nearest Neighbor** | Sharp pixels (recommended) |
| **Bilinear** | Smooth (not for pixel art) |
| **Bicubic** | Smoother (not for pixel art) |

Always use **Nearest Neighbor** for pixel art!

### Background

| Option | Description |
|--------|-------------|
| **Transparent** | Alpha channel preserved |
| **Color** | Solid background color |
| **Checkerboard** | Visual transparency indicator |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/plug_disconnected_16.png" width="16"> Plugin Formats

Plugins can add additional formats:

### Import Handlers

Plugins can add support for:
- Proprietary formats
- Game-specific formats
- Custom compression

### Export Handlers

Plugins can add:
- Custom export wizards
- Game engine integration
- Batch processing

See [[Plugin Development|Plugins]] for creating format handlers.

---

## Quick Reference

### For Game Development

| Use Case | Format |
|----------|--------|
| Sprite assets | PNG |
| Tile sets | PNG |
| Animation preview | GIF / APNG |
| Sprite sheets | PNG + JSON |

### For Web / Social

| Use Case | Format |
|----------|--------|
| Static image | PNG |
| Animation | GIF or APNG |
| Video | MP4 |

### For Archival

| Use Case | Format |
|----------|--------|
| Working files | .pxp (native) |
| Flat backup | PNG (lossless) |
| Print quality | TIFF or PNG |

---

## See Also

- [[Installation]] - File locations
- [[Game Art|Game-Art]] - Game asset workflow
- [[Canvas Animation|Canvas-Animation]] - Animation export
- [[Plugin Development|Plugins]] - Custom format handlers
