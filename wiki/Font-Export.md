# Font Export

Preview a pixel font as running text, then ship it as a sprite sheet or as an installable TrueType file.

Everything on this page lives under the **Font** menu, which only appears while a font document is open.

---

## The One Rule

A pixel font is crisp at whole multiples of its em, and nowhere else.

An 8 pixel em gives you 8, 16, 24, 32, 40, 48 and so on. Ask for 45 and the renderer has to stretch a design drawn on an 8 pixel grid across 45 pixels, so some stems come out two pixels wide and others three. That is not a fault in the export, and no setting anywhere fixes it. It is what asking for a size between grid steps means.

PixlPunkt says which sizes are honest rather than hiding the limit, in the New Font dialog, the preview window and the export confirmation.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/preview_link_16.png" width="16"> Preview Font

**Font → Preview Font…**, or the preview button on the glyph strip.

A window showing your font as real text, laid out with the same code the exporters use, so what you see here and what ships cannot drift apart.

| Control | Purpose |
|---------|---------|
| **Sample text** | Anything you like, across as many lines as you like |
| **Drawn only** | Fills the sample with the characters that actually have ink |
| **Reset** | Puts the standard sample back |
| **Size** | Whole multiples of the em, each labelled with its pixel size |
| **Text and Behind** | Foreground and background colours |
| **Recolour** | On by default. Clear it to see a font drawn in more than one colour as it is |
| **Baselines** | Draws the baseline through each line of the sample |
| **Zoom** | Magnifies the view without changing the rendered size |

Zoom and size are deliberately separate. Size decides how the font is rendered; zoom decides how closely you are looking at that rendering. Both are whole numbers, so looking closer never invents a half pixel.

**Middle button drags the view, and the wheel zooms** about the pointer, the same as the main canvas.

The preview follows the document live. Edit a glyph or drag an advance and the sample updates while the window is open.

> If the preview is empty it says why: no characters mapped, nothing drawn anywhere, none of the sample's characters in the font, or present but not yet drawn. A blank preview is never a mystery.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/arrow_upload_16.png" width="16"> Export Sprite Sheet

**Font → Export Sprite Sheet…**

Writes a PNG of the glyph sheet, and unless you clear the checkbox a BMFont `.fnt` metrics file beside it. Most game engines and frameworks read that pair directly.

| Setting | Effect |
|---------|--------|
| **Face name** | Recorded in the metrics |
| **Size** | Whole multiples of the em. The sheet is magnified, never interpolated |
| **Also write BMFont metrics** | The `.fnt` file, pointing at the image by its own file name |

Two things about the output are worth knowing:

- **The sheet is your document, not a repacked atlas.** What you drew is what ships.
- **Each character's region is its whole cell, not its ink box.** Trimming would save a little space and throw away exactly the overhang the drawing room exists to allow.

The metrics record vertical placement against the em rather than the cell, so a cell with drawing room above it carries a negative offset. That is how the format expects ink above the line to be described, and it is what keeps your glyphs from sitting low in an engine.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/document_16.png" width="16"> Export TrueType Font

**Font → Export TrueType Font…**

Writes a `.ttf` you can install and use in any program.

There is no size to choose, because the outlines are the pixels. The file is crisp at every whole multiple of the em and nowhere else, and the confirmation afterwards tells you which sizes those are.

### What Makes It Crisp

| Measure | What It Does |
|---------|--------------|
| **Units per em is an exact multiple of the pixel grid** | A pixel becomes a whole number of font units and never lands between them. An 8 pixel em becomes 1024 units, at 128 per pixel |
| **A `gasp` table asking for grid fitting without grey** | Stops a rasteriser softening edges that were drawn as hard pixels |
| **Straight lines along pixel boundaries, every point on curve** | No curve fitting is attempted anywhere. A stem stays exactly as wide as you drew it |

Holes come out as holes, wound the opposite way to the outside, so a counter in an O or an A is properly cut out rather than filled.

### Embedded Sizes

Tick a size and that size is also stored as a picture inside the font. A reader then uses the picture rather than drawing the outline, so it comes out exactly as painted whatever the program does. Every other size still renders from the outlines.

The drawn size and double it are ticked by default. Each extra size makes the file bigger for a gain that only shows on a reader that does not grid-fit the way it was asked to. Very large multiples are not offered, because the fields these are stored in are single bytes.

Only the ink of each glyph is stored, not the whole cell, so the blank drawing room is not carried into every size.

---

## Checking the Result

Install the `.ttf` and type in any program at a multiple of your em, then at a size between multiples. The first will be sharp and the second will not. That difference is the rule on this page, working exactly as designed.

---

## Next

- [[Font Editor|Font-Editor]] - drawing and spacing the glyphs in the first place
- [[File Formats|Formats]] - every format PixlPunkt reads and writes
- [[Tile-Based Game Art|Game-Art]] - getting assets into an engine
