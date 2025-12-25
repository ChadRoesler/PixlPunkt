using FluentIcons.Common;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// Base interface for all history items in the unified history stack.
    /// </summary>
    /// <remarks>
    /// <para>
    /// IHistoryItem provides a polymorphic way to handle different types of undoable operations
    /// in a single history stack. This includes both pixel-level changes (painting, fills) and
    /// structural changes (canvas resize, layer operations).
    /// </para>
    /// <para>
    /// Each implementation must capture enough state to fully undo and redo the operation.
    /// The <see cref="Description"/> property enables UI features like "Undo: Resize Canvas".
    /// </para>
    /// </remarks>
    public interface IHistoryItem
    {
        /// <summary>
        /// Gets a quick reference icon of the opperation (for UI display).
        /// </summary>
        Icon HistoryIcon { get; set; }

        /// <summary>
        /// Gets a human-readable description of the operation (for UI display).
        /// </summary>
        /// <example>"Brush Stroke", "Resize Canvas (128×128 ? 256×256)", "Fill"</example>
        string Description { get; }

        /// <summary>
        /// Undoes the operation, restoring the previous state.
        /// </summary>
        void Undo();

        /// <summary>
        /// Redoes the operation, re-applying the change.
        /// </summary>
        void Redo();
    }
}
