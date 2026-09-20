# Selection & History — Deep Dive (2026-09-17)

**Why this document exists.** Selection and undo have been the two persistent pain points in PixlPunkt. This is a full read of the live code — every file in `UI/CanvasHost/Selection/`, `CanvasViewHost.Selection.cs`, `CanvasViewHost.History.cs`, `Core/Selection/SelectionRegion.cs`, `Core/Tools/Selection/*`, `Core/History/*` and the document mutators they call — with the goal of explaining *why* they hurt, not just listing bugs. The bugs are here too (Part C), but the point is the shape.

Everything below was verified against the code as it stands after the fixes recorded in `CODE_REVIEW_2026-09-17.md`.

---

## Part A — Selection

### A1. One thing, five representations

The selection is stored in five places that must be kept in agreement by hand:

| # | Where | What it holds |
|---|---|---|
| 1 | `Core/Selection/SelectionRegion` | The mask: a byte buffer in local coords plus a world-space offset, with cached tight bounds |
| 2 | `UI/CanvasHost/Selection/SelectionSubsystem` | ~60 mutable properties: `Active`, `Floating`, `State`, `Rect`, `FloatX/Y`, `Buffer/W/H`, `ScaleX/Y`, `AngleDeg`, `CumulativeAngleDeg`, `OrigW/H`, `OrigCenterX/Y`, pivot ×4, `RotStart*` ×6, `ScaleStart*` ×6, `Preview*` ×9, `BufferFlipped`, `RegionNonRectangular`, … |
| 3 | `CanvasViewHost` "bridge properties" | 22 `_selXxx` shims over #2, commented *"for History.cs compatibility"* |
| 4 | `ToolState.Selection` | A second copy of presence / scale % / angle / scale mode, synced both ways with two re-entrancy flags (`_selPushToTool`, `_selApplyFromTool`) |
| 5 | `SelectionInputHandler` | A complete second copy of the pointer state machine — **dead** (no callers anywhere), and already diverged from the live copy: it pushes no history, and its `BakeTransformsOnRelease` resets `ScaleX/Y` to 1 without rescaling the buffer |

Until this session there was a sixth: the `Core/Selection` engine (`SelectionEngine`, `PixelSelection`, `SelectionMask`, …), constructed once and never driven — the reason Gradient Fill ignored the selection. #5 is the same disease; it just hasn't bitten yet.

**Why it hurts:** any invariant ("if Floating then Buffer != null", "Rect == Region.Bounds", "State == Armed iff Active") has to be re-established in every operation, in three or four places. The code does exactly that — `Active`, `Floating`, `State`, `Rect`, `OrigW/H`, `OrigCenterX/Y` are individually reassigned in `Selection_PointerReleased`, `Selection_DoubleTapped`, `SelectAll`, `InvertSelection`, `Paste`, `Delete`, `Cancel`, `ApplySelectionRegionFromHistory`, `ApplyTransformSnapshotAndRedraw`, `RestoreFloatingSelection`, `ClearFloatingSelection`, `LiftSelectionWithHistory`, `LiftSelectionSilently`, `CommitFloatingWithHistory`, `Clear()`… — and each site does it slightly differently. `Delete()` on a floating selection sets `Buffer = null, Floating = false, Active = false` but leaves `State = Armed`. `Cancel()` restores the buffer at `Rect.X/Y` where every other path uses `FloatX/FloatY`.

### A2. The state machine is three booleans pretending to be an enum

`SelectionState` has two values (`None`, `Armed`). Whether the selection is *floating* is a separate bool. Whether it *exists* is a third (`Active`). The real machine has three states:

```
None ──marquee──▶ Marquee(region) ──lift──▶ Floating(region, buffer, transform)
  ▲                   │  ▲                          │
  └───────clear───────┘  └─────────commit───────────┘
```

Because the three flags are independent, the code has to guard against impossible combinations everywhere (`if (_state.Floating && _state.Buffer != null)`, `if (!_state.Floating && !_state.Active) return false`), and several transitions leave one flag behind.

`Rect` is a *cached copy* of `Region.Bounds` that has to be refreshed manually after every mutation — it's a derived value stored as state.

