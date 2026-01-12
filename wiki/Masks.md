# Layer Masks

Non-destructive editing with masks in PixlPunkt.

---

## What Are Layer Masks?

A layer mask controls which parts of a layer are visible:
- **White** = Fully visible
- **Black** = Fully hidden
- **Gray** = Partially visible (transparency)

Masks are **non-destructive** - the original pixels are preserved, just hidden.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> Creating a Mask

### Add Mask to Layer

1. Select the layer in the Layers panel
2. Click the **Add Mask** button <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> at the bottom of the panel
3. Or right-click layer ? **Add Layer Mask**

### Mask Options

| Option | Result |
|--------|--------|
| **Reveal All** | White mask (everything visible) |
| **Hide All** | Black mask (everything hidden) |
| **From Selection** | Selection = white, rest = black |
| **From Transparency** | Existing alpha becomes mask |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> Editing Masks

### Selecting the Mask

In the Layers panel, each masked layer shows two thumbnails:
- **Left thumbnail** = Layer content
- **Right thumbnail** = Mask

Click the **mask thumbnail** to edit the mask (white border indicates selection).

### Painting on Masks

| Color | Effect |
|-------|--------|
| **White** | Reveals pixels |
| **Black** | Hides pixels |
| **Gray** | Partial transparency |

Use any painting tool:
- Brush (`B`) - Paint white/black
- Eraser (`E`) - Erases mask (reveals layer)
- Fill (`G`) - Fill areas of mask
- Gradient (`Shift+G`) - Create gradient masks

### Quick Tip: X to Swap
Press `X` to swap foreground/background colors - quickly switch between revealing (white) and hiding (black).

---

## Mask Visualization

### View Mask Only
- `Alt+Click` on mask thumbnail
- Shows mask as grayscale image
- `Alt+Click` again to return to normal

### Disable Mask Temporarily
- `Shift+Click` on mask thumbnail
- Red X appears over mask
- Layer shows without mask applied
- `Shift+Click` again to re-enable

### Mask Overlay
- Right-click mask ? **Show Mask Overlay**
- Tints masked areas with red (like rubylith)
- Helps visualize what's hidden

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/link_16.png" width="16"> Mask Linking

By default, masks are **linked** to their layer (chain icon between thumbnails).

### Linked Behavior
- Moving the layer moves the mask
- Transforming the layer transforms the mask
- They stay aligned

### Unlinked Behavior
- Click the chain icon to unlink
- Move layer and mask independently
- Useful for repositioning content within a mask

---

## Mask Operations

### Apply Mask
- Right-click mask ? **Apply Mask**
- Permanently applies mask to layer pixels
- Mask is removed, transparent areas become actual transparency
- **Cannot be undone** after saving!

### Delete Mask
- Right-click mask ? **Delete Mask**
- Choose: **Apply** or **Discard**
- **Apply** = Keep the masked result
- **Discard** = Restore full layer

### Invert Mask
- Right-click mask ? **Invert**
- Swaps black and white
- Hidden areas become visible, visible become hidden

### Duplicate Mask
- Right-click mask ? **Duplicate to Layer**
- Creates a new layer from the mask
- Useful for complex mask editing

---

## Selection and Masks

### Create Mask from Selection
1. Make a selection (any selection tool)
2. Add a mask to a layer
3. Choose **From Selection**
4. Selection becomes white, rest becomes black

### Load Mask as Selection
- `Ctrl+Click` on mask thumbnail
- Loads white areas as selection
- Useful for selecting complex shapes

### Add/Subtract from Mask
- With mask selected:
- `Shift+Drag` = Add to mask (paint white)
- `Alt+Drag` = Subtract from mask (paint black)

---

## Practical Uses

### Soft Edges
1. Create hard-edged artwork
2. Add mask
3. Use soft brush on mask edges
4. Creates anti-aliased appearance while keeping original pixels

### Non-Destructive Cropping
1. Add "Hide All" mask
2. Paint white where you want to see
3. Original extends beyond visible area
4. Adjust anytime!

### Blend Two Images
1. Place images on separate layers
2. Add mask to top layer
3. Use gradient tool on mask
4. Creates smooth transition

### Complex Selections
1. Create rough selection
2. Convert to mask
3. Refine with brush
4. More control than selection tools alone

---

## Tips & Best Practices

### Start with Reveal All
For most cases, start with everything visible and paint black to hide.

### Use Soft Brushes
Soft brush edges create smooth mask transitions.

### Check Your Colors
Make sure you're painting with pure white (#FFFFFF) or pure black (#000000) for best results.

### Backup with Duplicates
Before applying a mask, duplicate the layer as a backup.

### Feathering
To feather a mask:
1. Load mask as selection
2. Modify ? Feather
3. Create new mask from feathered selection

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `\` | Toggle mask overlay |
| `Alt+Click mask` | View mask only |
| `Shift+Click mask` | Disable mask |
| `Ctrl+Click mask` | Load mask as selection |
| `X` | Swap FG/BG (white/black) |

---

## See Also

- [[Layers]] - Layer management
- [[Tools]] - Selection and painting tools
- [[Effects]] - Layer effects
- [[Quick Start|Quick-Start]] - Getting started
