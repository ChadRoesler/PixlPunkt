# Voxel Workspace

Build, edit, and export voxel models directly inside the document tab.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_lightning_16.png" width="16"> Open the Workspace

1. Open a document that has tiles.
2. Go to **Tiles → Voxel Preview…**.
3. PixlPunkt opens a split workspace:
   - left: 2D canvas
   - right: voxel workspace

You can resize the split with the divider handle between panes.

---

## Workspace Layout

| Area | What It Does |
|------|---------------|
| **Viewport Tool Rail** | Active voxel tool buttons (paint, dropper, erase, create, delete, select, move, lighting) |
| **Left Sidebar** | Face mapping, display, lighting, voxel edit actions, build/export actions |
| **Viewport** | 3D voxel render with orbit camera, axis gizmo, and optional light handle |
| **Bottom Camera Strip** | Iso/cardinal presets, reset, focus/reset light actions |

---

## Face Mapping

PixlPunkt supports two source mapping modes:

| Mode | Behavior |
|------|----------|
| **3 Faces (mirrored)** | `Front/Back`, `Left/Right`, `Top/Bottom` share three source tiles |
| **6 Faces (individual)** | Each face gets its own tile (`Front`, `Back`, `Left`, `Right`, `Top`, `Bottom`) |

`Color Linking` can be enabled for seam-friendly color behavior while face painting.

---

## Camera and Navigation

| Input | Action |
|------|--------|
| `LMB drag` on viewport | Orbit camera |
| Mouse wheel | Zoom camera |
| Iso/Cardinal preset + `Go` | Snap to preset orientation |
| `Reset` | Reset camera to default |
| Axis gizmo click | Snap to matching axis orientation |

The active pane gets an accent indicator line, so you can tell whether canvas or voxel input is focused.

---

## Display Controls

| Option | Purpose |
|--------|---------|
| **Outline** | Silhouette outline around the rendered model |
| **Pixel Preview** | Pixel-stable presentation mode |
| **Pixel Preview Antialiasing** | Per-pixel AA (render-space, not post-scale blur) |
| **AA Strength** | Strength for Pixel Preview AA |
| **Pixel Base** | Base pixel density for pixel preview |
| **Backdrop Cage** | 3D guide cage around the model |
| **Backdrop Projection Tiles** | Tile projections on cage faces |
| **Cage Distance** | Distance/scale of cage from model |
| **Model Voxel Grid** | Grid overlay on visible voxel surface boundaries |

---

## Lighting Controls

Lighting is non-destructive preview shading and can be toggled independently.

| Control | Purpose |
|---------|---------|
| **Enable lighting** | Toggle point-light shading on/off |
| **Light color** | Tint for lit contribution |
| **Shadow color** | Tint for shadowed contribution |
| **Shadow** | Overall shadow strength |
| **Intensity** | Direct light intensity |
| **Falloff** | Distance attenuation |
| **Pos X/Y/Z** | Light world position |
| **Cast shadows** | Hard voxel cast shadows |

When enabled, a draggable light handle appears in the viewport.

---

## Voxel Edit + Actions

### Voxel Edit
- `Clear Sel` clears selection.
- `Expand` grows to connected voxels.
- `±X/±Y/±Z` nudges the selected voxels by one unit.

### Actions
- `Reload Tiles` refreshes face tile sources from the document.
- `Reload + Build` refreshes sources and rebuilds the model.
- `Build Voxel` rebuilds voxel data from current mappings.
- `Export Image…` exports WYSIWYG viewport output with scale/background/overlay toggles.
- `Export Model…` exports model geometry in OBJ, GLB, STL, or VOX.

### Model Export Matrix

| Format | Includes | Best Use | Notes |
|--------|----------|----------|-------|
| **OBJ + MTL + PNG** | Mesh + external texture atlas | DCC tools, broad compatibility | Sidecar files (`.obj`, `.mtl`, `.png`) are written together |
| **GLB** | Mesh + embedded texture atlas | Single-file glTF workflows | Supports **Double-sided material** toggle |
| **STL (binary)** | Geometry only | 3D print / mesh-only pipelines | No texture/material/color payload |
| **VOX (MagicaVoxel)** | Voxel occupancy + palette | Voxel-native tools | Max `255` units per axis after axis transform; unit scale and pivot are ignored |

---

## Keyboard Shortcuts (Viewport Focus)

| Shortcut | Action |
|----------|--------|
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo voxel edits |
| `Delete` | Delete selected voxels |
| Arrow keys | Move voxel selection on X/Y |
| `PageUp` / `PageDown` | Move voxel selection on Z |
| `Alt` + Arrow / `PageUp` / `PageDown` | Nudge light position |
| `Alt+Shift` + Arrow / `PageUp` / `PageDown` | Larger light nudge |

---

## See Also

- [[Voxel Tools|Voxel-Tools]]
- [[Tiles]]
- [[Interface]]
- [[Tools]]