### A3. Lift is not a history item — the root of the "hole"

This is the single most important finding. Trace the floating lifecycle:

1. **`LiftSelectionWithHistory`** clears the selected pixels on the layer and builds `_selPendingItem` (a `PixelChangeItem` for that clear) — **but does not push it**. It stashes `_liftBoundsForHistory`, `_liftPixelsBeforeForHistory` and, for tile-mapped layers, a clone of **every tile in the tileset** (`_liftTileBeforeStates`).
2. **`CommitFloatingWithHistory`** pushes a `SelectionCommitItem` whose *before* snapshot is the **already-cleared** canvas, then throws the pending lift item away (`ClearSelectionHistoryState`).

So the timeline for "marquee → drag → commit" is `[SelectionChangeItem] [SelectionTransformItem…] [SelectionCommitItem]`. **The clear is recorded nowhere.** Undoing the commit restores the cleared canvas plus the floating snapshot — visually fine. Undoing the marquee item then discards the floating state, and the canvas is left with the hole. That is the bug the previous commit ("Resolving hole with selection undo redo…") was chasing, and the reason `RestoreFloatingPixelsToCanvas` / `LiftSelectionSilently` / `FloatingWasRestoredOnUndo` exist: they mutate the canvas *outside* history to compensate for a step history never saw.

Every other exit from Floating drops the pending clear too:

- **`Delete` on a floating selection** — "Just discard the floating buffer". No push. **Ctrl+Z cannot bring the pixels back.** Reproduce: marquee → drag (lifts) → Delete → Ctrl+Z. The marquee item undoes (region comes back), the pixels stay gone.
- **`Cancel` (Escape)** — blits the buffer back at `Rect.X/Y` (not `FloatX/FloatY`), ignores rotation entirely, uses the *scaled* buffer if a scale was baked, and pushes nothing. After a rotate, Escape restores wrong pixels in the wrong place with no undo.
- **`ApplySelectionRegionFromHistory`** (any `SelectionChangeItem` undo/redo) sets `Floating = false, Buffer = null` — the hole case above.
- **Layer switch** — `ActiveLayerChanged` only resets stroke state. Every lift/commit path reads `Document.ActiveLayer` *at call time*, so a buffer lifted from layer A is committed onto layer B; `SelectionCommitItem` records B; A keeps the hole permanently.
- **Save while floating** — nothing commits before `DocumentIO.Save` (neither the explicit save nor autosave). The layer *has* the hole (the lift mutated it), the buffer exists only in view memory → **the saved file is missing the selected pixels.** The marquee item made the document dirty, so the save goes through without protest.
- **Canvas resize / animation frame change while floating** — neither reconciles the buffer. `AutoCaptureKeyframeIfNeeded` runs on commit but the clear on the *previous* frame was never captured.
- **Paste** sets `PendingCs = new PixelChangeItem(rl, "Paste")` — a property with two writers and zero readers.

Tile-mapped layers have a second copy of the problem: the commit item restores layer pixels but never restores tile definitions or re-propagates to other instances (only the "landed nowhere" branch uses `_liftTileBeforeStates`), so **undoing a commit on a tile-mapped layer desyncs the tileset**.

### A4. The transform model: two frames, sentinel zeros, asymmetric baking

- The pivot/center is computed as `OrigCenterX != 0 ? OrigCenterX : (FloatX + W/2)` in nine places (renderer, hit-test, transform ops, bake, commit). Zero is a valid coordinate — a selection centred on x=0 silently takes the fallback branch. Same pattern for `OrigW > 0 ? OrigW : BufferWidth`.
- **Scale is baked into the buffer on release; rotation is not** (it accumulates in `CumulativeAngleDeg` until commit). After scale → rotate → scale, the second scale rescales an unrotated buffer while the preview shows rotation. The region rebuilt by `RebuildSelectionRegionAsRotatedRect` and the preview produced by `RotateSpriteApprox` disagree at the edges, so `IsInsideTransformedSelection` (preview alpha) and `Region.Contains` (mask) disagree about what's selected.
- `BakeTransformsOnRelease` rewrites `OrigW/H` to the *scaled* size, so "Orig" stops meaning original after the first bake; `ScaleStartW/H` is a third set of "start" values on top.
- Hit-testing rotates handles by `CumulativeAngleDeg`; rendering rotates by `CumulativeAngleDeg + AngleDeg`. During a rotate drag the handles draw at the live angle but hit-test at the baked one.
- `RegionNonRectangular` — whether the marquee is a plain rectangle — is re-derived by an O(bounds) `Contains` scan on **every lift** (twice: `LiftSelectionWithHistory` and `LiftSelectionSilently`) and then selects between two different marquee-rebuild strategies. The shape's identity is a fact about how it was created, not something to rediscover from pixels.

