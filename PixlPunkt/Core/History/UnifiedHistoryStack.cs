using System;
using System.Collections.Generic;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// Unified history stack managing all undoable operations for a document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UnifiedHistoryStack provides a single point of truth for all history operations,
    /// handling both pixel-level changes (<see cref="PixelChangeItem"/>) and structural
    /// changes (<see cref="CanvasResizeItem"/>) through the <see cref="IHistoryItem"/> interface.
    /// </para>
    /// <para>
    /// Benefits of unified history:
    /// - Single undo/redo stack with correct ordering across all operation types
    /// - Simpler serialization if history needs to be saved
    /// - Clear UI state (CanUndo/CanRedo) without checking multiple stacks
    /// - Descriptions available for "Undo: X" menu items
    /// - Dirty tracking via <see cref="IsDirty"/> for unsaved changes detection
    /// </para>
    /// </remarks>
    public sealed class UnifiedHistoryStack
    {
        private readonly Stack<IHistoryItem> _undo = new();
        private readonly Stack<IHistoryItem> _redo = new();

        private bool _suppressChanged;

        /// <summary>
        /// Tracks the undo stack count at the last save point.
        /// -1 indicates the document has never been saved (always dirty if any changes exist).
        /// </summary>
        private int _savedUndoCount = 0;

        /// <summary>
        /// Gets a value indicating whether undo is available.
        /// </summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>
        /// Gets a value indicating whether redo is available.
        /// </summary>
        public bool CanRedo => _redo.Count > 0;

        /// <summary>
        /// Gets the description of the next action to undo, or null if none available.
        /// </summary>
        public string? UndoDescription => CanUndo ? _undo.Peek().Description : null;

        /// <summary>
        /// Gets the description of the next action to redo, or null if none available.
        /// </summary>
        public string? RedoDescription => CanRedo ? _redo.Peek().Description : null;

        /// <summary>
        /// Gets the number of items in the undo stack.
        /// </summary>
        public int UndoCount => _undo.Count;

        /// <summary>
        /// Gets the number of items in the redo stack.
        /// </summary>
        public int RedoCount => _redo.Count;

        public int AppliedCount => _undo.Count;

        public int TotalCount => _undo.Count + _redo.Count;


        /// <summary>
        /// Gets a value indicating whether the document has unsaved changes.
        /// </summary>
        /// <remarks>
        /// The document is considered dirty if the current undo stack count differs from
        /// the count at the last save point. This correctly handles undo/redo operations
        /// that might return the document to a saved state.
        /// </remarks>
        public bool IsDirty =>
            _savedUndoCount < 0
                ? _undo.Count > 0
                : _undo.Count != _savedUndoCount;

        /// <summary>
        /// Fired when the history state changes (after push, undo, or redo).
        /// </summary>
        public event Action? HistoryChanged;

        private void RaiseChanged()
        {
            if (!_suppressChanged)
                HistoryChanged?.Invoke();
        }

        /// <summary>
        /// Pushes a new history item onto the undo stack.
        /// </summary>
        /// <param name="item">The history item to push.</param>
        /// <remarks>
        /// Pushing a new item clears the redo stack, as is standard for undo/redo systems.
        /// Empty pixel change items are ignored.
        /// </remarks>
        public void Push(IHistoryItem item)
        {
            if (item == null) return;

            // Skip empty pixel changes
            if (item is PixelChangeItem pci && pci.IsEmpty) return;

            _undo.Push(item);
            _redo.Clear();
            LoggingService.Info("History pushed item={Item} undoCount={UndoCount}", item.Description, _undo.Count);
            RaiseChanged();
        }




        public bool Undo() => UndoInternal(raise: true);
        public bool Redo() => RedoInternal(raise: true);



        /// <summary>
        /// Undoes the most recent operation.
        /// </summary>
        /// <returns>True if an operation was undone; false if nothing to undo.</returns>
        public bool UndoInternal(bool raise)
        {
            if (!CanUndo) return false;

            var item = _undo.Pop();
            item.Undo();
            _redo.Push(item);
            LoggingService.Info("History undo item={Item} undoCount={UndoCount} redoCount={RedoCount}", item.Description, _undo.Count, _redo.Count);
            if (raise) RaiseChanged();
            return true;
        }

        /// <summary>
        /// Redoes the most recently undone operation.
        /// </summary>
        /// <returns>True if an operation was redone; false if nothing to redo.</returns>
        public bool RedoInternal(bool raise)
        {
            if (!CanRedo) return false;

            var item = _redo.Pop();
            item.Redo();
            _undo.Push(item);
            LoggingService.Info("History redo item={Item} undoCount={UndoCount} redoCount={RedoCount}", item.Description, _undo.Count, _redo.Count);
            if (raise) RaiseChanged();
            return true;
        }

        /// <summary>
        /// Clears all undo and redo history.
        /// </summary>
        public void Clear(bool resetSaveState = true)
        {
            _undo.Clear();
            _redo.Clear();
            LoggingService.Info("History cleared");
            if (resetSaveState) _savedUndoCount = 0;
            RaiseChanged();
        }

        /// <summary>
        /// Marks the current history state as saved.
        /// </summary>
        /// <remarks>
        /// Call this after a successful save operation. The <see cref="IsDirty"/> property
        /// will return false until further changes are made or undo/redo moves away from
        /// this save point.
        /// </remarks>
        public void MarkSaved()
        {
            _savedUndoCount = _undo.Count;
            LoggingService.Info("History marked saved at undoCount={UndoCount}", _savedUndoCount);
            RaiseChanged();
        }

        /// <summary>
        /// Peeks at the next item to be undone without removing it.
        /// </summary>
        /// <returns>The next undo item, or null if stack is empty.</returns>
        public IHistoryItem? PeekUndo() => CanUndo ? _undo.Peek() : null;

        /// <summary>
        /// Peeks at the next item to be redone without removing it.
        /// </summary>
        /// <returns>The next redo item, or null if stack is empty.</returns>
        public IHistoryItem? PeekRedo() => CanRedo ? _redo.Peek() : null;

        /// <summary>
        /// Timeline in chronological order:
        /// [oldest applied ... newest applied] + [next redo ... last redo]
        /// </summary>
        public IReadOnlyList<IHistoryItem> GetTimeline()
        {
            var undoNewestToOldest = _undo.ToArray(); // newest -> oldest
            Array.Reverse(undoNewestToOldest);        // oldest -> newest

            var redoNextToLast = _redo.ToArray();     // next redo -> last redo (already correct order)

            var list = new List<IHistoryItem>(undoNewestToOldest.Length + redoNextToLast.Length);
            list.AddRange(undoNewestToOldest);
            list.AddRange(redoNextToLast);
            return list;
        }

        /// <summary>
        /// Jump the history cursor to an "appliedCount" (0..TotalCount).
        /// 0 = Start, AppliedCount = UndoCount.
        /// </summary>
        public void JumpTo(int appliedCount)
        {
            appliedCount = Math.Clamp(appliedCount, 0, TotalCount);
            if (appliedCount == AppliedCount) return;

            _suppressChanged = true;
            try
            {
                // Need fewer applied ops => undo
                while (AppliedCount > appliedCount)
                    UndoInternal(raise: false);

                // Need more applied ops => redo
                while (AppliedCount < appliedCount)
                    RedoInternal(raise: false);
            }
            finally
            {
                _suppressChanged = false;
            }

            RaiseChanged();
        }
    }
}
