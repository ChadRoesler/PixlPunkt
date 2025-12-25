using System;
using FluentIcons.Common;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Selection;
using Windows.Graphics;

namespace PixlPunkt.Core.History
{
    /// <summary>
    /// History item for selection region changes (create, add, subtract, select all, invert).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Captures the before and after state of the selection region mask.
    /// Undo restores the previous selection; Redo re-applies the new selection.
    /// </para>
    /// <para>
    /// This only tracks the selection region itself, not floating selection pixel data
    /// or transforms (those are handled by <see cref="SelectionTransformItem"/>).
    /// </para>
    /// </remarks>
    public sealed class SelectionChangeItem : IHistoryItem
    {
        /// <summary>
        /// Describes the kind of selection change.
        /// </summary>
        public enum SelectionChangeKind
        {
            /// <summary>A new selection was created (replace mode).</summary>
            Create,
            /// <summary>Selection was added to (shift+select).</summary>
            Add,
            /// <summary>Selection was subtracted from (alt+select).</summary>
            Subtract,
            /// <summary>Entire document was selected.</summary>
            SelectAll,
            /// <summary>Selection was inverted.</summary>
            Invert,
            /// <summary>Selection was cleared/deselected.</summary>
            Clear
        }

        private readonly SelectionChangeKind _kind;
        private readonly SelectionRegion _beforeRegion;
        private readonly SelectionRegion _afterRegion;
        private readonly Action<SelectionRegion> _applyRegion;
        private readonly string _description;

        /// <summary>
        /// Gets a quick reference icon of the operation (for UI display).
        /// </summary>
        public Icon HistoryIcon { get; set; } = Icon.SelectAllOn;

        /// <summary>
        /// Gets a human-readable description of the action.
        /// </summary>
        public string Description => _description;

        /// <summary>
        /// Gets the kind of selection change.
        /// </summary>
        public SelectionChangeKind Kind => _kind;

        /// <summary>
        /// Gets whether this item represents an actual change (before != after).
        /// </summary>
        public bool HasChanges => !RegionsEqual(_beforeRegion, _afterRegion);

        /// <summary>
        /// Creates a new selection change history item.
        /// </summary>
        /// <param name="kind">The kind of selection change.</param>
        /// <param name="beforeRegion">Clone of the selection region before the change.</param>
        /// <param name="afterRegion">Clone of the selection region after the change.</param>
        /// <param name="applyRegion">Action to apply a region snapshot to the current selection.</param>
        public SelectionChangeItem(
            SelectionChangeKind kind,
            SelectionRegion beforeRegion,
            SelectionRegion afterRegion,
            Action<SelectionRegion> applyRegion)
        {
            _kind = kind;
            _beforeRegion = beforeRegion ?? throw new ArgumentNullException(nameof(beforeRegion));
            _afterRegion = afterRegion ?? throw new ArgumentNullException(nameof(afterRegion));
            _applyRegion = applyRegion ?? throw new ArgumentNullException(nameof(applyRegion));

            _description = kind switch
            {
                SelectionChangeKind.Create => "Create Selection",
                SelectionChangeKind.Add => "Add to Selection",
                SelectionChangeKind.Subtract => "Subtract from Selection",
                SelectionChangeKind.SelectAll => "Select All",
                SelectionChangeKind.Invert => "Invert Selection",
                SelectionChangeKind.Clear => "Clear Selection",
                _ => "Selection Change"
            };

            HistoryIcon = kind switch
            {
                SelectionChangeKind.Create => Icon.SelectAllOn,
                SelectionChangeKind.Add => Icon.AddSquareMultiple,
                SelectionChangeKind.Subtract => Icon.SubtractSquareMultiple,
                SelectionChangeKind.SelectAll => Icon.SelectAllOn,
                SelectionChangeKind.Invert => Icon.ArrowSwap,
                SelectionChangeKind.Clear => Icon.SelectAllOff,
                _ => Icon.SelectAllOn
            };
        }

        /// <summary>
        /// Undoes the selection change by restoring the before region.
        /// </summary>
        public void Undo()
        {
            _applyRegion(_beforeRegion);
            LoggingService.Info("Undo selection change kind={Kind}", _kind);
        }

        /// <summary>
        /// Redoes the selection change by applying the after region.
        /// </summary>
        public void Redo()
        {
            _applyRegion(_afterRegion);
            LoggingService.Info("Redo selection change kind={Kind}", _kind);
        }

        /// <summary>
        /// Compares two selection regions for equality.
        /// </summary>
        private static bool RegionsEqual(SelectionRegion a, SelectionRegion b)
        {
            if (a.IsEmpty && b.IsEmpty) return true;
            if (a.IsEmpty != b.IsEmpty) return false;

            var boundsA = a.Bounds;
            var boundsB = b.Bounds;

            // If bounds are different, regions are definitely different
            if (boundsA.X != boundsB.X || boundsA.Y != boundsB.Y ||
                boundsA.Width != boundsB.Width || boundsA.Height != boundsB.Height)
                return false;

            // Compare actual pixel content within the bounds
            // This is necessary because subtract operations may not change bounds
            // but will change the selected pixels within those bounds
            int x0 = boundsA.X;
            int y0 = boundsA.Y;
            int x1 = boundsA.X + boundsA.Width;
            int y1 = boundsA.Y + boundsA.Height;

            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    if (a.Contains(x, y) != b.Contains(x, y))
                        return false;
                }
            }

            return true;
        }
    }
}