### A5. Rendering and invalidation

- `CompositionTarget.Rendering += OnAntsRendering` → `InvalidateMainCanvas()` **every frame** while any selection exists. Marching ants cost a full canvas repaint at display rate, for as long as a marquee is on screen. `Region.DrawAnts` then does four O(bounds) passes per frame (`At()` per pixel); on a select-all of a 4096² canvas that's 64M reads per frame.
- `DrawAntsFromBuffer` (used for transformed previews) recomputes the full edge list from the preview buffer every frame.
- `_state.Dirty` is assigned in **19 places and read in none**. `_state.Changed`: 13 writes, 0 reads. Both are maintained on every drag tick.

### A6. Selection moves are broadcast as *structure* changes

Lift, commit, nudge, transform-snapshot restore, silent re-lift and clear all call `Document.RaiseStructureChanged()`. The listeners:

- `CanvasViewHost.OnDocChanged` → `EnsureSize` + `UpdateActiveLayerPreview` + `UpdateViewport` + invalidate
- `LayersPanel.OnDocStructureChanged` → `ActiveLayer.UpdatePreview()` → **allocates a new `WriteableBitmap` and copies the full layer**
- `AnimationPanel.OnDocumentStructureChanged` → `SyncCanvasAnimationTracks()`

So **every arrow-key nudge rebuilds the layer thumbnail bitmap and resyncs the animation timeline**. Most of the same paths also call `UpdateActiveLayerPreview()` directly, so the thumbnail is regenerated twice per nudge. `StructureChanged` means "the layer tree changed"; the selection is borrowing it as a generic "something happened" bus.

### A7. Marquee capture and the tools

