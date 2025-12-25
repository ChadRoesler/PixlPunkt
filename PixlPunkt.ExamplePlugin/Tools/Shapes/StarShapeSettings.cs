using FluentIcons.Common;
using PixlPunkt.PluginSdk.Settings;
using PixlPunkt.PluginSdk.Settings.Options;

namespace PixlPunkt.ExamplePlugin.Tools.Shapes
{
    /// <summary>
    /// Settings for the Star shape tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This demonstrates creating custom settings for a shape tool with configurable options:
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Shape tools automatically receive standard brush-like options
    /// (Brush shape, Stroke width, Opacity, Density, Filled) from the host application.
    /// This settings class only needs to provide shape-specific options.
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Point Count</strong>: Number of star points (3-12)</item>
    /// <item><strong>Inner Radius</strong>: Ratio of inner to outer radius for star depth</item>
    /// </list>
    /// </remarks>
    public sealed class StarShapeSettings : ToolSettingsBase
    {
        private int _pointCount = 5;
        private double _innerRadiusRatio = 0.4;

        public override Icon Icon => Icon.Star;

        /// <inheritdoc/>
        public override string DisplayName => "Star";

        /// <inheritdoc/>
        public override string Description => "Draws star shapes with configurable points.";

        /// <summary>
        /// Gets or sets the number of star points.
        /// </summary>
        public int PointCount
        {
            get => _pointCount;
            set
            {
                int clamped = Math.Clamp(value, 3, 12);
                if (_pointCount != clamped)
                {
                    _pointCount = clamped;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the inner radius ratio (0.1 to 0.9).
        /// Controls how "deep" the star points are.
        /// </summary>
        public double InnerRadiusRatio
        {
            get => _innerRadiusRatio;
            set
            {
                double clamped = Math.Clamp(value, 0.1, 0.9);
                if (Math.Abs(_innerRadiusRatio - clamped) > 0.001)
                {
                    _innerRadiusRatio = clamped;
                    RaiseChanged();
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// These options appear after the standard shape options (Brush, Stroke, Opacity, Density, Filled)
        /// which are automatically provided by the host for all shape tools.
        /// </remarks>
        public override IEnumerable<IToolOption> GetOptions()
        {
            // Star-specific options - these appear after the standard shape options
            // The host adds: Brush (0), Stroke (1), Opacity (2), Density (3), Filled (5)
            // So we start at order 100 to ensure these come after

            yield return new SliderOption(
                "pointCount",
                "Points",
                3, 12, _pointCount,
                v => PointCount = (int)v,
                Order: 100,
                Step: 1,
                Tooltip: "Number of star points (3-12)"
            );

            yield return new SliderOption(
                "innerRadius",
                "Inner Radius",
                0.1, 0.9, _innerRadiusRatio,
                v => InnerRadiusRatio = v,
                Order: 101,
                Step: 0.05,
                Tooltip: "Ratio of inner to outer radius (smaller = deeper points)"
            );
        }
    }
}
