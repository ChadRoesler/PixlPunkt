Place custom UI icon fonts here.

Current runtime expectations:
- File path: `Assets/Fonts/PixlPunktIcons.ttf`
- Font family name inside the font: `PixlPunktIcons`
- Voxel delete glyph codepoint: `U+E900` (`CubeSubtract`)
- Voxel move glyph codepoint: `U+E901` (`CubeMove`)

If you change the file name or family name, update:
- `PixlPunkt/UI/Icons/PixlPunktIconFont.cs`

When adding new icons:
- Add a new value to `PixlPunktCodicon` in `PixlPunkt/UI/Icons/PixlPunktIconFont.cs`
- Keep codepoints stable once assigned (avoid reshuffling existing values)
