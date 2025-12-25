using FluentIcons.Common;
using PixlPunkt.Core.Tools.Settings;
using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.Core.Tools.Settings
{
    /// <summary>
    /// Settings for the Tile Animation tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Tile Animation tool allows users to paint frame selections across tiles.
    /// Click and drag to select a range of tiles in row-major order for animation frames.
    /// </para>
    /// </remarks>
    public sealed class TileAnimationToolSettings : ToolSettingsBase
    {
        /// <inheritdoc/>
        public override KeyBinding? Shortcut => new(VirtualKey.A, Shift: true);

        private bool _addToExisting = false;

        public override Icon Icon => Icon.TableCellEdit;

        /// <summary>
        /// Gets or sets whether to add tiles to existing frames (vs replacing).
        /// </summary>
        public bool AddToExisting
        {
            get => _addToExisting;
            set
            {
                if (_addToExisting == value) return;
                _addToExisting = value;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Sets whether to add tiles to existing frames.
        /// </summary>
        public void SetAddToExisting(bool value)
        {
            AddToExisting = value;
        }
    }
}
