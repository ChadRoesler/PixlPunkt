using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.History;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;
using SelectionState = PixlPunkt.UI.CanvasHost.Selection.SelectionSubsystem.SelectionState;

namespace PixlPunkt.UI.CanvasHost
{
    /// <summary>
    /// History, selection lift/commit, and undo/redo orchestration for CanvasViewHost.
    /// Uses the unified history stack on the document for all operations.
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ────────────────────────────────────────────────────────────────────
        // HISTORY STATE
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when the history state changes (after undo, redo, or commit operations).
        /// </summary>
        public event Action? HistoryStateChanged;

        /// <summary>Gets whether an undo operation is available.</summary>
        public bool CanUndo => Document.History.CanUndo;

        /// <summary>Gets whether a redo operation is available.</summary>
        public bool CanRedo => Document.History.CanRedo;

        /// <summary>Gets the description of the next undo operation.</summary>
        public string? UndoDescription => Document.History.UndoDescription;

        /// <summary>Gets the description of the next redo operation.</summary>
        public string? RedoDescription => Document.History.RedoDescription;

        // ────────────────────────────────────────────────────────────────────
        // HISTORY HELPERS
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Pushes a pixel change to the unified history stack.
        /// </summary>
        private void PushPixelChange(RasterLayer layer, RectInt32 rect, byte[] before, byte[] after, string description = "Pixel Change")
        {
            var item = PixelChangeItem.FromRegion(layer, rect, before, after, description);
            Document.History.Push(item);
        }

        /// <summary>
        /// Pushes a multi-region pixel change to the unified history stack.
        /// </summary>
        private void PushMultiRegionChange(RasterLayer layer, (RectInt32 rect, byte[] before, byte[] after)[] regions, string description = "Pixel Change")
        {
            var item = PixelChangeItem.FromMultiRegion(layer, regions, description);
            Document.History.Push(item);
        }

        // ────────────────────────────────────────────────────────────────────
        // SELECTION: LIFT / COMMIT
        // ────────────────────────────────────────────────────────────────────
        // The floating selection is document state (CanvasDocument.Floating) and lift, commit,
        // cancel and delete are all history items produced by FloatingSelectionOps. The view
        // only asks for the operation, pushes the item, and refreshes. Undo/redo of these items
        // needs no special handling here: the document raises SelectionChanged and the view syncs.

        private void LiftSelectionWithHistory()
        {
            if (Document.Floating != null) return;
            if (Document.ActiveLayer is not RasterLayer rl) return;

            var item = Core.Selection.FloatingSelectionOps.Lift(Document, rl);
            if (item == null) return;

            Document.History.Push(item);
            HistoryStateChanged?.Invoke();
            UpdateActiveLayerPreview();
            InvalidateMainCanvas();
        }

        private void CommitFloatingWithHistory() => CommitFloatingWithHistory(null);

        /// <param name="keyframeIndex">Frame to capture the keyframe on; null = the current frame.</param>
        private void CommitFloatingWithHistory(int? keyframeIndex)
        {
            var floating = Document.Floating;
            if (floating == null) return;
            var layer = floating.Layer;

            var item = Core.Selection.FloatingSelectionOps.Commit(Document);
            if (item == null) return;

            Document.History.Push(item);
            HistoryStateChanged?.Invoke();
            UpdateActiveLayerPreview();
            InvalidateMainCanvas();
            SetCursor(InputSystemCursorShape.Arrow);
            RaiseFrame();
            Document.RaiseDocumentModified();
            AutoCaptureKeyframeIfNeeded(layer, keyframeIndex);
        }

        // ────────────────────────────────────────────────────────────────────
        // REGION MUTATION + HISTORY
        // ────────────────────────────────────────────────────────────────────

        private void ApplyWithHistory(RectInt32 rect, Action<byte[]> mutator, string description = "Edit")
        {
            if (Document.ActiveLayer is not RasterLayer rl) return;
            var surf = rl.Surface;
            var r = ClampToSurface(Normalize(rect), surf.Width, surf.Height);
            if (r.Width == 0 || r.Height == 0) return;

            var before = CopyRectBytes(surf.Pixels, surf.Width, surf.Height, r);
            mutator(surf.Pixels);
            var after = CopyRectBytes(surf.Pixels, surf.Width, surf.Height, r);

            // Check if this affects mapped tiles - if so, use TileAwarePixelChangeItem
            if (rl.TileMapping != null && Document.TileSet != null)
            {
                var item = new TileAwarePixelChangeItem(rl, Document.TileSet, r, before, after, description);
                Document.History.Push(item);
            }
            else
            {
                PushPixelChange(rl, r, before, after, description);
            }

            // Recomposite after pixel changes
            Document.CompositeTo(Document.Surface);
            UpdateActiveLayerPreview();
            HistoryStateChanged?.Invoke();
            Document.RaiseDocumentModified(); // Notify animation panels of pixel changes
            InvalidateMainCanvas();

            // Capture keyframe if in canvas animation mode and layer has animation
            AutoCaptureKeyframeIfNeeded();
        }

        // ────────────────────────────────────────────────────────────────────
        // UNDO / REDO
        // ────────────────────────────────────────────────────────────────────

        private static bool GroupIsStructural(HistoryGroupItem group)
        {
            foreach (var child in group.Items)
                if (IsStructuralItem(child)) return true;
            return false;
        }

