# Font Editor

Draw a pixel font glyph by glyph, space it by hand or automatically, and see it as running text while you work.

A font in PixlPunkt is an ordinary document with extra meaning attached. The canvas is a sheet of cells, each cell is one character, and the tile grid is the cell grid. Everything you already know about drawing applies, because you are drawing on a normal canvas the whole time.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/grid_16.png" width="16"> Create a Font

**File → New Font…**

| Setting | What It Does |
|---------|--------------|
| **Font Name** | The family name, recorded in every export |
| **Em Box** | The design size in pixels. This is what "8 px font" means |
| **Drawing room** | Blank pixels around the em on every side, where overhanging ink can live |
| **Characters** | Printable ASCII, uppercase and digits, letters and digits, or your own list |
| **Monospace** | Every glyph advances by the em width instead of by its ink |

The dialog tells you the cell size, the sheet layout, and which pixel sizes the finished font renders evenly at. Read that last line before you commit to an em, because it does not change afterwards without reshaping the whole font.

---

## The Em and the Cell Are Different Things

This is the one idea the rest of the font editor rests on. Get it wrong and everything downstream is subtly off.

| Term | Meaning |
|------|---------|
| **Cell** | The box you draw in. It is the document's tile size |
| **Em** | The design size, a rectangle sitting inside the cell |
| **Drawing room** | The blank margin between them, two pixels a side by default |

The em is what the outside world measures your font by. A font with an 8 pixel em is an 8 pixel font, whatever its cell size, and it renders evenly at multiples of 8.

The drawing room exists so a glyph can put ink outside its own body. A swash, a heavy accent, a script letter that leans over its neighbour: all of them need somewhere to go. Without it, ink stops at the edge of the character's own box.

> **Why this matters:** a font whose em does not divide evenly into whole pixels renders with uneven stems at every size. Some letters get a two pixel stroke and others three, from the same design. If you have used FontStruct and wondered why your fonts looked lumpy at some sizes, this was why.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16"> The Canvas

Draw with every normal tool. The font-specific parts are drawn over the top.

| Guide | What It Is |
|-------|------------|
| **Baseline** | The line letters sit on. Dashed, drawn once per row of cells |
| **Cap height** | The top of the em. Dashed, same treatment |
| **Em box** | A faint outline inside each cell, shown only when the cell is larger than the em |
| **Origin post** | Where the pen lands for the focused glyph |
| **Advance post** | Where the pen moves on to, with a shaded band between the two |

The spacing posts are only drawn for the glyph you are working on and its immediate neighbours. Showing them on every cell at once makes it impossible to tell which belongs to what.

**Click inside a cell to choose that glyph.** The click is only noted, never consumed, so the same click still paints. Hovering deliberately does nothing, so moving the pointer across the sheet on your way somewhere else does not change what you are looking at.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/align_center_vertical_16.png" width="16"> The Metrics Tool

The baseline, cap height, origin and advance are all draggable, but only while the **metrics tool** is armed. It is the ruler button in the Glyphs panel.

While it is off, which is the default, the guides are drawn faded and ignore the pointer entirely. That is deliberate: on a font sheet you are forever drawing right on top of the baseline, and a brush must never lose a click to a guide.

Turning it on puts the drawing tools down. The tool rail shows nothing selected, nothing can paint, and your chosen tool comes back the moment you turn the metrics tool off or pick anything from the rail.

| Drag | Effect |
|------|--------|
| **Baseline or cap height** | Moves it for the whole font, since both are uniform across every glyph |
| **Origin post** | Changes the gap on the left while the advance post stays put |
| **Advance post** | Changes how far the pen moves |

Both posts stop at the edges of the glyph's own cell. Past that the pen would move further than the cell it came from, and the next glyph would land on ink belonging to this one.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_16.png" width="16"> The Glyphs Panel

A pane on the right of the canvas area, beside the artwork, listing every mapped character in codepoint order.

| Feature | Behaviour |
|---------|-----------|
| **Thumbnails** | Each glyph with a baseline hairline through it, so you can see alignment at a glance |
| **Dimmed labels** | A character that is mapped but not yet drawn |
| **Dot after a label** | That glyph's spacing was set by hand rather than fitted automatically |
| **Tooltip** | The character and its codepoint, for telling an apostrophe from a backtick |
| **Go to character** | Type a character to select it, or `U+1FAE` for one the keyboard will not produce |
| **Ruler button** | Arms the metrics tool |
| **Reset button** | Puts the selected glyph's spacing back to automatic, as one undo step |
| **Zoom buttons** | Larger or smaller cells in the list |

The status line reports how many glyphs exist and how many are still blank.

---

## The Glyph Strip

Along the bottom, in the place an animation timeline occupies for an ordinary document. A timeline says nothing about a typeface; seeing a letter next to its neighbours says everything.

It shows the glyph you are working on with one character either side, drawn at the font's real advances rather than on a uniform grid. Spacing only means anything in company.

| Control | Purpose |
|---------|---------|
| **Left and Right** | Hold a fixed character on that side. Empty follows the character set |
| **Zoom** | Larger or smaller, without changing anything about the font |
| **Preview button** | Opens the full preview window |

Pinning both sides is how spacing is actually judged. Put the same letter on both sides, then walk the alphabet through the middle and watch the gaps change.

The strip repaints as you drag, so the neighbours reflow live while you move an advance post.

---

## Spacing

Every glyph is fitted automatically until you touch it.

| Mode | How The Advance Is Decided |
|------|----------------------------|
| **Auto-fit** | Measured from the ink, plus the side bearing on each side |
| **Set by hand** | Whatever you dragged the posts to, ignored by later edits to the drawing |
| **Monospace** | Every glyph advances by the em width, whatever its ink |

A glyph with no ink at all, like a space, gets an advance of twice the side bearing.

**Ink is not limited by the advance.** The whole cell is drawn at the pen less the origin, while only the advance moves the pen. Give a glyph a narrow advance and wide ink and it will reach over the letters either side, which is exactly what a script face needs. The advance is bounded by the cell; the ink is not bounded by the advance.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/table_edit_16.png" width="16"> Changing a Font After You Start

**Edit → Edit Canvas…**

For a font document this dialog offers the em box, the drawing room and the character set instead of the usual tile size and tile counts, because a font's tile is its glyph cell and its tile count follows from how many characters it holds.

The anchor grid decides what happens to your artwork:

- **Growing the cell:** the anchor decides where the old content sits inside the new one.
- **Shrinking the cell:** the anchored edge is the one that keeps its pixels.

Guides move with the ink rather than staying on their old row numbers, so a glyph sitting on the baseline still is afterwards, and hand-set spacing shifts with it.

> **Anything destructive is stated twice.** A caution line appears under the controls the moment the settings would remove work, and pressing Save asks again, listing the characters being dropped and the old and new cell sizes. The whole reshape is a single undo step.

---

## Saving

Fonts save as **`.pxpf`**, a PixlPunkt Font. It is the same container as a `.pxp` project with the font's own information alongside: the character mapping, the em box, the guides, and every glyph's spacing.

Double-clicking a `.pxpf` opens it in PixlPunkt with the font tooling already in place.

---

## Next

- [[Font Export|Font-Export]] - preview, sprite sheets, and installable TrueType files
- [[File Formats|Formats]] - where `.pxpf` sits among the rest
- [[Tiles]] - the tile system a glyph sheet is built on
