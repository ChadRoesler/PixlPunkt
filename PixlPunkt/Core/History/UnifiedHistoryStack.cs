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
    /// <para>
    /// Memory management is provided via <see cref="HistoryMemoryManager"/> which
    /// can automatically offload old history items to disk when memory limits are exceeded.
    /// </para>
    /// </remarks>
    public sealed class UnifiedHistoryStack : IDisposable
    {
        private readonly Stack<IHistoryItem> _undo = new();
        private readonly Stack<IHistoryItem> _redo = new();

        private bool _suppressChanged;
        private HistoryMemoryManager? _memoryManager;

        // An open group collects pushes until EndGroup; nesting depth lets callers compose.
        private HistoryGroupItem? _openGroup;
        private int _groupDepth;

        /// <summary>
        /// The item that sat on top of the undo stack at the last save point, and the depth it
        /// sat at. Depth alone is not enough: undoing and then making a new edit returns the
        /// stack to the same depth via a completely different item.
        /// </summary>
        private IHistoryItem? _savedItem;
        private int _savedDepth;

        /// <summary>
        /// False once the save point can no longer be returned to, which happens when a push
        /// discards a redo stack that still held it.
        /// </summary>
        private bool _savePointReachable = true;

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
        /// Gets the memory manager for this history stack, if enabled.
        /// </summary>
        public HistoryMemoryManager? MemoryManager => _memoryManager;

        /// <summary>
        /// Gets the current estimated memory usage of history in bytes.
        /// </summary>
        public long EstimatedMemoryBytes => _memoryManager?.CurrentMemoryBytes ?? 0;

        /// <summary>
        /// Gets the total bytes offloaded to disk.
        /// </summary>
        public long OffloadedBytes => _memoryManager?.OffloadedBytes ?? 0;

        /// <summary>
        /// Gets a value indicating whether the document has unsaved changes.
        /// </summary>
        /// <remarks>
        /// The document is clean only when the history cursor sits exactly on the save point.
        /// Comparing stack depth alone is not sufficient - saving, undoing once and then making
        /// a fresh edit returns the stack to the saved depth while holding entirely different
        /// work, so the identity of the item on top is checked too.
        /// </remarks>
        public bool IsDirty
        {
            get
            {
                if (!_savePointReachable) return true;
                var (top, depth) = ContentTop();
                return depth != _savedDepth || !ReferenceEquals(top, _savedItem);
            }
        }

        /// <summary>
        /// The newest item that changes document content, and how many such items are applied.
        /// View-only items (<see cref="IViewOnlyHistoryItem"/>) are skipped, so flipping the
        /// view after a save leaves the document clean.
        /// </summary>
        private (IHistoryItem? item, int depth) ContentTop()
        {
            IHistoryItem? top = null;
            int depth = 0;
            foreach (var it in _undo)               // Stack<T> enumerates newest first
            {
                if (it is IViewOnlyHistoryItem) continue;
                top ??= it;
                depth++;
            }
            return (top, depth);
        }

        /// <summary>
        /// Fired when the history state changes (after push, undo, or redo).
        /// </summary>
        public event Action? HistoryChanged;

        /// <summary>
        /// Creates a new unified history stack.
        /// </summary>
        public UnifiedHistoryStack()
        {
        }

        /// <summary>
        /// Enables memory management with the specified limit.
        /// </summary>
        /// <param name="memoryLimitBytes">Maximum memory to use before offloading. Default is 256MB.</param>
        /// <param name="documentId">Optional document ID for offload folder naming.</param>
        /// <returns>The memory manager instance.</returns>
        public HistoryMemoryManager EnableMemoryManagement(long memoryLimitBytes = HistoryMemoryManager.DefaultMemoryLimitBytes, string? documentId = null)
        {
            if (_memoryManager != null)
            {
                _memoryManager.MemoryLimitBytes = memoryLimitBytes;
                return _memoryManager;
            }

            _memoryManager = new HistoryMemoryManager(this, documentId)
            {
                MemoryLimitBytes = memoryLimitBytes
            };

            LoggingService.Info("History memory management enabled: limit={Limit}MB", memoryLimitBytes / (1024 * 1024));
            return _memoryManager;
        }

        /// <summary>
        /// Disables memory management.
        /// </summary>
        public void DisableMemoryManagement()
        {
            _memoryManager?.Dispose();
            _memoryManager = null;
        }

        private void RaiseChanged()
        {
            if (!_suppressChanged)
                HistoryChanged?.Invoke();
        }

        /// <summary>
        /// Starts a group: every push until the matching <see cref="EndGroup"/> becomes one undo
        /// step named <paramref name="description"/>. Groups nest; only the outermost is pushed.
        /// </summary>
        /// <param name="description">The name shown for the undo step.</param>
        public void BeginGroup(string description)
        {
            if (_groupDepth++ == 0)
                _openGroup = new HistoryGroupItem(description);
        }

        /// <summary>Closes the current group and pushes it (a single-item group pushes just that item).</summary>
        public void EndGroup()
        {
            if (_groupDepth == 0) return;
            if (--_groupDepth > 0) return;

            var group = _openGroup!;
            _openGroup = null;
            if (group.Count == 1) PushCore(group.Items[0]);
            else if (group.Count > 1) PushCore(group);
        }

        /// <summary>Abandons the current group, undoing whatever it had collected.</summary>
        public void CancelGroup()
        {
            if (_groupDepth == 0) return;
            _groupDepth = 0;
            var group = _openGroup;
            _openGroup = null;
            if (group == null) return;
            group.Undo();
            group.Dispose();
        }

        /// <summary>Whether a group is currently open.</summary>
        public bool IsGroupOpen => _groupDepth > 0;

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

            if (_openGroup != null)
            {
                _openGroup.Add(item);
                return;
            }

            // Consecutive items of a kind that coalesces (a run of nudges) collapse into the
            // previous one, so the user gets one undo step per gesture rather than per tick.
            if (_redo.Count == 0 && _undo.Count > 0 && _undo.Peek() is ICoalescingHistoryItem prev && prev.TryAbsorb(item))
            {
                RaiseChanged();
                return;
            }

            PushCore(item);
        }

        private void PushCore(IHistoryItem item)
        {
            // If the cursor is behind the save point, that point lives in the redo stack we are
            // about to discard, so it can never be returned to again.
            if (ContentTop().depth < _savedDepth)
                _savePointReachable = false;

            // Dispose redo items that support it
            foreach (var redoItem in _redo)
            {
                if (redoItem is IDisposableHistoryItem disposable)
                {
                    disposable.Dispose();
                }
            }

            _undo.Push(item);
            _redo.Clear();

            // Track memory usage
            _memoryManager?.TrackItem(item);

            LoggingService.Info("History pushed item={Item} undoCount={UndoCount} memUsage={Mem}MB",
                item.Description, _undo.Count, EstimatedMemoryBytes / (1024 * 1024));

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

            // Ensure item is loaded if it was offloaded
            _memoryManager?.EnsureLoaded(item);

            try
            {
                item.Undo();
            }
            catch
            {
                // Put it back where it was so the timeline stays intact; the caller sees the error.
                _undo.Push(item);
                throw;
            }
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

            // Ensure item is loaded if it was offloaded
            _memoryManager?.EnsureLoaded(item);

            try
            {
                item.Redo();
            }
            catch
            {
                _redo.Push(item);
                throw;
            }
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
            // Dispose all items that support it
            foreach (var item in _undo)
            {
                if (item is IDisposableHistoryItem disposable)
                    disposable.Dispose();
            }
            foreach (var item in _redo)
            {
                if (item is IDisposableHistoryItem disposable)
                    disposable.Dispose();
            }

            // Clearing discards the save point either way; preserve the dirty flag it implied.
            bool wasDirty = IsDirty;

            _undo.Clear();
            _redo.Clear();
            LoggingService.Info("History cleared");

            _savedItem = null;
            _savedDepth = 0;
            _savePointReachable = resetSaveState || !wasDirty;
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
            (_savedItem, _savedDepth) = ContentTop();
            _savePointReachable = true;
            LoggingService.Info("History marked saved at undoCount={UndoCount}", _savedDepth);
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

        /// <summary>
        /// Disposes of resources including the memory manager.
        /// </summary>
        public void Dispose()
        {
            Clear(resetSaveState: false);
            _memoryManager?.Dispose();
            _memoryManager = null;
        }
    }
}
