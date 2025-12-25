using FluentIcons.Common;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.ExamplePlugin.Tools.Tile
{
    /// <summary>
    /// Settings for the Tile Bucket Fill tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This tool is intentionally small and focused. It exists to demonstrate how to implement an
    /// <see cref="PixlPunkt.PluginSdk.Tile.ITileHandler"/> that edits the active layer's tile mapping
    /// via <see cref="PixlPunkt.PluginSdk.Tile.ITileContext"/>.
    /// </para>
    /// <para>
    /// Tile tools operate on a tile grid (TileCountX x TileCountY). This bucket fill performs a flood
    /// fill over the mapping grid, replacing a contiguous region of the clicked tile-id with the
    /// currently selected tile-id.
    /// </para>
    /// </remarks>
    public sealed class TileBucketFillSettings : ToolSettingsBase
    {
        // NOTE: Icon choice isn't important for the sample. Use any icon you like.
        public override Icon Icon => Icon.TableDismiss;

        public override string DisplayName => "Tile Bucket Fill";
        public override string Description => "Flood fills tile mappings using the selected tile.";
        public override KeyBinding? Shortcut => null;

        private bool _useDiagonalFill;
        /// <summary>
        /// If true, flood fill uses 8-way connectivity (includes diagonals). If false, 4-way.
        /// </summary>
        public bool UseDiagonalFill
        {
            get => _useDiagonalFill;
            set
            {
                if (_useDiagonalFill != value)
                {
                    _useDiagonalFill = value;
                    RaiseChanged();
                }
            }
        }

        private bool _fillEmptyOnly;
        /// <summary>
        /// If true, only fills tiles that are currently unmapped (-1).
        /// </summary>
        public bool FillEmptyOnly
        {
            get => _fillEmptyOnly;
            set
            {
                if (_fillEmptyOnly != value)
                {
                    _fillEmptyOnly = value;
                    RaiseChanged();
                }
            }
        }

        private bool _eraseMode;
        /// <summary>
        /// If true, the fill clears mappings instead of setting a tile id.
        /// </summary>
        public bool EraseMode
        {
            get => _eraseMode;
            set
            {
                if (_eraseMode != value)
                {
                    _eraseMode = value;
                    RaiseChanged();
                }
            }
        }

        private int _maxTiles = 50_000;
        /// <summary>
        /// Safety limit to prevent extremely large fills from freezing the UI.
        /// </summary>
        public int MaxTiles
        {
            get => _maxTiles;
            set
            {
                var clamped = Math.Clamp(value, 1, 1_000_000);
                if (_maxTiles != clamped)
                {
                    _maxTiles = clamped;
                    RaiseChanged();
                }
            }
        }

        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ToggleOption(
                "diagonal",
                "Diagonal Fill",
                _useDiagonalFill,
                v => UseDiagonalFill = v,
                Order: 0,
                Tooltip: "Use 8-way fill (includes diagonals)."
            );

            yield return new ToggleOption(
                "emptyOnly",
                "Fill Empty Only",
                _fillEmptyOnly,
                v => FillEmptyOnly = v,
                Order: 1,
                Tooltip: "Only fill unmapped tiles (-1)."
            );

            yield return new ToggleOption(
                "eraseMode",
                "Erase Mode",
                _eraseMode,
                v => EraseMode = v,
                Order: 2,
                Tooltip: "Clear mappings instead of filling with the selected tile."
            );

            yield return new SeparatorOption(Order: 3);

            yield return new SliderOption(
                "maxTiles",
                "Max Tiles",
                1,
                200_000,
                _maxTiles,
                v => MaxTiles = (int)v,
                Order: 4,
                Step: 1000,
                Tooltip: "Safety limit for fill size. Prevents giant fills from freezing the editor."
            );
        }
    }
}