- The host captures `_selectionBeforeMarquee = _selRegion.Clone()` (a full-document mask clone) and the combine kind from `InputKeyboardSource` on every pointer press; the tool then decides its own `CombineMode` from `e.KeyModifiers`. Two sources of truth for the same modifier state.
- `SelectionChangeItem` stores two full-document masks (`before`, `after`) — 32 MB per item on a 4096² canvas — and is **not** memory-tracked (it doesn't derive from `OffloadableHistoryItemBase`; only the pixel-payload items were converted). Twenty marquees on a large canvas is 640 MB the budget can't see. Masks RLE-compress extremely well.
- `HasChanges` compares the two regions by an O(bounds) double `Contains` scan on every push attempt — including every Lasso vertex click, since each click is a press/release pair that reaches `PushMarqueeSelectionHistory`.
- `SelectionClipboard.ApplySelectionRegion` (the undo callback for Select All / Invert) rebuilds the region with `AddRect(CreateRect(x, y, 1, 1))` **per pixel over the entire document** — W×H calls — while the marquee undo callback next door (`ApplySelectionRegionFromHistory`) uses `CopyFrom`. Two undo callbacks for the same item type, one of them ~10⁶× slower.

### A8. Dead and duplicated code (verified)

**Dead:**
- `SelectionInputHandler.cs` — 614 lines, no callers.
- `SelectionSubsystem` properties with **zero reads**: `Dirty` (19 writes), `Changed` (13), `PendingCs` (2), `LiftRect` (1), `PivotSnappedTo` (2); with **zero reads and zero writes**: `HadSelBefore`, `BeforeRect`, `CombineMode`, `DragStartRect`, `PreviewRect`, `LiftedFromDoc`, `SourceRect`.

**Duplicated (same logic, separate copies that can drift):**
- `CopyRectBytes` / `BlitBytes` / `ClearRectBytes` — in `CanvasViewHost.Selection.cs` and `SelectionClipboard.cs`
- `UpdateRotation` / `BakeTransformsOnRelease` / `OffsetSelectionRegion` / `UpdateSelectionCursor` / `CursorForHandle` — host and `SelectionInputHandler`
- `GetPivotPositionDoc` / `GetPivotPositionView` — `SelectionTransformOps` and `SelectionHitTesting`
- `BuildScaledBuffer` / `BuildRotatedBuffer` — host (`…ForCommit`) and `SelectionRenderer`
- `DrawDashedLine` — `RectSelectionTool` and `LassoSelectionTool`
- Region-from-buffer-alpha rebuild — `RebuildSelectionRegionFromTransformedBuffer` (History.cs) and inline in `ApplyTransformSnapshotAndRedraw` (Selection.cs)
- Handle geometry (`handleW/H`, `selX/Y`, `cx/cy`, `rad`) — five copies: `HitTestHandle`, `HitTestRotateHandle`, `DrawTransformHandles`, `DrawRotatedRectangleAnts`, `DrawDebugHitAreas`

---

## Part B — History

### B1. What's sound

`UnifiedHistoryStack` is small and, after this session, identity-safe (`IsDirty`) and exception-safe (a throwing `Undo` no longer drops the item). Items are self-contained: each holds references to the objects it mutates and restores by reference, not by index, wherever that matters (`LayerAddItem`, `LayerTreeAddItem`, `LayerMoveToFolderItem`, `PixelChangeItem`). The index-carrying items (`LayerReorderItem`, `FolderReorderItem`) store post-removal-adjusted indices and the mutators they call do remove-then-insert, so they round-trip correctly. The offload machinery works and now covers ten of the heavy item types.

### B2. Where it hurts

1. **The model can't represent a lifted selection** (A3). Everything the UI does to paper over that — `RestoreFloatingPixelsToCanvas`, `LiftSelectionSilently`, `FloatingWasRestoredOnUndo`, `IsStructuralItem`, the per-type branching in `StepHistory` — is the history stack being taught about selection internals because selection never told it the truth. Any consumer that walks the core stack directly gets the raw sequence: **timelapse export renders the hole** at every "after marquee, before commit" step, because at that point the document genuinely has a hole.

2. **No transactions or coalescing.** Twenty arrow-key nudges are twenty `SelectionTransformItem`s and twenty "Undo Move" presses. A scale drag is a lift + a transform; a paste-move-commit is a transform + a commit. `VoxelCommandHistory` already has `BeginTransaction/Commit/Cancel`; the unified stack doesn't, which is also why the two can't be merged today.

3. **Crossfade capture per item.** Nearly every layer-tree item calls `RaiseBeforeStructureChanged()` in `Undo`/`Redo`, and the host's handler (`CaptureForCrossfade`) does a **full composite plus a full-surface copy** each time. The document mutators (`AddLayerWithoutHistory`, `RemoveLayerWithoutHistory`, `MoveLayerByReferenceWithoutHistory`) each `CompositeTo(Surface)` again internally. A timeline scrub across 50 layer operations is ~100 full composites for a crossfade nobody sees mid-scrub. My `JumpHistoryTo` batching fixed the *after* refresh, not this.

4. **`SelectionChangeItem` is untracked and uncompressed** (A7).

5. **Lift clones the whole tileset.** `LiftSelectionWithHistory` on a tile-mapped layer iterates `TileSet.TileIds` and clones every tile, regardless of how many the selection touches. `TileAwarePixelChangeItem.CaptureAffectedTileStates` already computes only the affected ones; the lift path doesn't use it. And as noted, the commit item on a tile-mapped layer doesn't restore tiles at all.

6. **`RecalculateMemory` on every `HistoryChanged`** walks every tracked item (and `PayloadBytes` on the new base items sums their buffers), so it's O(items) per push/undo/redo. Fine at hundreds; will show up at thousands.

---

## Part C — Concrete defects found in this pass

Ordered by severity. Items marked ⚠ are data loss. ✅ = fixed (see Part E).

| | Defect | Where |
|---|---|---|
| ⚠ ✅ | Save (and autosave) while a selection is floating writes a document with the selected pixels missing | no commit before `DocumentIO.Save`; lift mutates the layer, buffer is view-only |
| ⚠ ✅ | Delete on a floating selection discards the lift's pixel clear without a history item — Ctrl+Z cannot restore | `SelectionClipboard.Delete` |
| ⚠ ✅ | Switching the active layer while floating commits the buffer onto the *new* layer; the old layer keeps the hole | every lift/commit path reads `Document.ActiveLayer` at call time; `ActiveLayerChanged` doesn't reconcile |
| ⚠ ✅ | Cancel (Escape) restores at `Rect.X/Y` instead of `FloatX/FloatY`, ignores rotation, uses the scaled buffer, pushes nothing | `SelectionClipboard.Cancel` |
| ⚠ ✅ | Undoing a commit on a tile-mapped layer restores layer pixels but not tile definitions/instances | `SelectionCommitItem.Undo` → `ApplyPixelsToLayer`; tile before-states only used on the "landed nowhere" branch |
| ✅ | Timelapse export renders holes at every lifted-but-uncommitted step | consequence of lift not being an item |
| ✅ | Scale→rotate→scale: second scale acts on the unrotated buffer; mask and preview disagree | fixed: scale is never baked before commit |
| ✅ | Handles hit-tested at baked angle, drawn at live angle | fixed: both use `Cumulative + AngleDeg` |
| ✅ | `OrigCenter == 0` sentinel misfires for selections centred at x=0 or y=0 | fixed: sentinels removed |
| ✅ | Every nudge rebuilds the layer thumbnail (twice) and resyncs animation tracks | `RaiseStructureChanged` from selection paths (A6) |
| ✅ | Full-canvas invalidate every frame while any selection exists | fixed: 24 fps ants cadence |
| ✅ | Select All / Invert undo rebuilds the mask per pixel over the whole document | `SelectionClipboard.ApplySelectionRegion` |
| ✅ | `SelectionChangeItem` holds two full masks, untracked by the memory budget | A7 |
| ✅ | Lift clones the entire tileset | A3 / B2.5 |
| ✅ | Two sources of truth for combine mode (host key state vs tool `KeyModifiers`) | fixed: both read the pointer event |
| ✅ | 614 lines of dead `SelectionInputHandler`; 12 dead/write-only state properties; ~9 duplicated helper families | A8 |

---

## Part D — What "not painful" would look like

> **Status (same day):** D6, D1, D2, D4 and D5 are implemented; see *Part E* at the end. D3 (the
> matrix transform) is the one piece left, deliberately, because it changes rendering/hit-test
> math that only the running app can verify.


The fixes above are patches. The reason both subsystems keep generating new edge cases is structural, and the structure can be changed incrementally. In order of leverage:

### D1. Make the floating selection document state, not view state
Introduce `CanvasDocument.FloatingSelection` — a small object holding: the **source layer by reference**, the lifted surface, a mask, and one **affine transform** (translate, scale, rotate, flip, pivot). Everything that today lives in ~40 `SelectionSubsystem` fields becomes: `Layer`, `Surface`, `Mask`, `Transform`.

What this buys immediately:
- Save can rasterize or refuse. Autosave sees it. The plugin API sees the same selection the user does.
- Layer switch can commit-or-refuse instead of silently retargeting.
- Canvas resize can transform it like any layer.
- Timelapse and the history panel walk a document that is actually in the state the user saw.

### D2. Make lift / transform / commit / discard history items
Four items on the unified stack:
- `LiftSelectionItem` — pixel clear (before/after for the bounds) + creates `FloatingSelection`
- `TransformSelectionItem` — transform before/after (a matrix, not 16 fields; **coalesces** with the previous transform item in the same floating session)
- `CommitSelectionItem` — rasterize; pixel before/after for the destination
- `DiscardSelectionItem` — cancel/delete; restores the lift's pixels

Undo/redo becomes pure LIFO. `RestoreFloatingPixelsToCanvas`, `LiftSelectionSilently`, `FloatingWasRestoredOnUndo`, the per-type branching in `StepHistory`, and `IsStructuralItem`'s selection cases all disappear. The tile-mapped case stops being special: the lift item captures *affected* tiles once, and commit/discard restore them.

### D3. One state, one transform, one geometry helper
- Replace `Active`/`Floating`/`State`/`Rect` with a single `SelectionPhase { None, Marquee, Floating }` and derived properties.
- Replace `OrigW/H`, `OrigCenterX/Y`, `ScaleStart*`, `RotStart*`, `CumulativeAngleDeg`/`AngleDeg`, `Preview*` with the transform matrix plus a "drag start" copy of it. Bake = compose. Rendering, hit-testing and commit all read the same matrix. The `!= 0` sentinels go away because there is nothing to fall back to.
- One `TransformFrame` helper computes handle/pivot geometry for renderer, hit-test and debug overlay.
- Rectangular vs free-form is a property set at creation, not rediscovered by scanning.

### D4. Stop borrowing `StructureChanged`
A `SelectionChanged` event on the document. Layers and animation panels stop reacting to nudges. The ants become an overlay drawn from a cached edge list (invalidated on region change, not per frame), with only the overlay layer repainting at animation rate.

### D5. History: transactions and a quieter crossfade
- `UnifiedHistoryStack.BeginGroup(name)` / `EndGroup()` with a merge policy per item type — this is also the prerequisite for folding `VoxelCommandHistory` in (it already has transactions), which gives one Ctrl+Z, one dirty flag and one timeline.
- Items stop calling `RaiseBeforeStructureChanged`; the host raises it once per *user gesture*. The mutators stop compositing internally; the caller composites once.
- `SelectionChangeItem` derives from `OffloadableHistoryItemBase` and stores masks RLE-compressed.

### D6. Delete the dead weight
`SelectionInputHandler.cs`, the twelve dead properties, the duplicated helpers (A8). This is a mechanical step and shrinks the surface a redesign has to touch by roughly a third.

### Sequencing
D6 → D1 → D2 → D3 are dependent in that order and each leaves the app working. D4 and D5 are independent of them and can go in any time. D1+D2 together remove the entire class of "hole" bugs and all five ⚠ rows in Part C; D3 removes the transform-consistency rows.

---

## Part E — What was implemented

**D6 — dead weight.** `SelectionInputHandler.cs` deleted; the twelve write-only/unused
`SelectionSubsystem` properties removed with all their writers; the pivot geometry, byte-rectangle
helpers (`PixelRectOps`), scale/rotate buffer builders (`SelectionBufferOps`) and dashed-line
drawing (`SelectionToolDrawing`) each have one home.

**D1 — the floating selection is document state.** `CanvasDocument.Selection` (the mask) and
`CanvasDocument.Floating` (a `FloatingSelection`: layer-bound pixels + transform + where they
came from) replace the view-owned copies. `SelectionSubsystem` forwards its floating/transform
properties to the document object while lifted and falls back to local values for the armed
marquee, so the ~300 call sites in the renderer, hit-testing and transform ops were untouched.
The host observes `CanvasDocument.SelectionChanged`. Consequences that fell out for free:
commit goes to the *source* layer regardless of the active layer; a save while floating writes
what the user sees (rasterized onto a copy in `DocumentIO`, never committing); a canvas resize
and a timeline frame change commit first (the latter onto the frame being left).

**D2 — lift / transform / commit / discard are history items.** `FloatingSelectionOps`
(Core) performs each operation and returns its item: `SelectionLiftItem`, `SelectionTransformItem`
(now applies to the document, no host callback), `SelectionCommitItem`, `SelectionDiscardItem`
(Escape puts the pixels back; Delete leaves the hole; both undoable). Each captures the affected
tiles of a tile-mapped layer, so undo restores tile definitions and instances too. Undo/redo is
pure LIFO: `RestoreFloatingPixelsToCanvas`, `LiftSelectionSilently`, `FloatingWasRestoredOnUndo`
and the per-type branching in `StepHistory` are gone. `SelectionChangeItem` stores RLE masks and
takes part in the memory budget. Nudges and flips are recorded (they were not).

**D4 — selection stopped borrowing `StructureChanged`.** Selection paths raise
`SelectionChanged`; the layers and animation panels no longer rebuild on a nudge. Marching ants
draw from an edge-run cache keyed on `SelectionRegion.Version` instead of re-scanning the bounds
four times per frame.

**D5 — history transactions and a quieter crossfade.** `UnifiedHistoryStack.BeginGroup /
EndGroup / CancelGroup` (nesting-safe) make multi-item gestures one step: Delete Selection,
Paste (commit + paste), Select All / Invert while floating, Resize Canvas (commit + resize).
`ICoalescingHistoryItem` lets a run of `SelectionTransformItem`s of one kind collapse into one
step (twenty arrow-key nudges = one Undo). History items and the `WithoutHistory` document
mutators no longer call `RaiseBeforeStructureChanged` or composite internally; the view captures
the crossfade once per step (once per timeline jump) and composites once when the step is done.

**Tests.** `SelectionHistoryTests` (9) and `HistoryGroupingTests` (6) drive all of the above
through the document with no view: lift→move→commit round-trips through undo with no hole,
cancel/delete are undoable, layer binding holds across an active-layer change, saving while
floating writes the moved pixels without mutating the document, the mask codec round-trips,
groups undo as one, nudges coalesce, coalescing stops across kinds.

**D3 (scoped) — no more sentinels.** A `FloatingSelection` sets `OrigW/H` and `OrigCenterX/Y`
in its constructor, every move shifts the centre with the float, and a bake re-centres them, so
the forty-one `Orig* != 0 ? … : derive-from-X/buffer` fallbacks across the renderer, hit-testing,
transform ops and Core commit were dead code with one live bug (a float whose centre sits on
x=0 or y=0 took the fallback path). They now read the properties directly; the invariant is
documented on the class. The full matrix rewrite (scale baked / rotation unbaked asymmetry) was
not done: it changes pixel results and needs the running app to verify.

**Voxel history merged.** `VoxelCommandHistory` is now an adapter over a `UnifiedHistoryStack`
(`VoxelHistoryItem` wraps each command; transactions are groups) and the workspace passes the
document's stack, so canvas and voxel edits share one timeline, one dirty flag and one memory
budget. The workspace only steps items it pushed; a canvas item on top is left for the canvas
host, which knows how to refresh after it. `CanvasDocument.MarkVoxelModified` and its flag are
gone. Covered by `VoxelHistorySharingTests` (3).

**Scale deferred to commit (D3, second slice).** `BakeTransformsOnRelease` no longer resamples
the buffer; scale and rotation are both live until `FloatingSelectionOps.Rasterize` applies them
once. The scale/rotate transform items carry no buffer copy any more. Handles are hit-tested at
the live angle. What remains of D3 is only the representation: rotation is baked into the mask
for rectangular selections and not for free-form ones.

**Selection as a shape (D3, third slice) — landed, in six verified stages (2026-09-18).**
`FloatingSelection.Mask` is the marquee in the float's own pixel frame (all ones for a paste).
It follows the pixels: a scale bake resamples it, flips mirror it, rotation stays live for both,
undo snapshots and history payloads carry it. `SelectionRegionBuilders.RebuildFromMask` places
the transformed mask (nearest-neighbour scale, the pixels' own rotation method so the outline
previews what RotSprite/nearest will produce) through the region's world offset, so an
off-canvas shape stays whole; `SelectionRegion.EnsureSize` is grow-only so a float scaled past
the canvas fits. The region is rebuilt on bake, flip, undo/redo, options-box edits and every
scale/rotate pointer move, and shifted on move. Ants, the paused outline, the click-inside test
(`IsInsideTransformedSelection` is `Region.Contains`) and the committed mask all read that one
region; nothing derives from pixel alpha any more. One deliberate exception: a rectangular
selection keeps its analytic rotated polygon while a rotate drag is in progress. Deleted:
`RebuildAsRotatedRect`, `RebuildFromTransformedBuffer`, the alpha `RebuildFromFloating`, and the
renderer's buffer-traced ants (`DrawAntsFromBuffer`, `DrawTransformedMarchingAnts`,
`DrawRotatedRectangleAnts`, the edge caches). Bake-on-release is unchanged. Tests:
`SelectionMaskTests`, `SelectionMaskRegionTests`, `SelectionTwoScaleTests`.
