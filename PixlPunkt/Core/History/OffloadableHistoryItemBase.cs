using System;
using FluentIcons.Common;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// Base class for history items that hold sizeable payloads (pixel buffers, live layers)
    /// and therefore must take part in the history memory budget.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HistoryMemoryManager"/> only tracks items implementing
    /// <see cref="IDisposableHistoryItem"/>. Before this base existed only the two pixel-change
    /// items did, so canvas resizes, selection commits, removed layers and tile stamps - the
    /// heaviest items in the stack - were invisible to the 256 MB limit, never offloaded to
    /// disk and never released when discarded.
    /// </para>
    /// <para>
    /// Derived items report <see cref="PayloadBytes"/>. Items whose payload is plain bytes
    /// also override <see cref="SerializePayload"/>, <see cref="DeserializePayload"/> and
    /// <see cref="ReleasePayload"/> to become offloadable; items that hold live objects
    /// (a removed <c>RasterLayer</c>) leave <see cref="SerializePayload"/> returning
    /// <c>null</c> and are counted but kept in memory.
    /// </para>
    /// </remarks>
    public abstract class OffloadableHistoryItemBase : IDisposableHistoryItem
    {
        private readonly object _stateLock = new();
        private Guid _offloadId = Guid.Empty;
        private bool _isOffloaded;
        private long _offloadedBytes;

        /// <inheritdoc/>
        public abstract Icon HistoryIcon { get; set; }

        /// <inheritdoc/>
        public abstract string Description { get; }

        /// <inheritdoc/>
        public abstract void Undo();

        /// <inheritdoc/>
        public abstract void Redo();

        /// <summary>
        /// Bytes currently held in memory by this item's payload (buffers, layer surfaces).
        /// </summary>
        protected abstract long PayloadBytes { get; }

        /// <summary>
        /// Serializes the payload for offloading, or returns <c>null</c> if this item cannot
        /// be offloaded (its payload is a live object rather than bytes).
        /// </summary>
        protected virtual byte[]? SerializePayload() => null;

        /// <summary>Restores the payload from bytes produced by <see cref="SerializePayload"/>.</summary>
        protected virtual void DeserializePayload(byte[] data) { }

        /// <summary>Drops the in-memory payload after it has been offloaded or the item discarded.</summary>
        protected virtual void ReleasePayload() { }

        /// <inheritdoc/>
        public long EstimatedMemoryBytes => _isOffloaded ? 128 : PayloadBytes + 128;

        /// <inheritdoc/>
        public bool IsOffloaded => _isOffloaded;

        /// <inheritdoc/>
        public Guid OffloadId => _offloadId;

        /// <inheritdoc/>
        public bool Offload(IHistoryOffloadService offloadService)
        {
            lock (_stateLock)
            {
                if (_isOffloaded) return false;

                byte[]? data;
                try
                {
                    data = SerializePayload();
                }
                catch (Exception ex)
                {
                    LoggingService.Error($"Failed to serialize history item '{Description}' for offload", ex);
                    return false;
                }

                if (data == null || data.Length == 0)
                    return false;

                try
                {
                    _offloadId = Guid.NewGuid();
                    offloadService.WriteData(_offloadId, data);
                    _offloadedBytes = PayloadBytes;
                    ReleasePayload();
                    _isOffloaded = true;
                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.Error($"Failed to offload history item '{Description}'", ex);
                    _offloadId = Guid.Empty;
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        public bool Reload(IHistoryOffloadService offloadService)
        {
            lock (_stateLock)
            {
                if (!_isOffloaded || _offloadId == Guid.Empty)
                    return true;

                try
                {
                    var data = offloadService.ReadData(_offloadId);
                    if (data == null)
                    {
                        LoggingService.Error($"Failed to reload history item '{Description}': offloaded data not found");
                        return false;
                    }

                    DeserializePayload(data);
                    offloadService.RemoveData(_offloadId);
                    _offloadId = Guid.Empty;
                    _isOffloaded = false;
                    _offloadedBytes = 0;
                    return true;
                }
                catch (Exception ex)
                {
                    LoggingService.Error($"Failed to reload history item '{Description}'", ex);
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            lock (_stateLock)
            {
                ReleasePayload();
                _isOffloaded = false;
                _offloadId = Guid.Empty;
            }
        }
    }
}
