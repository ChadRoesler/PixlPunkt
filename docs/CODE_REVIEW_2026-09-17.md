# PixlPunkt — Codebase Review (2026-09-17)

**Scope:** 152,470 lines of C# across 555 files (app + PluginSdk).

**Method:** pattern sweeps across the whole tree (constant conditions, swallowed exceptions, `async void`, sync-over-async, threading primitives, unsafe code, process/file/zip handling, parse-without-try, suppressed warnings, TODO markers); line-level reads of the core subsystems — document IO, autosave, history, selection, compositing, animation storage, export, plugin loading, updater, layer tree ops; a non-incremental build with a full warning census. Importers, history and selection were reviewed in depth in earlier passes; those findings are folded in and marked where already fixed.

Every finding below was verified against the code, not inferred from naming.

---

## What's in good shape

- **Compositor scratch buffers are `[ThreadStatic]`** — cross-thread corruption was suspected; it's already handled.
- **Layer drag-drop has a real cycle guard** (`CanMoveIntoFolder` walks ancestors) — a folder can't be dropped into itself or a descendant.
- **The updater never downloads or executes anything** — it opens the GitHub releases page.
- **Plugin loading is careful**: `ZipFile.ExtractToDirectory` (zip-slip safe on modern .NET), normalized-path containment check, isolated `AssemblyLoadContext` with unload.
- ffmpeg goes through FFMpegCore — no string-concatenated argument injection.
- Global `UnhandledException` / `UnobservedTaskException` handlers exist and log with session state for crash recovery.
- Sweep results: 4 TODOs in 152k lines, zero `lock(this)`, zero sync-over-async, zero `Thread.Sleep`, and all 14 empty `catch` blocks are teardown / temp-file cleanup.
- The `.pxp` format is versioned (21 versions) with proper magic and range gating on load.
- Autosave writes timestamped files to a *separate* directory with rotation — it never touches the user's real file.

---

## P0 — silent data loss

### 1. `DocumentIO.Save` is not atomic — ✅ fixed
`DocumentIO.cs:126` did `using var fs = File.Create(filePath);`, which truncates the existing file before a single byte is serialized. The serializer then walks 21 format versions of layers, effects, tile sets, animation tracks, audio, sub-routines and voxel state. Any exception anywhere in that path (a null effect property, a bad enum, disk full, a crash) left the user's `.pxp` as a truncated stub with the previous good version already gone. Six callers, none wrapped it.

**Fix applied:** `Save(doc, Stream)` now serializes fully into memory first and only then writes to the target stream, so a mid-serialize failure never touches the destination (this covers the file-picker callers too). `Save(doc, path)` additionally writes to `path + ".tmp"` and moves over the original.

### 2. Voxel edits never mark the document dirty — ✅ fixed
The voxel workspace has its own `VoxelCommandHistory`; nothing in `Core/Voxel` or `UI/Voxel` touched `doc.History`, `IsDirty` or `DocumentModified`. `CanvasDocument.IsDirty` was `History.IsDirty` alone, and the close-tab prompt (`PixlPunktMainWindow.Tabs.cs:260`) gates on it. Edit voxels for an hour, close the tab → no prompt, work gone. Voxel state *is* persisted (format versions 11–21 are all voxel).

**Fix applied:** `CanvasDocument` now tracks an unsaved-voxel-changes flag, set whenever the voxel edit engine's history changes and cleared by `MarkSaved()`. `IsDirty` ORs it in.

### 3. `IsDirty` was count-based — ✅ fixed (earlier pass)
Save → undo → new edit returned the stack to the saved depth and reported clean. Now identity-based.

---

## P1 — correctness and crashes

### 4. Autosave races the UI thread — ✅ fixed
`AutoSaveService.cs:123` uses `System.Threading.Timer` (a thread-pool thread); `OnTimerTick` → `SaveDocument` → `DocumentIO.Save(doc, filePath)` runs there with no dispatcher marshal. `Save` enumerates `doc.RootItems`, `Effects`, `Tracks`, `TileSet`, `AudioTracks` (plain `List<>`s the UI thread mutates) and reads layer pixel buffers mid-stroke. It also *writes* document state (`doc.VoxelWorkspace.CopyFromPreviewState(...)`) from the background thread.