        /// <summary>
        /// Returns true when undoing or redoing <paramref name="item"/> can change the canvas
        /// dimensions or the shape of the layer tree, and the host therefore has to re-sync
        /// zoom, selection region size, composite and stroke state.
        /// </summary>
        /// <remarks>
        /// Every item that adds, removes, reorders, merges, flattens or reparents a layer or
        /// folder belongs here. Note that CanvasDocument.AddLayer pushes a
        /// <see cref="LayerTreeAddItem"/> when the active layer sits inside a folder and a
        /// <see cref="LayerAddItem"/> otherwise, so both have to be listed or the same user
        /// action re-syncs only for layers at root level.
        /// Reference layers are deliberately excluded: they live outside the layer tree and a
        /// plain recomposite plus invalidate is enough for them.
        /// An item may also say so itself by implementing <see cref="IStructuralHistoryItem"/>,
        /// which is how anything defined in Core opts in without this list having to know about it.
        /// </remarks>
        private static bool IsStructuralItem(IHistoryItem? item) =>
            item is HistoryGroupItem g ? GroupIsStructural(g) :
            item is IStructuralHistoryItem
                or CanvasResizeItem
                or LayerAddItem
                or LayerRemoveItem
                or LayerReorderItem
                or LayerTreeAddItem
                or LayerMergeDownItem
                or LayerMoveToFolderItem
                or FolderAddItem
                or FolderReorderItem
                or FlattenFolderItem;

        /// <summary>
        /// Undoes the last operation from the unified history stack.
        /// </summary>
        public void Undo()
        {
            if (!Document.History.CanUndo) return;
            var step = StepHistory(undo: true);
            RefreshAfterHistory(step);
        }

        /// <summary>
        /// Redoes the last undone operation from the unified history stack.
        /// </summary>
        public void Redo()
        {
            if (!Document.History.CanRedo) return;
            var step = StepHistory(undo: false);
            RefreshAfterHistory(step);
        }

        /// <summary>
        /// Moves the history cursor to <paramref name="appliedCount"/> (0 = start), stepping
        /// through every item in between but refreshing the view only once at the end.
        /// </summary>
        public void JumpHistoryTo(int appliedCount)
        {
            appliedCount = Math.Clamp(appliedCount, 0, Document.History.TotalCount);
            if (appliedCount == Document.History.AppliedCount) return;

            // One crossfade capture for the whole jump; StepHistory would otherwise take one per step.
            Document.RaiseBeforeStructureChanged();

            var merged = new HistoryStepResult();
            while (Document.History.AppliedCount > appliedCount)
                merged.MergeWith(StepHistory(undo: true, captureBefore: false));
            while (Document.History.AppliedCount < appliedCount)
                merged.MergeWith(StepHistory(undo: false, captureBefore: false));

            RefreshAfterHistory(merged);
        }

        /// <summary>
        /// What a history step changed, so the view can be refreshed proportionately.
        /// </summary>
        private struct HistoryStepResult
        {
            /// <summary>Canvas size or layer tree may have changed.</summary>
            public bool Structural;
            /// <summary>Layer pixels may have changed (needs a recomposite).</summary>
            public bool Pixels;
            /// <summary>The step already fully refreshed the view itself.</summary>
            public bool AlreadyRefreshed;

            public void MergeWith(HistoryStepResult other)
            {
                Structural |= other.Structural;
                Pixels |= other.Pixels;
                // A step that refreshed itself does not excuse the batch from refreshing:
                // later steps may have changed more, so only keep the flag for a lone step.
                AlreadyRefreshed = false;
            }
        }

        /// <summary>
        /// Performs exactly one undo or redo without touching the view. Selection items apply to
        /// the document themselves and raise SelectionChanged, which the view observes.
        /// </summary>
        private HistoryStepResult StepHistory(bool undo, bool captureBefore = true)
        {
            var item = undo ? Document.History.PeekUndo() : Document.History.PeekRedo();
            bool isStructural = IsStructuralItem(item);
            // Mask-only and transform-only items never touch layer pixels; everything else may.
            bool pixels = item is not (SelectionChangeItem or SelectionTransformItem or IViewOnlyHistoryItem);

            // Items no longer capture the crossfade themselves (it was a full composite per item);
            // the view captures once per step, and only for steps that change the layer tree.
            if (captureBefore && isStructural)
                Document.RaiseBeforeStructureChanged();

            if (undo) Document.History.Undo();
            else Document.History.Redo();

            return new HistoryStepResult { Structural = isStructural, Pixels = pixels };
        }

        /// <summary>
        /// Brings the view in line with the document after one or more history steps.
        /// </summary>
        private void RefreshAfterHistory(HistoryStepResult step)
        {
            if (step.AlreadyRefreshed)
                return;

            if (step.Structural)
            {
                _zoom.SetDocSize(Document.PixelWidth, Document.PixelHeight);
                _selRegion.EnsureSize(Document.PixelWidth, Document.PixelHeight);
                EnsureComposite();
                ResetStrokeForActive();
            }

            if (step.Pixels)
            {
                Document.CompositeTo(Document.Surface);
                UpdateActiveLayerPreview();
            }

            InvalidateMainCanvas();
            HistoryStateChanged?.Invoke();
            Document.RaiseDocumentModified(); // Notify animation panels

            if (step.Pixels)
                RaiseFrame();
        }
    }
}
