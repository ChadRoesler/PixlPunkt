using FluentIcons.Common;
using PixlPunkt.PluginSdk.Imaging;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.ExamplePlugin.Tools
{
    /// <summary>
    /// Settings for the Sparkle Brush tool.
    /// </summary>
    public sealed class SparkleSettings : ToolSettingsBase, IBrushLikeSettings
    {
        public override Icon Icon => Icon.Sparkle;
        public override string DisplayName => "Sparkle Brush";
        public override string Description => "Paints with random sparkle particles";
        public override KeyBinding? Shortcut => null; // Plugin tools don't typically have shortcuts

        // IBrushLikeSettings implementation
        private int _size = 16;
        public int Size
        {
            get => _size;
            set { _size = Math.Clamp(value, 1, 128); RaiseChanged(); }
        }

        private BrushShape _shape = BrushShape.Circle;
        public BrushShape Shape
        {
            get => _shape;
            set { _shape = value; RaiseChanged(); }
        }

        private byte _opacity = 255;
        public byte Opacity
        {
            get => _opacity;
            set { _opacity = value; RaiseChanged(); }
        }

        private byte _density = 204; // ~80% of 255
        public byte Density
        {
            get => _density;
            set { _density = value; RaiseChanged(); }
        }

        // Sparkle-specific settings
        private int _sparkleCount = 5;
        public int SparkleCount
        {
            get => _sparkleCount;
            set { _sparkleCount = Math.Clamp(value, 1, 20); RaiseChanged(); }
        }

        private double _sparkleSpread = 0.5;
        public double SparkleSpread
        {
            get => _sparkleSpread;
            set { _sparkleSpread = Math.Clamp(value, 0.1, 2.0); RaiseChanged(); }
        }

        private bool _randomColors;
        public bool RandomColors
        {
            get => _randomColors;
            set { _randomColors = value; RaiseChanged(); }
        }

        public override IEnumerable<IToolOption> GetOptions()
        {
            yield return new ShapeOption(
                "shape", "Shape", Shape,
                v => Shape = v,
                Order: 0, Tooltip: "Brush shape");

            yield return new SliderOption(
                "size", "Size", 1, 128, Size,
                v => Size = (int)v,
                Order: 1, Tooltip: "Brush size in pixels");

            yield return new SliderOption(
                "opacity", "Opacity", 0, 255, Opacity,
                v => Opacity = (byte)v,
                Order: 2, Tooltip: "Brush opacity");

            yield return new SliderOption(
                "density", "Density", 0, 255, Density,
                v => Density = (byte)v,
                Order: 3, Tooltip: "Brush hardness/density");

            yield return new SeparatorOption(Order: 4);

            yield return new SliderOption(
                "sparkleCount", "Sparkles", 1, 20, SparkleCount,
                v => SparkleCount = (int)v,
                Order: 5, Tooltip: "Number of sparkle particles per stamp");

            yield return new SliderOption(
                "sparkleSpread", "Spread", 0.1, 2.0, SparkleSpread,
                v => SparkleSpread = v,
                Order: 6, Step: 0.1, Tooltip: "How far sparkles spread from center");

            yield return new ToggleOption(
                "randomColors", "Random Colors", RandomColors,
                v => RandomColors = v,
                Order: 7, Tooltip: "Use random colors instead of foreground color");
        }
    }
}
