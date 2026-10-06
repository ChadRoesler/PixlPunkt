# Icon Export

Write a multi-resolution Windows `.ico` straight out of PixlPunkt, and draw the small sizes by hand instead of letting them be shrunk.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/arrow_upload_16.png" width="16"> Export an Icon

**File → Export → Icon…**

The dialog previews each size over four backgrounds, transparency stripes, white, grey and black, so you can see what the alpha is really doing before you commit.

Icons are written at 256, 128, 64, 48, 32 and 16 pixels, all in one file.

| Scale mode | Best for |
|------------|----------|
| **Bilinear** | Smooth artwork, illustration-style icons |
| **Nearest Neighbor** | Keeping hard pixel edges, at the cost of ragged diagonals |
| **EPX** and **Scale2x** | Pixel art being enlarged rather than reduced |

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/layer_16.png" width="16"> Drawing the Small Sizes Yourself

**Name a folder after a size and that folder becomes the artwork for it.**

| Folder name | Supplies |
|-------------|----------|
| `16x16` | The 16 pixel icon |
| `32x32` | The 32 pixel icon |
| `48` | The 48 pixel icon. The short form works too |
| `256x256` | The 256 pixel icon |

Spaces are fine, so is a capital X, and so is a real multiplication sign. A name that is not a plain square size, like `16px` or `32x16`, is treated as an ordinary folder and ignored.

### Why This Exists

Shrinking one detailed drawing down to 16 pixels turns it to mush, whatever scale mode you pick. Too much has to be thrown away, and the filter decides what goes rather than you.

A hand-drawn small version beats a scaled one, every time. At 16 pixels you are not shrinking the design, you are redrawing it: fewer colours, no fine detail, and usually no wordmark, because a word at 16 pixels is noise. The silhouette is the whole icon at that size.

> The same principle appears in the font editor as embedded bitmap strikes. A picture drawn for a size beats one derived for it.

### How It Behaves

- **Size folders are drawn at the document's own resolution** and scaled down from there, so you can work on them beside the large art with no second document to keep in step.
- **A size with no folder inherits from the nearest larger one.** With a `32x32` folder and no `16x16`, the 16 is shrunk from your hand-drawn 32 rather than from the master. Each size is already a simplification of the one above it, so that is a much better starting point. Only when nothing larger has a folder does it fall back to the master.
- **Inheritance only ever looks upward.** A 16 will never feed a 32, because detail cannot be invented on the way back up.
- **Size folders never appear in any other size.** Your 16 pixel drawing cannot leak into the 256 pixel icon.
- **Hiding a size folder does not stop it exporting.** You will want the small versions hidden while drawing the big one, and that should not quietly empty them. Hiding a layer *inside* the folder does hide it, as normal.

The preview says where each size came from: **drawn** for its own folder, **from 32** when it inherited, and **scaled** when it came from the master. A folder that is not being picked up is obvious in the dialog rather than after the file is written.

---

## A Suggested Setup

```
256x256   (or just leave the master art at root)
48x48     simplified, fewer colours
32x32     simplified further, no fine detail
16x16     silhouette only, two or three colours
Master artwork
```

Work top down. Draw the big one, then redraw it smaller each time, throwing something away at every step. If you cannot decide what to drop, that is the icon telling you it is carrying too much.

You do not need every size. Because a size inherits from the nearest larger one, a `48x48` and a `16x16` will carry 32 and 64 along with them. Add a folder only where the automatic result is not good enough.

> **One thing to watch:** a new folder is created inside whatever is selected in the layers panel. Deselect, or pick something at the top level, before making a size folder, or it will end up nested inside another one and be ignored.

---

## Next

- [[Layers]] - folders, visibility, and organising a document
- [[File Formats|Formats]] - everything PixlPunkt reads and writes
- [[Font Export|Font-Export]] - the same idea applied to type
