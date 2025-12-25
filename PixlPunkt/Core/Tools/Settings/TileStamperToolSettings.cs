using System.Collections.Generic;
using FluentIcons.Common;
using VirtualKey = PixlPunkt.PluginSdk.Settings.VirtualKey;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Tile Stamper tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Tile Stamper places tiles on the canvas with optional mapping data.
    /// </para>
    /// <para><strong>Actions:</strong></para>
    /// <list type="bullet">
    /// <item>LMB: Place tile with mapping</item>
    /// <item>Shift + LMB: Place tile without mapping (when snap enabled)</item>
    /// <item>Ctrl + LMB: Create new tile from area under stamp</item>
    /// <item>RMB: Sample tile and mapping (tile dropper)</item>
    /// </list>
    /// </remarks>
    public sealed class TileStamperToolSettings : ToolSettingsBase
    {
        private bool _snapToGrid = true;
        private int _selectedTileId = -1;

        /// <inheritdoc/>
        public override Icon Icon => Icon.TableLightning;

        /// <inheritdoc/>
        public override string DisplayName => "Tile Stamper";

        /// <inheritdoc/>
        public override string Description => "Place and create tiles on the canvas";

        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.T, Shift: true);

        //////////////////////////////////////////////////////////////////
        // Tile Stamper Properties
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets whether tile placement snaps to the tile grid.
        /// </summary>
        /// <remarks>
        /// When true, tiles align to grid positions and mapping data is written.
        /// When false, tiles can be placed freely (stamper mode) without mapping.
        /// </remarks>
        public bool SnapToGrid => _snapToGrid;

        /// <summary>
        /// Gets the currently selected tile ID for stamping.
        /// </summary>
        /// <value>The tile ID, or -1 if no tile is selected.</value>
        public int SelectedTileId => _selectedTileId;

        /// <inheritdoc/>
        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ToggleOption(
                "snapToGrid",
                "Snap to Grid",
                _snapToGrid,
                SetSnapToGrid,
                Order: 0,
                Tooltip: "Align tiles to grid and write mapping data"
            );
        }

        /// <summary>
        /// Sets whether tile placement snaps to the tile grid.
        /// </summary>
        public void SetSnapToGrid(bool value)
        {
            if (_snapToGrid == value) return;
            _snapToGrid = value;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the selected tile ID for stamping.
        /// </summary>
        public void SetSelectedTileId(int tileId)
        {
            if (_selectedTileId == tileId) return;
            _selectedTileId = tileId;
            RaiseChanged();
        }
    }
}
