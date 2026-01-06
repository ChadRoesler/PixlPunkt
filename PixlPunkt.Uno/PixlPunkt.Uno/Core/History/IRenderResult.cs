using FluentIcons.Common;

namespace PixlPunkt.Uno.Core.History
{
    /// <summary>
    /// Marker interface for results returned from tool rendering operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IRenderResult"/> provides a common abstraction for all tool operation outputs,
    /// enabling plugin extensibility while maintaining type safety. Built-in implementations:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="PixelChangeItem"/>: Tracks individual pixel changes with delta compression</item>
    /// </list>
    /// <para>
    /// Plugin authors can implement custom result types for specialized operations
    /// (e.g., vector path results, procedural generation metadata).
    /// </para>
    /// <para><strong>Integration with History:</strong></para>
    /// <para>
    /// Results that should participate in undo/redo must also implement <see cref="IHistoryItem"/>.
    /// The <see cref="CanPushToHistory"/> property indicates whether this result should be recorded.
    /// </para>
    /// </remarks>
    public interface IRenderResult
    {
        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        Icon HistoryIcon { get; set; }

        /// <summary>
        /// Gets whether this result represents actual changes.
        /// </summary>
        /// <value>
        /// <c>true</c> if the operation made modifications; <c>false</c> if no changes occurred
        /// (e.g., painting on a locked layer, clicking outside bounds).
        /// </value>
        bool HasChanges { get; }

        /// <summary>
        /// Gets whether this result should be pushed to the undo/redo history.
        /// </summary>
        /// <value>
        /// <c>true</c> if this result implements <see cref="IHistoryItem"/> and should be recorded;
        /// <c>false</c> for transient or preview-only results.
        /// </value>
        bool CanPushToHistory { get; }

        /// <summary>
        /// Gets a human-readable description of the operation.
        /// </summary>
        /// <value>
        /// A string suitable for display in undo/redo UI (e.g., "Brush Stroke", "Fill Rectangle").
        /// </value>
        string Description { get; }
    }
}
