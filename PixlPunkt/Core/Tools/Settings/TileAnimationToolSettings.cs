using System.Collections.Generic;
using FluentIcons.Common;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Selection order for tile animation frame creation.
    /// </summary>
    public enum TileSelectionOrder
    {
        /// <summary>Frames are ordered left-to-right, then top-to-bottom.</summary>
        RowMajor,

        /// <summary>Frames are ordered top-to-bottom, then left-to-right.</summary>
        ColumnMajor
    }

    /// <summary>
    /// Settings for the Tile Animation tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Tile Animation tool allows users to select tile positions on the canvas
    /// to create animation frames. Click and drag to select a rectangular range of tiles.
    /// </para>
    /// <para><strong>Actions:</strong></para>
    /// <list type="bullet">
    /// <item>LMB drag: Select tile positions for animation frames</item>
    /// <item>Shift + LMB: Add tiles to existing frames instead of replacing</item>
    /// <item>RMB: Sample tile (tile dropper)</item>
    /// </list>
    /// </remarks>
    public sealed class TileAnimationToolSettings : ToolSettingsBase
    {
        private bool _addToExisting = false;
        private TileSelectionOrder _selectionOrder = TileSelectionOrder.RowMajor;

        /// <inheritdoc/>
        public override Icon Icon => Icon.TableCellEdit;

        /// <inheritdoc/>
        public override string DisplayName => "Tile Animation";

        /// <inheritdoc/>
        public override string Description => "Select tiles to create animation frames";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.A, Shift: true);

        //////////////////////////////////////////////////////////////////
        // Tile Animation Properties
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets whether to add tiles to existing frames instead of replacing them.
        /// </summary>
        /// <remarks>
        /// When true, selected tiles are appended to the current reel's frames.
        /// When false, existing frames are cleared before adding new ones.
        /// Can also be toggled by holding Shift during selection.
        /// </remarks>
        public bool AddToExisting => _addToExisting;

        /// <summary>
        /// Gets the order in which tiles are added as frames.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><see cref="TileSelectionOrder.RowMajor"/>: Left-to-right, then top-to-bottom</item>
        /// <item><see cref="TileSelectionOrder.ColumnMajor"/>: Top-to-bottom, then left-to-right</item>
        /// </list>
        /// </remarks>
        public TileSelectionOrder SelectionOrder => _selectionOrder;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ToggleOption(
                "addToExisting",
                "Add to Existing",
                _addToExisting,
                SetAddToExisting,
                Order: 0,
                Tooltip: "Add frames to existing reel instead of replacing (or hold Shift)"
            );

            yield return new DropdownOption(
                "selectionOrder",
                "Frame Order",
                ["Row Major (??)", "Column Major (??)"],
                (int)_selectionOrder,
                SetSelectionOrderByIndex,
                Order: 1,
                Tooltip: "Order in which selected tiles become animation frames"
            );
        }

        //////////////////////////////////////////////////////////////////
        // Setters
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Sets whether to add tiles to existing frames.
        /// </summary>
        public void SetAddToExisting(bool value)
        {
            if (_addToExisting == value) return;
            _addToExisting = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the selection order for frame creation.
        /// </summary>
        public void SetSelectionOrder(TileSelectionOrder order)
        {
            if (_selectionOrder == order) return;
            _selectionOrder = order;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the selection order by dropdown index.
        /// </summary>
        private void SetSelectionOrderByIndex(int index)
        {
            SetSelectionOrder((TileSelectionOrder)index);
        }
    }
}