**Outcomes:** `InvalidOperationException: Collection was modified` (autosave silently fails, logged) or a torn backup. The crash-recovery feature is least reliable exactly when the user is actively working.

**Fix applied:** `DocumentIO` now exposes `SaveToBytes` (reads live state) and `WriteFileAtomically` (pure disk write). `AutoSaveService` captures the UI `SynchronizationContext` in `Start()`, posts the serialize step to it and blocks the pool thread until the bytes are ready, then does the atomic write off the UI thread.

### 5. A throwing plugin effect crashes the app on every repaint — ✅ fixed
`Compositor.cs:185` called `fx.Apply(...)` (plugin code) with no try/catch. `CanvasDocument.CompositeTo` had none. The only catch in the render path (`CanvasViewHost.Rendering.cs:279`) wraps reference-layer drawing only. `App.UnhandledException` leaves `e.Handled = true` commented out, so the process exits. Because the effect is enabled on the layer and persisted, reopening the document crashed again — the user could not get in to disable it.

**Fix applied:** each effect is applied inside a try/catch in `CompositeLinear`. On failure the error is logged with the effect's name and the effect instance is disabled so the layer still renders and the UI reflects the state.

### 6. Timelapse export freezes the UI and can't be cancelled — ✅ fixed
`TimelapseExportService.cs:120–170`: one `await Task.Yield()` *before* the loop, then a synchronous `for` over every history step (`JumpTo` + `CompositeTo` + capture). Called with a plain `await` from the UI thread (`File.Export.cs:199`); it can't be `Task.Run`'d because it drives the live document's history. The progress dialog never repaints and Cancel is unclickable until the export finishes. The `finally` restoring `originalPosition` is correct and the modal dialog blocks canvas input, so replaying undo on the live document is *safe today*, just frozen.

**Fix applied:** the step loop now yields to the message loop (`await Task.Yield()`) whenever ~16 ms have elapsed since the last yield, re-checking the cancellation token after each yield. Progress repaints and Cancel works; the `finally` still restores the original history position. Snapshotting frames instead of driving the live document remains the better long-term design.

### 7. Gradient Fill ignores the selection; plugin selection masks are stubbed — ✅ fixed
`CanvasViewHost.GradientFill.cs:108` reads `_selectionEngine.HasActiveSelection` — but `SelectionEngine` is constructed once (`CanvasViewHost.xaml.cs:387`) and never driven, so the flag is permanently false and gradient fill floods the whole layer regardless of the marquee. Every other painting path uses `_selRegion` (`CanvasViewHost.Painting.cs:544`).

`PixlPunktMainWindow.xaml.cs:1408`: `isPointSelected: (x, y) => !(CurrentHost?.HasSelection ?? false) || true` — the `|| true` makes it unconditionally true; `getSelectionMask: (x, y) => 255`. Plugins always see "fully selected".

**Fix applied:** `CanvasViewHost` gained `HasPaintConstrainingSelection` and `IsPointSelected(x, y)`, using exactly the logic the stroke engine's mask uses (active, non-floating, non-empty region). Gradient fill and both plugin-context lambdas now go through them. The never-driven `_selectionEngine` field and its construction were removed; the `Core/Selection` files it referenced are now fully unreferenced (see #15).

### 8. Two "guard" fields are never assigned (CS0649), so their guards are dead — ✅ fixed
- `CanvasViewHost._isCommittingChanges` (`xaml.cs:332`) — `if (_isCommittingChanges) return;` at `:670` never fires
- `LayersPanel._suppressDocRefresh` (`xaml.cs:41`) — `if (_suppressDocRefresh) return;` at `:337` never fires

Whatever re-entrancy or refresh-suppression these were meant to provide isn't happening.

**Fix applied:** both fields and their guard lines removed, so the code now says what it does. *Correction to the original finding:* `_mainCanvasXaml`, `_horizontalRulerXaml` and `_verticalRulerXaml` are **not** dead — they are assigned inside the `#else` (WinAppSdk-only) branch at `xaml.cs:467`. The CS0649 is per-configuration and expected under Uno builds; left as-is.

