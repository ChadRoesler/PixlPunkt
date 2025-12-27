using System;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// Extended history item interface for items that can release memory resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IDisposableHistoryItem"/> extends <see cref="IHistoryItem"/> with memory management
    /// capabilities. History items that hold significant memory (pixel data, byte arrays) should
    /// implement this interface to participate in the memory-constrained history system.
    /// </para>
    /// <para>
    /// Key features:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="EstimatedMemoryBytes"/>: Reports approximate memory usage for tracking</item>
    /// <item><see cref="IsOffloaded"/>: Indicates if data has been offloaded to disk</item>
    /// <item><see cref="Offload"/>: Serializes heavy data to disk and releases memory</item>
    /// <item><see cref="Reload"/>: Restores data from disk when needed for undo/redo</item>
    /// </list>
    /// <para>
    /// The history memory manager uses these methods to keep memory usage within configured limits
    /// while preserving the full history timeline for timelapse export.
    /// </para>
    /// </remarks>
    public interface IDisposableHistoryItem : IHistoryItem, IDisposable
    {
        /// <summary>
        /// Gets the estimated memory usage of this history item in bytes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This should be a reasonable approximation including:
        /// - Byte arrays (pixel data, snapshots)
        /// - Lists of indices and values
        /// - Any other significant allocations
        /// </para>
        /// <para>
        /// For <see cref="PixelChangeItem"/>, this includes:
        /// - _indices list: Count * sizeof(int)
        /// - _before list: Count * sizeof(uint)
        /// - _after list: Count * sizeof(uint)
        /// - Object overhead (~40 bytes)
        /// </para>
        /// </remarks>
        long EstimatedMemoryBytes { get; }

        /// <summary>
        /// Gets whether this item's heavy data has been offloaded to disk.
        /// </summary>
        /// <remarks>
        /// When true, <see cref="Reload"/> must be called before <see cref="IHistoryItem.Undo"/>
        /// or <see cref="IHistoryItem.Redo"/> can execute properly.
        /// </remarks>
        bool IsOffloaded { get; }

        /// <summary>
        /// Gets the unique identifier for this history item's offloaded data.
        /// </summary>
        /// <remarks>
        /// This ID is used by the <see cref="HistoryOffloadService"/> to locate the
        /// serialized data on disk. Only valid when <see cref="IsOffloaded"/> is true.
        /// </remarks>
        Guid OffloadId { get; }

        /// <summary>
        /// Offloads heavy data to disk, releasing memory.
        /// </summary>
        /// <param name="offloadService">The service to write data to.</param>
        /// <returns>True if offload succeeded; false if item cannot be offloaded.</returns>
        /// <remarks>
        /// <para>
        /// After calling this method:
        /// - <see cref="IsOffloaded"/> should return true
        /// - <see cref="EstimatedMemoryBytes"/> should return a reduced value
        /// - Internal byte arrays/lists should be cleared or set to null
        /// </para>
        /// <para>
        /// Some items may not support offloading (e.g., those with references to live objects).
        /// In such cases, return false.
        /// </para>
        /// </remarks>
        bool Offload(IHistoryOffloadService offloadService);

        /// <summary>
        /// Reloads previously offloaded data from disk.
        /// </summary>
        /// <param name="offloadService">The service to read data from.</param>
        /// <returns>True if reload succeeded.</returns>
        /// <remarks>
        /// After calling this method:
        /// - <see cref="IsOffloaded"/> should return false
        /// - The item should be fully functional for undo/redo operations
        /// </remarks>
        bool Reload(IHistoryOffloadService offloadService);
    }

    /// <summary>
    /// Service interface for offloading and reloading history item data.
    /// </summary>
    /// <remarks>
    /// Implementations handle serialization of history data to temporary storage
    /// and retrieval when needed for undo/redo or timelapse export.
    /// </remarks>
    public interface IHistoryOffloadService
    {
        /// <summary>
        /// Writes offloaded data for a history item.
        /// </summary>
        /// <param name="id">Unique identifier for this item's data.</param>
        /// <param name="data">Serialized data to store.</param>
        void WriteData(Guid id, byte[] data);

        /// <summary>
        /// Reads offloaded data for a history item.
        /// </summary>
        /// <param name="id">The identifier of the data to read.</param>
        /// <returns>The serialized data, or null if not found.</returns>
        byte[]? ReadData(Guid id);

        /// <summary>
        /// Removes offloaded data when no longer needed.
        /// </summary>
        /// <param name="id">The identifier of the data to remove.</param>
        void RemoveData(Guid id);

        /// <summary>
        /// Gets the file path for timelapse export to read history data.
        /// </summary>
        /// <param name="id">The identifier of the data.</param>
        /// <returns>Path to the data file, or null if not available.</returns>
        string? GetDataPath(Guid id);
    }
}
