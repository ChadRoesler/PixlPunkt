# Game Art

Creating pixel art assets for games using PixlPunkt.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/grid_16.png" width="16"> Resolution & Scale

### Choosing a Resolution

| Resolution | Style | Examples |
|------------|-------|----------|
| 8×8 | Icons, tiny | NES items |
| 16×16 | Small sprites | Most retro games |
| 32×32 | Medium sprites | SNES, GBA |
| 64×64 | Detailed | PS1 era |
| 128×128 | High detail | Modern indie |

### Consistency is Key

Pick a base resolution and stick to it:
- All characters same size
- UI elements proportional
- Tiles match sprite scale

### Screen Resolution

Common game resolutions:
- **160×144** - GameBoy
- **256×224** - NES
- **320×240** - Common 4:3
- **384×216** - 16:9 pixel art
- **640×360** - Larger 16:9

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_16.png" width="16"> Color Palettes for Games

### Classic Console Palettes

PixlPunkt includes authentic palettes:
- **NES** - 54 colors (typical: 4 per sprite)
- **GameBoy** - 4 shades of green
- **SNES** - 256 colors (15 per sprite + transparent)
- **Genesis** - 64 colors (15 + transparent per sprite)

### Modern Limited Palettes

Popular choices:
- **PICO-8** - 16 colors, fantasy console
- **DB16/DB32** - DawnBringer community palettes
- **AAP-64** - 64 colors, versatile

### Creating Your Own

Consider:
1. **Ramps** - 3-5 colors per hue (shadow ? highlight)
2. **Skin tones** - Usually 3-4 colors
3. **Environment colors** - Match your game's mood
4. **UI colors** - High contrast for readability

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> Character Sprites

### Basic Character Structure

```
    ??       Head (1-2 tiles)
   ????
  ??????     Body (1-2 tiles)
   ?  ?
   ?  ?      Legs (1 tile)
```

### Animation States

Typical character needs:

| State | Frames | Notes |
|-------|--------|-------|
| Idle | 2-4 | Subtle breathing |
| Walk | 4-8 | Loop |
| Run | 4-6 | Faster than walk |
| Jump | 3-5 | Rise, apex, fall |
| Attack | 3-6 | Anticipation + action |
| Hurt | 2-3 | Flash/knockback |
| Death | 4-8 | One-time |

### Readability Tips

1. **Silhouette** - Clear outline shape
2. **Contrast** - Stand out from background
3. **Action lines** - Show movement direction
4. **Consistent lighting** - Usually top-left

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_edit_16.png" width="16"> Tile-Based Environments

### Tile Set Organization

```
????????????????????????????????????
? Terrain                          ?
? ??????????  ??????????          ?
? ?TL?TM?TR?  ?  ?  ?  ? Corners  ?
? ??????????  ?????????? & Edges  ?
? ?ML?MM?MR?  ?  ?  ?  ?          ?
? ??????????  ??????????          ?
? ?BL?BM?BR?  ?  ?  ?  ?          ?
? ??????????  ??????????          ?
????????????????????????????????????
? Props & Details                  ?
? ??????????  ??????????          ?
? ???????  ?  ?  ?  ?  ?          ?
? ??????????  ??????????          ?
????????????????????????????????????
```

### Auto-Tile Patterns

For terrain that connects:
- **4-connected** - 16 tiles (corners + edges)
- **8-connected** - 47 tiles (all combinations)
- **Wang tiles** - 16 tiles (edge matching)

### Seamless Tiles

Use [[Tiles]] tessellation feature:
1. Draw base tile
2. Preview tiling
3. Fix edge seams
4. Test in grid

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/square_16.png" width="16"> UI Elements

### UI Design Principles

1. **High contrast** - Easy to read
2. **Consistent borders** - Same style throughout
3. **Adequate padding** - Content doesn't touch edges
4. **Scalable** - 9-slice compatible

### 9-Slice UI

```
???????????????
? 1 ?  2  ? 3 ?  Corners don't stretch
???????????????  Edges stretch one way
? 4 ?  5  ? 6 ?  Center stretches both
???????????????
? 7 ?  8  ? 9 ?
???????????????
```

### Common UI Elements

- Health bars
- Buttons (normal, hover, pressed)
- Panels/windows
- Icons (items, abilities)
- Fonts (bitmap)
- Cursor sprites

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/play_16.png" width="16"> Animation for Games

### Sprite Sheet Export

Export animations as sprite sheets:
1. **File ? Export ? Animation** (select Sprite Strip format)
2. Choose batch export for multiple reels
3. Set scale if needed
4. Include padding if needed

### Frame Data

Document your animations:
```json
{
  "idle": { "start": 0, "count": 4, "fps": 8, "loop": true },
  "walk": { "start": 4, "count": 6, "fps": 12, "loop": true },
  "attack": { "start": 10, "count": 4, "fps": 15, "loop": false }
}
```

### Hitboxes & Collision

Consider while animating:
- When does attack connect?
- What's the hurtbox?
- Movement box vs visual

---

## Platform-Specific Tips

### Unity

- Import as sprite sheet
- Use **Sprite Editor** to slice
- Set **Filter Mode** to "Point (no filter)"
- **Compression** to "None"

### Godot

- Import as texture
- Split with **AtlasTexture**
- Set import preset to "2D Pixel"

### GameMaker

- Use sprite strips
- Set origin point consistently
- Collision masks per-frame if needed

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_16.png" width="16"> Asset Organization

### Folder Structure

```
game_assets/
??? characters/
?   ??? player/
?   ?   ??? player_idle.pxp
?   ?   ??? player_walk.pxp
?   ?   ??? player_spritesheet.png
?   ??? enemies/
??? tiles/
?   ??? grass_tileset.pxp
?   ??? grass_tileset.png
??? ui/
?   ??? buttons.pxp
?   ??? health_bar.png
??? fx/
    ??? explosions.pxp
    ??? particles.png
```

### Naming Conventions

Be consistent:
- `character_action_direction.png`
- `tileset_biome.png`
- `ui_element_state.png`

Examples:
- `player_walk_right.png`
- `tileset_forest.png`
- `btn_play_hover.png`

---

## Performance Considerations

### Texture Atlas

Combine sprites into atlases:
- Reduces draw calls
- More efficient memory
- PixlPunkt can export combined sheets

### Power of 2

Some engines prefer power-of-2 textures:
- 64×64, 128×128, 256×256, 512×512
- Or 256×128, etc.
- Prevents scaling artifacts

### Color Depth

- **Indexed** - Smaller files, faster loading
- **True color** - More flexibility, larger

---

## Style Guides

### Consistency Checklist

- [ ] Same resolution throughout
- [ ] Consistent palette
- [ ] Same lighting direction
- [ ] Matching outline style
- [ ] Proportional sizes
- [ ] Similar animation speeds

### Documentation

Create a style guide including:
- Palette (with color codes)
- Size reference sheet
- Lighting reference
- Do's and don'ts
- Example sprites

---

## See Also

- [[Tiles]] - Tile system
- [[Tile Animation|Tile-Animation]] - Animated tiles
- [[Palette]] - Color management
