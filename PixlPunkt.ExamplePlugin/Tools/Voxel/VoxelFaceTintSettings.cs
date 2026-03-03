using FluentIcons.Common;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.ExamplePlugin.Tools.Voxel
{
    /// <summary>Simple example settings for a voxel face tint tool.</summary>
    public sealed class VoxelFaceTintSettings : ToolSettingsBase
    {
        private bool _sampleInsteadOfPaint;
        private bool _useBackgroundColor;

        public override Icon Icon => Icon.PaintBrush;
        public override string DisplayName => "Voxel Face Tint";
        public override string Description => "Paints or samples a single voxel face in the voxel workspace.";

        public bool SampleInsteadOfPaint
        {
            get => _sampleInsteadOfPaint;
            set
            {
                if (_sampleInsteadOfPaint == value) return;
                _sampleInsteadOfPaint = value;
                RaiseChanged();
            }
        }

        public bool UseBackgroundColor
        {
            get => _useBackgroundColor;
            set
            {
                if (_useBackgroundColor == value) return;
                _useBackgroundColor = value;
                RaiseChanged();
            }
        }

        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ToggleOption(
                "sample",
                "Sample Instead of Paint",
                _sampleInsteadOfPaint,
                v => SampleInsteadOfPaint = v,
                Order: 0,
                Tooltip: "When enabled, clicking samples the face color into the palette instead of painting.");

            yield return new ToggleOption(
                "useBg",
                "Use Background Color",
                _useBackgroundColor,
                v => UseBackgroundColor = v,
                Order: 1,
                Tooltip: "Paint using the current background color instead of foreground.");
        }
    }
}
