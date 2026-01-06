using System;
using System.Collections.Generic;
using System.Linq;
using PixlPunkt.Uno.Core.Logging;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// Manages memory usage for the history stack with automatic offloading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HistoryMemoryManager"/> wraps a <see cref="UnifiedHistoryStack"/> and monitors
    /// memory usage. When usage exceeds the configured limit, it offloads older history items
    /// to disk while preserving their metadata for the timeline UI.
    /// </para>
    /// <para>
    /// Key behaviors:
    /// </para>
    /// <list type="bullet">
    /// <item>Tracks estimated memory of all disposable history items</item>
    /// <item>Offloads oldest items first when memory limit exceeded</item>
    /// <item>Automatically reloads items when needed for undo/redo</item>
    /// <item>Preserves full timeline for timelapse export</item>
    /// </list>
    /// </remarks>
    public sealed class HistoryMemoryManager : IDisposable
    {
        private readonly UnifiedHistoryStack _stack;
        private readonly HistoryOffloadService _offloadService;
        private readonly List<WeakReference<IDisposableHistoryItem>> _trackedItems = new();

        /// <summary>
        /// Default memory limit: 256 MB
        /// </summary>
        public const long DefaultMemoryLimitBytes = 256 * 1024 * 1024;

        /// <summary>
        /// Minimum memory to keep in RAM (32 MB) - recent items always stay in memory
        /// </summary>
        public const long MinimumInMemoryBytes = 32 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the memory limit in bytes.
        /// </summary>
        public long MemoryLimitBytes { get; set; } = DefaultMemoryLimitBytes;

        /// <summary>
        /// Gets the current estimated memory usage in bytes.
        /// </summary>
        public long CurrentMemoryBytes { get; private set; }

        /// <summary>
        /// Gets the total bytes offloaded to disk.
        /// </summary>
        public long OffloadedBytes => _offloadService.TotalOffloadedBytes;

        /// <summary>
        /// Gets the number of items currently offloaded.
        /// </summary>
        public int OffloadedItemCount => _offloadService.OffloadedItemCount;

        /// <summary>
        /// Gets whether memory management is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Raised when an item is offloaded.
        /// </summary>
        public event Action<IDisposableHistoryItem>? ItemOffloaded;

        /// <summary>
        /// Raised when an item is reloaded.
        /// </summary>
        public event Action<IDisposableHistoryItem>? ItemReloaded;

        /// <summary>
        /// Creates a new history memory manager.
        /// </summary>
        /// <param name="stack">The history stack to manage.</param>
        /// <param name="documentId">Optional document ID for offload folder naming.</param>
        public HistoryMemoryManager(UnifiedHistoryStack stack, string? documentId = null)
        {
            _stack = stack ?? throw new ArgumentNullException(nameof(stack));
            _offloadService = new HistoryOffloadService(documentId);

            // Hook into history changes
            _stack.HistoryChanged += OnHistoryChanged;
        }

        /// <summary>
        /// Registers a history item for memory tracking.
        /// </summary>
        /// <param name="item">The item that was just pushed.</param>
        public void TrackItem(IHistoryItem item)
        {
            if (!IsEnabled || item is not IDisposableHistoryItem disposable)
                return;

            _trackedItems.Add(new WeakReference<IDisposableHistoryItem>(disposable));
            CurrentMemoryBytes += disposable.EstimatedMemoryBytes;

            // Clean up dead references periodically
            if (_trackedItems.Count % 50 == 0)
            {
                CleanupDeadReferences();
            }

            // Check if we need to offload
            if (CurrentMemoryBytes > MemoryLimitBytes)
            {
                TryOffloadOldItems();
            }
        }

        /// <summary>
        /// Ensures an item is loaded in memory before undo/redo.
        /// </summary>
        /// <param name="item">The item to ensure is loaded.</param>
        /// <returns>True if item is ready for use.</returns>
        public bool EnsureLoaded(IHistoryItem item)
        {
            if (item is not IDisposableHistoryItem disposable || !disposable.IsOffloaded)
                return true;

            var success = disposable.Reload(_offloadService);
            if (success)
            {
                CurrentMemoryBytes += disposable.EstimatedMemoryBytes;
                ItemReloaded?.Invoke(disposable);
                LoggingService.Debug("History item reloaded: {Desc} memUsage={Mem}MB",
                    disposable.Description, CurrentMemoryBytes / (1024 * 1024));
            }

            return success;
        }

        /// <summary>
        /// Gets the offload service for timelapse export access.
        /// </summary>
        public IHistoryOffloadService OffloadService => _offloadService;

        private void OnHistoryChanged()
        {
            // Recalculate memory usage after undo/redo/clear
            RecalculateMemory();
        }

        private void RecalculateMemory()
        {
            long total = 0;

            for (int i = _trackedItems.Count - 1; i >= 0; i--)
            {
                if (_trackedItems[i].TryGetTarget(out var item))
                {
                    if (!item.IsOffloaded)
                    {
                        total += item.EstimatedMemoryBytes;
                    }
                }
                else
                {
                    _trackedItems.RemoveAt(i);
                }
            }

            CurrentMemoryBytes = total;
        }

        private void TryOffloadOldItems()
        {
            if (!IsEnabled) return;

            // Get items sorted by age (oldest first) - we want to offload oldest items
            var candidates = new List<(int index, IDisposableHistoryItem item, long memory)>();

            for (int i = 0; i < _trackedItems.Count; i++)
            {
                if (_trackedItems[i].TryGetTarget(out var item) && !item.IsOffloaded)
                {
                    candidates.Add((i, item, item.EstimatedMemoryBytes));
                }
            }

            // Keep at minimum memory threshold worth of recent items
            long targetOffload = CurrentMemoryBytes - MemoryLimitBytes + MinimumInMemoryBytes;
            long offloaded = 0;

            // Skip the most recent items (keep them in memory for quick undo)
            int skipRecent = Math.Max(10, candidates.Count / 4);

            for (int i = 0; i < candidates.Count - skipRecent && offloaded < targetOffload; i++)
            {
                var (_, item, memory) = candidates[i];

                if (item.Offload(_offloadService))
                {
                    offloaded += memory;
                    CurrentMemoryBytes -= memory;
                    ItemOffloaded?.Invoke(item);

                    LoggingService.Debug("Offloaded history item: {Desc} saved={Saved}KB remaining={Rem}MB",
                        item.Description, memory / 1024, CurrentMemoryBytes / (1024 * 1024));
                }
            }

            if (offloaded > 0)
            {
                LoggingService.Info("History memory manager offloaded {Count} bytes, current usage: {Current}MB / {Limit}MB",
                    offloaded, CurrentMemoryBytes / (1024 * 1024), MemoryLimitBytes / (1024 * 1024));
            }
        }

        private void CleanupDeadReferences()
        {
            _trackedItems.RemoveAll(r => !r.TryGetTarget(out _));
        }

        /// <summary>
        /// Forces recalculation of memory and offloading check.
        /// </summary>
        public void CheckMemory()
        {
            RecalculateMemory();
            if (CurrentMemoryBytes > MemoryLimitBytes)
            {
                TryOffloadOldItems();
            }
        }

        /// <summary>
        /// Disposes of resources and cleans up offloaded files.
        /// </summary>
        public void Dispose()
        {
            _stack.HistoryChanged -= OnHistoryChanged;

            // Dispose any tracked items that support it
            foreach (var weakRef in _trackedItems)
            {
                if (weakRef.TryGetTarget(out var item))
                {
                    item.Dispose();
                }
            }
            _trackedItems.Clear();

            _offloadService.Dispose();
            CurrentMemoryBytes = 0;
        }
    }
}
