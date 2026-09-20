# Custom Icons (the PixlPunkt icon font)

End-to-end: from an SVG on disk to a glyph on a button. This is for icons built into the
app, not plugin icons.

PixlPunkt draws almost all of its UI icons from **Fluent UI System Icons** through the
`FluentIcons.WinUI` package (`<ic:FluentIcon Icon="Cube"/>` in XAML, `Icon.Cube` in code).
When Fluent has no glyph for what you need, or you want a variant it doesn't ship (a cube with
a minus, a stamp with a sync arrow), you add it to the app's own icon font,
`PixlPunktIcons.ttf`, and reference it by codepoint.

---

## The pieces

| Piece | Where | Purpose |
|---|---|---|
| Raw SVGs | `PixlPunkt/Assets/Fonts/rawSvg/` | Source art, one glyph per file, 20×20 Fluent-style. Kept so the font can be rebuilt. |
| IcoMoon project | `PixlPunkt/Assets/Fonts/PixlPunktIcons.json` | The saved [IcoMoon](https://icomoon.io/app) project. Holds which SVGs are in the font and their codepoints. |
| The font | `PixlPunkt/Assets/Fonts/PixlPunktIcons.ttf` | Built by IcoMoon. Family name inside the font must be `PixlPunktIcons`. Copied to the output by the csproj (`Assets\Fonts\*.ttf`). |
| Codepoint enum | `PixlPunkt/UI/Icons/PixlPunktIconFont.cs` → `PixlPunktCodicon` | The C# names for each glyph. **The only place that maps a name to a codepoint.** |
| Glyph factory | same file → `PixlPunktIconFont.TryCreateGlyph(...)` | Makes a `TextBlock` (or a `Viewbox`-scaled one) in the icon font, with a Fluent fallback when the font file is missing. |
| Consumers | e.g. `PixlPunkt/UI/Voxel/Tools/VoxelToolRail.cs` | Ask for a glyph by `PixlPunktCodicon`; fall back to a Fluent `Icon` if the font isn't available. |

Current glyphs:

| Name | Codepoint | Source SVG |
|---|---|---|
| `CubeSubtract` | `U+E900` | `fluent_cube_subtract_20_regular.svg` |
| `CubeMove` | `U+E901` | `fluent_cube_move_20_regular.svg` |

Everything else in `rawSvg/` (stamp, stamp_add, stamp_sync, table_cell_edit, cube, cube_add,
cube_sync, full_screen_maximize, table, table_simple) is staged art that has **not** been added
to the font yet.

---

## Adding an icon, start to finish

### 1. Make or fetch the SVG

- Start from a Fluent icon when you can: grab the `*_20_regular.svg` from
  [microsoft/fluentui-system-icons](https://github.com/microsoft/fluentui-system-icons)
  (`assets/<Name>/SVG/`) and edit it, or draw a new one at the same 20×20 canvas so it sits on the
  same optical size as the rest of the UI.
- Single colour, filled paths only. Strokes, gradients and groups with transforms confuse the
  font builder; flatten them (Inkscape: *Path → Object to Path*, *Path → Combine*).
- Name it like the others: `fluent_<name>_20_regular.svg`.
- Drop it in `PixlPunkt/Assets/Fonts/rawSvg/`.

### 2. Rebuild the font in IcoMoon

1. Open <https://icomoon.io/app>, **Manage Projects → Import Project**, and load
   `PixlPunkt/Assets/Fonts/PixlPunktIcons.json`. (Importing the saved project is what keeps the
   existing codepoints stable. Don't start a fresh set.)
2. **Load** the project, then **Import Icons** and pick the new SVG(s). They land in the
   PixlPunktIcons set.
3. Select every glyph that belongs in the font (the existing ones plus the new ones). Unselected
   glyphs are left out of the build.
4. **Generate Font**. In the preferences (cog next to *Download*):
   - Font name: `PixlPunktIcons` (this is the family name the app looks up; if it changes,
     `PixlPunktIconFont.FontFamilyName` must change with it).
   - Codepoints: existing glyphs keep theirs (`E900`, `E901`, …). New glyphs take the next free
     codepoint. Write down what IcoMoon assigned; you need it in step 4.
   - Leave the class prefix and CSS options alone; the app doesn't use them.
5. **Download**, unzip, and copy `fonts/PixlPunktIcons.ttf` over
   `PixlPunkt/Assets/Fonts/PixlPunktIcons.ttf`.
6. Back in IcoMoon, **Manage Projects → Download** the project JSON and overwrite
   `PixlPunkt/Assets/Fonts/PixlPunktIcons.json`, so the next person can import the same project.

Commit the SVG, the `.ttf` and the `.json` together.

### 3. Sanity-check the font (optional, thirty seconds)

Double-click the `.ttf` in Explorer. The preview window shows the family name in the title bar
(must read `PixlPunktIcons`) and renders the glyphs. If a glyph is a box, the SVG wasn't a clean
filled path; go back to step 1.

### 4. Give it a name in code

In `PixlPunkt/UI/Icons/PixlPunktIconFont.cs`, add the codepoint IcoMoon assigned:

```csharp
public enum PixlPunktCodicon
{
    CubeSubtract = 0xE900,
    CubeMove = 0xE901,
    StampSync = 0xE902,   // new
}
```

Rules:
- Codepoints are **append-only**. Never renumber an existing value; other code, and the saved
  IcoMoon project, depend on them.
- Name it after what it depicts, in Fluent's PascalCase, not after where it's used.

### 5. Use it

**In a tool rail** (the pattern the voxel tools use): give the tool's `ToolVisual` a
`CustomGlyph` and keep the Fluent icon as the fallback for when the font file is missing.

```csharp
VoxelToolIds.VoxelMove => new ToolVisual("Move", Icon.ArrowMove, CustomGlyph: PixlPunktCodicon.CubeMove),
```

`VoxelToolRail.CreateGlyph` then does the right thing: custom glyph if the font is present,
Fluent icon otherwise. `CustomGlyphScale` nudges the optical size if the glyph reads smaller or
larger than its Fluent neighbours.

**Anywhere else in code:**

```csharp
if (PixlPunktIconFont.TryCreateGlyph(PixlPunktCodicon.StampSync, 16, out var glyph))
    button.Content = glyph;
else
    button.Content = new FluentIcon { Icon = Icon.Stamp, FontSize = 16 };
```

**Directly in XAML**, when you know the font is shipped (it always is in the installer) and
don't need the fallback:

```xml
<FontIcon FontFamily="ms-appx:///Assets/Fonts/PixlPunktIcons.ttf#PixlPunktIcons"
          Glyph="&#xE902;"
          FontSize="16"/>
```

Prefer the code path with the fallback for anything a user could hit with a broken install.

### 6. Documentation icons (separate pipeline)

The PNGs under `docs/assets/icons/` that the cheat sheet embeds are made by
`scripts/export-doc-icons.ps1`, which downloads Fluent SVGs and renders them with Inkscape. It
knows nothing about the custom font. If a custom glyph needs to appear in the docs, export it
from the SVG by hand at 16 and 20 px (white, transparent background) into that folder and add
it to `docs/assets/icons/ICON_MANIFEST.md`.

---

## Checklist

- [ ] SVG in `rawSvg/`, filled paths, 20×20, named `fluent_<name>_20_regular.svg`
- [ ] IcoMoon: imported the saved project, added the SVG, generated with font name `PixlPunktIcons`
- [ ] `PixlPunktIcons.ttf` and `PixlPunktIcons.json` replaced
- [ ] `PixlPunktCodicon` has the new value with the codepoint IcoMoon assigned
- [ ] Consumer passes a Fluent fallback
- [ ] SVG, TTF, JSON and code committed together

## When something's off

| Symptom | Cause |
|---|---|
| Fluent fallback shows instead of the glyph | `Assets/Fonts/PixlPunktIcons.ttf` isn't in the output folder (`IsAvailable` false). Check the csproj `Content Include="Assets\Fonts\*.ttf"` still covers it. |
| A box or blank where the glyph should be | Codepoint in the enum doesn't match the font, or the family name inside the font isn't `PixlPunktIcons`. |
| Glyph looks thin or off-centre next to Fluent icons | Source SVG wasn't on a 20×20 canvas. Fix the SVG, or use `CustomGlyphScale`. |
| Existing icons changed after a rebuild | The project wasn't imported from the JSON and IcoMoon reassigned codepoints. Re-import and regenerate. |
