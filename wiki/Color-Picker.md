# Color Picker

The complete guide to PixlPunkt's color selection tools.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/color_16.png" width="16"> Opening the Color Picker

- **Click** the foreground or background color swatch
- **Double-click** any palette swatch
- Press `F4` (if configured)

---

## Color Picker Layout

```
???????????????????????????????????????????
?            HSL Square                   ?
?     ???????????????????????             ?
?     ?                     ?  ?????????  ?
?     ?    Saturation ?     ?  ?       ?  ?
?     ?         ?           ?  ?  Hue  ?  ?
?     ?      Lightness      ?  ? Slider?  ?
?     ?                     ?  ?       ?  ?
?     ???????????????????????  ?????????  ?
???????????????????????????????????????????
?  Ladder Controls                        ?
?  H: [????????]                         ?
?  S: [????????]                         ?
?  L: [????????]                         ?
???????????????????????????????????????????
?  Hex: [#FF5500]   RGB: 255, 85, 0      ?
???????????????????????????????????????????
?  Recent Colors: ? ? ? ? ? ? ? ?        ?
???????????????????????????????????????????
```

---

## HSL Color Model

PixlPunkt uses **HSL** (Hue, Saturation, Lightness):

### Hue (0-360°)
The color itself:
- **0°** = Red
- **60°** = Yellow
- **120°** = Green
- **180°** = Cyan
- **240°** = Blue
- **300°** = Magenta

### Saturation (0-100%)
Color intensity:
- **0%** = Grayscale
- **50%** = Muted
- **100%** = Vivid

### Lightness (0-100%)
Brightness:
- **0%** = Black
- **50%** = Pure color
- **100%** = White

---

## HSL Square

The main color selection area:

### How to Use

1. **Hue Slider** (right) - Select the base color
2. **Square** - Fine-tune saturation and lightness
   - **Left ? Right** = Saturation (gray to vivid)
   - **Top ? Bottom** = Lightness (light to dark)

### Quick Selection

- Click anywhere in the square
- Drag to fine-tune
- Current selection shown as circle marker

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/sliders_16.png" width="16"> Ladder Controls

Precise adjustment sliders:

### HSL Ladders

Each component has a visual ladder:
```
H: [????????] ? Shows hue spectrum
S: [????????] ? Gray to saturated  
L: [????????] ? Dark to light
```

### Using Ladders

- **Click** on ladder to set value
- **Drag** for fine adjustment
- **Scroll** while hovering for precision
- Shows preview of color at each level

### Ladder Benefits

- See how color changes across each axis
- Quick access to lighter/darker variants
- Easy to find complementary shades

---

## Input Methods

### Hex Input

Enter colors directly:
- Format: `#RRGGBB` or `RRGGBB`
- Example: `#FF5500` or `FF5500`
- Press Enter to apply

### RGB Values

Numeric input for Red, Green, Blue:
- Range: 0-255 each
- Click value to edit
- Tab between fields

### HSL Values

Numeric input for Hue, Saturation, Lightness:
- H: 0-360
- S: 0-100
- L: 0-100

---

## Recent Colors

The picker remembers recently used colors:

- Shows last 16 colors
- Click to quickly reselect
- Persists across sessions
- Automatically updates as you work

---

## Color Operations

### Copy Color
- Click **Copy** button
- Or `Ctrl+C` with picker open
- Copies hex value to clipboard

### Paste Color
- Click **Paste** button
- Or `Ctrl+V`
- Accepts: hex, RGB, named colors

### Sample from Screen

1. Click **Sample** button <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eyedropper_16.png" width="16">
2. Click anywhere on your screen
3. Color is picked (even outside PixlPunkt!)

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/arrow_swap_16.png" width="16"> Compare Colors

When editing an existing color:

```
???????????????
? New  ? Old  ?
? ???? ? ???? ?
???????????????
```

- **New** = Currently selected color
- **Old** = Original color before editing
- Click **Old** to revert

---

## Quick Color Picking (Dropper)

Without opening the picker:

### From Canvas
- **Right-click** anywhere = Pick color
- Works with any tool selected

### Dropper Tool (`I`)
- Select Dropper tool
- Click to sample
- Shows color preview under cursor

### Modifier Keys

| Key + Click | Action |
|-------------|--------|
| Right-click | Sample to foreground |
| Shift+Right-click | Sample to background |
| Alt+Click | Add sampled color to palette |

---

## Color Harmony Tools

### Complementary
Find the opposite color:
- **[?] Menu** ? **Complementary**
- Shows color 180° opposite on color wheel

### Analogous
Colors next to current:
- **[?] Menu** ? **Analogous**
- Shows colors ±30° on color wheel

### Triadic
Three evenly spaced colors:
- **[?] Menu** ? **Triadic**
- Shows colors at 0°, 120°, 240°

### Split-Complementary
- Base color + two colors adjacent to complement
- Good for balanced palettes

---

## Opacity/Alpha

For colors with transparency:

### Alpha Slider

When enabled, shows alpha slider:
- **0** = Fully transparent
- **128** = 50% transparent
- **255** = Fully opaque

### Hex with Alpha

8-digit hex includes alpha:
- Format: `#RRGGBBAA`
- Example: `#FF550080` (50% opacity orange)

---

## Tips for Pixel Art Colors

### Limited Palettes
- Start with few colors (4-16)
- Add colors only when needed
- Too many colors = muddy art

### Value Contrast
- Ensure sufficient lightness difference
- Test in grayscale
- 30%+ difference for readability

### Saturation Balance
- Don't max out saturation
- 60-80% often looks better
- Reserve 100% for accents

### Hue Shifting
For shading:
- Shadows: Shift hue toward blue/purple
- Highlights: Shift hue toward yellow/orange
- More interesting than just darker/lighter

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `X` | Swap FG/BG colors |
| `Right-click` | Sample color from canvas |
| `Ctrl+C` | Copy hex value |
| `Ctrl+V` | Paste hex value |
| `Escape` | Close picker |
| `Enter` | Confirm and close |

---

## See Also

- [[Palette]] - Palette management
- [[Gradient Fill|Gradient-Fill]] - Gradient tool
- [[Dithering]] - Dithering techniques
- [[Tools]] - All tools reference