### 9. Selection / history / importer items from earlier passes — ✅ fixed
- ✅ *fixed:* **Undo/redo hole-fix asymmetry** (`CanvasViewHost.History.cs`): undo filled the lift-hole whenever the selection was floating; redo only re-lifted if `PeekRedo() is SelectionTransformItem`. Now `SelectionChangeItem` carries `FloatingWasRestoredOnUndo`, set by the undo step that blits the pixels back and consumed by the matching redo, so the two are exact inverses regardless of what else is on the redo stack. `Undo()`/`Redo()`/`JumpHistoryTo` also share one `StepHistory` + `RefreshAfterHistory` pair now, which removed the duplicated tails that let them drift apart. (`RestoreFloatingPixelsToCanvas` still mutates the canvas outside history — modelling the lift as a history item remains the deeper fix.)
- ✅ *fixed:* **Pyxel tile animation** conflated the tileset grid with the canvas grid. `baseTile` is a linear tileset index; reels reference canvas cells, so each frame now maps index → imported tile id → a canvas cell that uses that tile (skipped with a warning if the tile isn't placed), and a reel is only created when it has at least one frame.
- ✅ *fixed:* **`UndoInternal`/`RedoInternal` lost the item entirely if `Undo()` threw** (`UnifiedHistoryStack.cs:192`) — now pushed back onto its original stack before rethrowing.
- ✅ *fixed:* starter layer never removed on import; Aseprite partial palette chunks wiped the palette; `isStructural` missed 6 of 10 tree-mutating item types.

### 10. `async void` without a try/catch — ✅ fixed
`SettingsWindow.xaml.cs:386` (`UpdateBrushPanelInfo`). An exception in an `async void` is a process crash. The other three non-handler `async void`s are guarded.

**Fix applied:** body moved to `UpdateBrushPanelInfoAsync()` returning `Task`; the `async void` wrapper awaits it inside try/catch and logs.

### 11. Untrusted-input parsing — ✅ fixed
- TMX/TSX importer uses `int.Parse` on 15 XML attributes (`ForeignDocumentImporter.cs:1260–1592`) — malformed file → `FormatException` instead of "invalid file".
- `DocumentIO.Load` trusts every file-supplied size (`pixelWidth/Height`, `dataLen`, item counts) — a truncated `.pxp` yields `OverflowException`/`ArgumentException`/OOM rather than a clean `InvalidDataException`; `ReadBytes(dataLen)` short-reads silently and isn't length-checked before `BlockCopy` (`:1314–1356`).

Robustness, not security — but "your file is corrupt" beats a stack trace.

**Fix applied:** all 15 TMX/TSX sites use a `ReadIntAttribute` helper (missing → default, non-numeric → `InvalidDataException` naming the attribute and element). `DocumentIO.Load` validates canvas and tile geometry against `CanvasConstants.MaxCanvasDimension` before allocating, checks each raster layer's byte count against its surface, and reads all file-sized buffers through `ReadExactBytes`, which turns a short read into a clear "file ends inside …" error.

---

## P2 — memory and performance

### 12. Animation pixel storage is never pruned, and the leak is saved to disk — ✅ fixed
`CanvasAnimationState.CleanupUnusedPixelData()` exists and has **zero callers**. `CanvasAnimationTrack.SetKeyframe` removes the old keyframe object but never releases its `PixelDataId`, so every keyframe overwrite orphans a full-canvas BGRA clone. `DocumentIO.cs:227` serializes the entire `PixelDataStorage` dictionary, so orphans bloat every save. A 1024² canvas leaks 4 MB per keyframe edit for the life of the document.

**Fix applied:** `CleanupUnusedPixelData()` is now called from `SetKeyframe` (replacement) and `RemoveKeyframe`, and from `WriteCanvasAnimationState` before the storage is serialized. No history item references a `PixelDataId`, so pruning is undo-safe.

### 13. From earlier passes — ✅ fixed
- ✅ **History memory manager tracked 2 of 20 item types.** New `OffloadableHistoryItemBase` implements `IDisposableHistoryItem` once; `CanvasResizeItem`, `SelectionCommitItem`, `SelectionTransformItem` and `TileStampHistoryItem` derive from it and are fully offloadable to disk, while `LayerRemoveItem`, `LayerMergeDownItem`, `FlattenFolderItem` and `TileMappedPixelChangeItem` (live layers / mixed structures) derive from it for accounting only. The 256 MB budget now sees 10 of the heavy item types instead of 2.
- ✅ **Aseprite import stored dense keyframes.** A keyframe is now emitted only where the resolved cel changes (linked cels and no-cel runs collapse), and every "no cel" transition shares one blank buffer.
- ✅ **`JumpHistoryTo` paid a full composite + invalidate per step.** It now steps through `StepHistory` (keeping the per-item selection bookkeeping) and refreshes the view once at the end.
- ✅ **Per-pixel `AddRect`.** `SelectionRegion.AddPixel` (O(1), no rect construction) serves the flood-fill tools; the lasso scanline and the transform-snapshot rebuild use one `AddRect` per horizontal run.

---

## P3 — structural and hygiene

### 14. Two undo systems — ✅ fixed
`UnifiedHistoryStack` for the canvas, `VoxelCommandHistory` for voxels, no bridge. Root cause of #2 (now patched with a dirty flag); also means voxel edits are invisible to timelapse export. A design decision rather than a patch.

### 15. Dead code — ✅ fixed
- ~2,200 lines in `Core/Selection` (`SelectionEngine`, `PixelSelection`, `SelectionMask`, `SelectionOutlineBuilder`, `SelectionTransform`, `SelectionState`) reachable only through the bogus gradient-fill check. `SelectionRegion` is the only live file in that folder. Three files carry a `using SelectionState = ...` alias to disambiguate a name collision with the nested enum that never needed to exist.
- Never-used fields (CS0169): `_cachedFrameIndexList`, `_havePreview`, `_previewRect`, `_toggling`, `_animationPreviewContainerBackup`, `_measuringGoodTop`. Assigned-never-read (CS0414): `_idleFrameCount`, `_didMove`.

**Fix applied:** the six `Core/Selection` files are deleted (`SelectionRegion` remains) and all eight fields are removed along with their dead assignments.

### 16. 95 distinct compiler warnings — ✅ fixed (95 → 10)
52 × CS0618 — SkiaSharp 3 obsolete APIs (`SKFilterQuality`, `SKPaint.TextSize`, `DrawText` overloads) that become errors on the next major. 21 × nullability (CS86xx), including three `IndexOf(null)` candidates at `CanvasDocument.cs:817/854/893`.

**Fix applied:** CS0618 → 0 (text drawing moved to `SKFont`; `SKFilterQuality` replaced — nearest-neighbour sites simply use Skia's default, and the renderer routes Linear/Cubic draws through `DrawImage` with `SKSamplingOptions` since this SkiaSharp exposes sampling only there). Nullability → 0 (real guards, plus `[NotNullWhen(true)]` on `TryCreateGlyph`). Remaining 10: 7 × CS0067 unused public events (left as API surface) and 3 × CS0649 for the WinAppSdk-only `*Xaml` fields (conditional compilation, expected).

### 17. Test coverage — ✅ unblocked
129 app tests across 9 areas + 230 SDK tests. Respectable for the SDK, thin for the app: no tests for importers, history, selection, compositing, painting, or non-voxel document IO. **Blocker:** `RasterLayer`'s constructor builds a `WriteableBitmap` (`RasterLayer.cs:74`), so `new CanvasDocument(...)` throws `NotSupportedException` in a test host — all of `Core` is untestable. Lazily creating the bitmap on first UI access is the single highest-leverage change on this list.

**Fix applied:** `RasterLayer` and `LayerMask` create their preview bitmap lazily on the first `Preview` read (and rebuild eagerly only once a UI has asked for it), so the document model constructs with no dispatcher. `PixlPunkt.Tests/ForeignDocumentImporterTests.cs` is the first beneficiary — four end-to-end tests over `PyxelImportTesting.pyxel` covering the layer tree, nesting, blend modes, opacity, tile references and clean history. App tests: 129 → 133.

### 18. Defense in depth
- `CanvasDocument.MoveLayerToFolder` has no cycle guard (the UI does) — matters if the SDK ever exposes it.
- `UpdateService.OpenReleaseUrl` passes a URL from the GitHub API JSON to `Process.Start(UseShellExecute: true)` with no scheme check; restrict to `http`/`https`.
- `AppSettings` save is `File.WriteAllText` (non-atomic; recovers to defaults, loses prefs only).
- `SelectionRegion` mixes coordinate spaces: `Contains`/`Bounds` are world-space (offset applied), `AddRect`/`SubtractRect` are local. `Clone()` drops `_boundsInvalid` (`CopyFrom` resets it) — a trap baked into undo snapshots.

### 19. Miscellaneous
- Two high-severity vulnerable transitive packages: `Microsoft.Kiota.Abstractions` 1.21.0 (GHSA-7j59-v9qr-6fq9), `Tmds.DBus.Protocol` 0.21.2 (GHSA-xrw6-gwf8-vvr9).
- `VoxelWorkspaceControl.xaml.cs` is 6,243 lines.
- Aseprite import: no frame-boundary resync (`frameBytes` read and discarded); blend modes 6/7/11 mapped to approximations and 9/12–15/18 fall to Normal silently; per-frame durations flattened to an average FPS.

---

## Consolidated backlog

| Pri | # | Item | Effort | Status |
|---|---|---|---|---|
| P0 | 1 | Atomic `DocumentIO.Save` | S | ✅ done |
| P0 | 2 | Voxel edits mark document dirty | S | ✅ done |
| P1 | 5 | Isolate plugin effect exceptions in compositor | S | ✅ done |
| P1 | 4 | Autosave: snapshot on UI thread, serialize off-thread | M | ✅ done |
| P1 | 7 | Gradient fill → `_selRegion`; real plugin selection mask | S | ✅ done |
| P1 | 12 | Call `CleanupUnusedPixelData` on keyframe remove/overwrite and before save | S | ✅ done |
| P1 | 6 | Timelapse: yield per N steps (or snapshot frames) | S–M | ✅ done |
| P1 | 8 | Resolve the dead-guard fields (assign or delete) | S | ✅ done (2 of 4; the other 2 are conditional compilation, not dead) |
| P1 | 9 | Selection hole-fix asymmetry; Pyxel anim grid; `Undo()` throw safety | M | ✅ done |
| P1 | 10–11 | `async void` guard; `TryParse` in TMX; size validation in `Load` | S | ✅ done |
| P2 | 13 | History memory accounting; Aseprite sparse keyframes; `JumpHistoryTo`; run-length `AddRect` | M | ✅ done |
| P3 | 17 | Decouple `WriteableBitmap` from `RasterLayer` ctor → unlocks Core tests | M | ✅ done (+4 importer tests) |
| P3 | 14 | Unify or bridge the two undo stacks | L | ✅ done — `VoxelCommandHistory` is an adapter over the document's `UnifiedHistoryStack` |
| P3 | 15–16 | Delete dead code; SkiaSharp 3 API migration; nullability pass | M | ✅ done (warnings 95 → 10) |
| P3 | 18–19 | URL scheme check; `MoveLayerToFolder` guard; bump vulnerable packages | S | ✅ done (also: atomic settings save, `SelectionRegion.Clone` bounds, Aseprite frame resync + blend warnings) |

**Suggested sequencing after the P0s:** #12 and #7 (the two a user hits in a normal session), then #17 — because everything after that gets a test.


---

## Addendum (same day) — selection & history redesign

See `SELECTION_AND_HISTORY_DEEP_DIVE.md`, Part E. The floating selection is now document state
bound to its layer; lift, transform, commit, cancel and delete are history items; selection no
longer raises `StructureChanged`; the history stack has groups and coalescing; the crossfade is
captured once per step. All five ⚠ data-loss paths in that document are closed structurally and
covered by Core tests (148 total, all passing).

## Addendum 2 (same day) — SMOOTHBRAIN_THOUGHTS + D3 + voxel merge

- **Close crash** (`COMException 0x800710DD` from `OnAnyLostFocus` → `MainXamlRoot`): a regression from
  the earlier focus-check change. `TryGetMainXamlRoot()` returns null once the window is closing
  or `Window.Content` throws; the focus handlers are unhooked in `OnWindowClosed`.
- **Tiles:** deleting a tile clears every mapping cell and voxel side-tile slot that referenced it
  (`CanvasDocument.OnTileRemoved`; the constructor now goes through `SetTileSet` so new documents get
  the hook, which they previously did not). *Tiles → Renumber Tiles* renumbers 1..N as one undo step
  (`TileRenumberItem`, `TileSet.BuildRenumberMap/ApplyIdMap`, `TileMapping.RemapTileIds`).
- **File → New from Clipboard:** a canvas the size of the clipboard image (one tile of that size),
  pasted at the origin. Ordinary pastes are now kept on-canvas when they fit, and a paste switches
  to the rectangle select tool when no select tool is active (that is what drags a float; there is
  no separate move tool).
- **Layers:** *Add Folder* nests inside the selected folder; new and duplicated layers/folders are
  selected and scrolled into view instead of the list snapping back to the top.
- **D3 (scoped)** and **#14 voxel merge:** see the deep-dive Part E.

Tests: 155 (was 148), all passing; warnings unchanged at the 10-item baseline.

## Addendum 3 (same day) — the rest of the list

- **Scale deferred to commit — reverted.** Leaving scale live in `ScaleX/Y` after release (one
  resample at commit) passed the Core tests and a handle-drag simulation, but in the app Chad saw
  scaling "doubled". `BakeTransformsOnRelease` is back to resampling the buffer on mouse-up exactly
  as before. The commit-side maths (`FloatingSelectionOps.Rasterize` at a live scale) is unchanged
  and still covered by `DeferredScaleTests`. If this is revisited, it goes with the selection-shape
  pass, verified in the running app, and the first thing to check is what pushes a percentage
  back into `OnOptionsChanged` between release and the next drag.
- **Handles are hit-tested at the live angle** (`Cumulative + AngleDeg`), matching where they
  are drawn; an angle typed into the options box no longer leaves the handles unclickable.
- **Marching ants repaint at 24 fps** instead of on every display frame while a selection is
  idle (`OnAntsRendering`); the phase is time-based so the speed is unchanged. A tool painting
  its marquee still gets every frame.
- **One source for Shift/Alt** on selection press: the host reads `e.KeyModifiers` like the
  tools do, instead of polling the keyboard separately.
- **Timelapse export skips voxel-only steps** (`VoxelHistoryItem`, or a group of them) now that
  voxel edits live on the document stack; they cannot change the canvas.
- **`VoxelWorkspaceControl.xaml.cs` split** along its own section markers into nine partial
  files (largest 1,927 lines, was 6,242). Pure moves; no member changed.
- Not done: per-frame durations on Aseprite import (the canvas animation model only has an
  FPS; a variable-timing model is a feature), and the full transform-matrix rewrite (the
  remaining asymmetry is that rotation lives in the mask only for rectangular selections).

Tests: 157, all passing; warnings at the 10-item baseline.

## Addendum 4 (same day) — SMOOTHBRAIN 2.0

- **New Canvas dialog has a "From Clipboard" button** (Ctrl+N, File menu and the tab "+" all go
  through one `ShowNewCanvasDialogAsync`). It is only offered when something has been copied and
  uses the dialog's name.
- **Shy "+" button:** the empty-strip nudge was queued and could land after a tab had been added
  (session restore). The queued callback now re-checks the tab count.
- **Layer previews:** right-click the panel → *Layer Previews* → Off / Small / Normal / Large.
  Persisted in `AppSettings.LayerPreviewSize`; rows compact when small or off.
- **Only the top edge while dragging:** the static outline drew only edges with an unselected
  pixel above. `SelectionRegion.DrawOutline` draws all four from the ants edge cache. (Reverted
  once with the shape work, re-applied on its own after scaling was confirmed fixed.)
- **Selection-as-shape** was implemented, then reverted at Chad's request: the model (mask rides with the
  pixels, one region feeds ants/hit-test/commit) is right, but it changes the feel of every transform and
  needs its own carefully verified pass. See the deep dive for the design.

Tests: 157, all passing; warnings at the 10-item baseline.

## Addendum 5 (2026-09-18) — selection as a shape, landed

Done as its own pass in six stages, each built, tested and hand-verified by Chad before the next:
carry the mask; region from the mask after bake/flip; undo/redo from the mask; click-inside reads
the region; live outline during scale/rotate drags; cleanup. Two things found and fixed on the
way: the rebuild clipped shapes to the canvas (now placed through the region offset, region
grow-only), and undo rebuilt from alpha. Layer previews moved to General settings; Auto Crop
added to per-layer image export. See the deep dive Part E for the design as landed.
