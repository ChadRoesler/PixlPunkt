# Layers

Layers are fundamental to organizing your artwork in PixlPunkt. This page covers everything about layer management, blend modes, and organization.

## Layer Types

### Raster Layers
Standard pixel layers for drawing. This is what you'll use 99% of the time.

### Layer Folders
Organize multiple layers into collapsible groups. Great for:
- Character parts (head, body, arms, legs)
- Background elements
- Effect layers
- Animation states

---

## Layer Panel

```
?????????????????????????????????????????
? Layers                          + ?? ??    ?
?????????????????????????????????????????
? ?? ?? ? [preview] ? Foreground        ?   ?
? ?? ?? ? [preview] ? Character         ?   ?
? ?? ?? ? [preview] ? ?? Effects            ?
?       ?           ?   ? Glow layer        ?
?       ?           ?   ? Shadow layer      ?
? ?? ?? ? [preview] ? Background        ?   ?
?????????????????????????????????????????
```

### Panel Toolbar Buttons

| Icon | Name | Action |
|:----:|------|--------|
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_20.png" width="20"> | Add Layer | Create a new raster layer above current |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_add_20.png" width="20"> | Add Folder | Create a new layer folder |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/delete_20.png" width="20"> | Delete | Remove the selected layer or folder |

### Layer Row Icons

| Icon | Name | Action |
|:----:|------|--------|
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_20.png" width="20"> | Visibility | Toggle layer visible/hidden (click to toggle) |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lock_closed_20.png" width="20"> | Lock | Prevent editing this layer (click to toggle) |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/glasses_20.png" width="20"> | Effects | Toggle layer effects on/off |
| <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_20.png" width="20"> | Settings | Open layer properties dialog |

---

## Layer Operations

### Creating Layers
- Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> **Add Layer** button in the panel toolbar
- **Ctrl+Shift+N** - New layer dialog
- **Right-click ? New Layer** - Context menu

### Selecting Layers
- **Click** layer in panel
- **Ctrl+Click** on canvas - Auto-select layer under cursor

### Reordering Layers
- **Drag and drop** in the layer panel
- Layers render bottom-to-top (bottom = back, top = front)

### Duplicating Layers
- **Right-click ? Duplicate**
- **Ctrl+J** - Duplicate current layer

### Merging Layers
- **Right-click ? Merge Down** - Combine with layer below
- **Ctrl+E** - Merge down
- **Right-click folder ? Flatten** - Merge all folder contents

### Deleting Layers
- Select + **Delete** key
- Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/delete_16.png" width="16"> **Delete** button
- **Right-click ? Delete**

---

## Layer Settings

Double-click a layer or click the <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_16.png" width="16"> **Settings** button to open:

### Basic Properties

| Property | Description |
|----------|-------------|
| **Name** | Layer name (for organization) |
| **Opacity** | Layer transparency 0-255 (0 = invisible, 255 = opaque) |
| **Blend Mode** | How layer combines with layers below |
| **Solo** | Show only this layer (hide all others) |

### Visibility States

| State | Icon | Description |
|-------|:----:|-------------|
| Visible | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_16.png" width="16"> | Layer renders normally |
| Hidden | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_off_16.png" width="16"> | Layer doesn't render |
| Solo | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_tracking_16.png" width="16"> | Only this layer visible |
| Locked | <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lock_closed_16.png" width="16"> | Can't edit, but renders |

---

## Blend Modes

Blend modes control how a layer's pixels combine with the layers beneath it.

### Available Blend Modes

| Mode | Effect | Use Case |
|------|--------|----------|
| **Normal** | Standard layering, opacity controls transparency | Default |
| **Multiply** | Multiplies colors (always darker) | Shadows, shading |
| **Screen** | Inverse multiply (always lighter) | Glow, light |
| **Add** | Adds values directly | Glow effects, fire |
| **Subtract** | Subtracts colors (always darker) | Effects |
| **Darken** | Keeps darker pixels | Shadows |
| **Lighten** | Keeps lighter pixels | Highlights |
| **Overlay** | Multiply darks, Screen lights | General contrast |
| **HardLight** | Strong contrast | Dramatic lighting |
| **Difference** | Subtracts colors | Psychedelic effects |
| **Invert** | Inverts where opaque | X-ray effects |

---

## Blend Mode Examples

### Shadows with Multiply
1. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> to create new layer above your art
2. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_16.png" width="16"> and set blend mode to **Multiply**
3. Paint with dark purple/blue
4. Result: Natural-looking shadows that preserve underlying detail

### Glow with Add
1. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> to create new layer above your art
2. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_16.png" width="16"> and set blend mode to **Add**
3. Paint with light colors
4. Result: Bright glow effect that intensifies

### Contrast with Overlay
1. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/add_16.png" width="16"> to create new layer, fill with a color
2. Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/settings_16.png" width="16"> and set blend mode to **Overlay**
3. Adjust opacity slider
4. Result: Enhanced contrast while preserving midtones

---

## Layer Folders

### Creating Folders
- Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_add_16.png" width="16"> **Add Folder** button
- **Select multiple layers + Right-click ? Group** - Folder from selection

### Folder Behavior
- Folders can be collapsed/expanded (click the <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/chevron_right_16.png" width="16"> arrow)
- Hiding a folder hides all children
- Locking a folder locks all children
- Effects can be applied to folders

### Nested Folders
Folders can contain other folders.

```
?? Character
?? ?? Head
?   ?? Face
?   ?? Hair
?   ?? Accessories
?? ?? Body
?   ?? Torso
?   ?? Arms
?? ?? Legs
    ?? Upper
    ?? Lower
```

---

## Layer Locking

Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/lock_closed_16.png" width="16"> to lock a layer:

| State | Effect |
|-------|--------|
| **Unlocked** | Can edit normally |
| **Locked** | Can't edit anything |

When locked:
- Can't paint on the layer
- Can't move layer contents
- Layer still renders normally

---

## Layer Tips

### Organization
- Name your layers! "Layer 1" is useless
- Use folders <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/folder_16.png" width="16"> to group related elements

### Performance
- Flatten finished areas to reduce layer count
- Large documents with many layers can slow down
- Click <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/eye_16.png" width="16"> to hide complex layers while working

### Workflow
- Work on separate layers for different elements
- Keep adjustment/effect layers separate
- Use folders to manage complexity

### Animation
- Each layer can be animated independently
- Consider which elements need separate keyframes
- Use folders for animated vs. static elements

---

## See Also

- [[Masks]] - Non-destructive layer masking
- [[Effects]] - Layer effects (shadow, glow, etc.)
- [[Canvas Animation|Canvas-Animation]] - Animating layers
