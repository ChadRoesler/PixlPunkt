# Voxel Tools

Reference for built-in voxel editing tools in the Voxel Workspace.

---

## Tool Set

| Tool | Purpose | Typical Use |
|------|---------|-------------|
| **Face Paint** | Paint color on a picked voxel face | Manual seam fixes and detail paint |
| **Face Dropper** | Sample picked face color into FG/BG workflow | Color matching while editing |
| **Face Erase Override** | Remove face override and revert to mapped/base color | Undo manual face paint on specific faces |
| **Voxel Create** | Add voxel adjacent to picked face | Grow forms and add structure |
| **Voxel Delete** | Delete picked voxel | Carve silhouette and shape cleanup |
| **Voxel Select** | Select voxels for multi-step edits | Prepare region edits and transforms |
| **Voxel Move** | Move selected voxels by integer units | Reposition selected parts |
| **Lighting** | Edit viewport lighting controls | Preview shading before export |

---

## Selection Behavior

Selection modifiers:

| Modifier | Mode |
|----------|------|
| *(none)* | Replace selection |
| `Shift` | Add to selection |
| `Ctrl` | Toggle selection |
| `Alt` | Remove from selection |

Move behavior:
- Moves happen in integer voxel units.
- Collision is validated before commit.
- `Delete` removes current selection when viewport is focused.

---

## Face Workflow Tips

1. Start from tile-driven build output.
2. Use **Face Paint** for seam repair and directional touch-ups.
3. Use **Face Erase Override** when you want to fall back to source mapping.
4. Use **Voxel Create/Delete** after silhouette decisions are final.

This keeps source mapping edits and manual voxel edits predictable.

---

## Lighting Tool Scope

Lighting is preview-only and non-destructive:
- does not rewrite face colors
- can be toggled on/off instantly
- is controlled with light/shadow color, intensity, falloff, shadow strength, and position

For flat rendering, disable lighting.

---

## Plugin Voxel Tools

Voxel tools are SDK-extensible through the plugin voxel API.

- SDK docs: `PixlPunkt.PluginSdk/docs/VoxelTools.md`
- Wiki: [[SDK Reference|SDK-Reference]]

---

## See Also

- [[Voxel Workspace|Voxel-Workspace]]
- [[Tiles]]
- [[Tools]]
