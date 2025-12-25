using PixlPunkt.PluginSdk.Imaging;

namespace PixlPunkt.PluginSdk.Settings
{
    /// <summary>
    /// Interface for tool settings that support stroke operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IStrokeSettings"/> defines the minimal properties required for all
    /// stroke-based tools. This is the base interface that all brush-like tools implement.
    /// </para>
    /// <para>
    /// Tools may additionally implement:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="IOpacitySettings"/>: For tools supporting variable opacity</item>
    /// <item><see cref="IDensitySettings"/>: For tools supporting soft edge falloff</item>
    /// <item><see cref="ICustomBrushSettings"/>: For tools supporting custom brush shapes</item>
    /// </list>
    /// </remarks>
    public interface IStrokeSettings
    {
        /// <summary>
        /// Gets the stroke size (diameter or side length depending on shape).
        /// </summary>
        /// <remarks>
        /// Size is typically in the range [1, 128] but implementations may
        /// support larger values. A size of 1 represents a single pixel.
        /// </remarks>
        int Size { get; }

        /// <summary>
        /// Gets the stroke shape (circle, square, or custom).
        /// </summary>
        /// <remarks>
        /// The shape determines how the brush mask is computed and how
        /// distance calculations are performed for density falloff.
        /// </remarks>
        BrushShape Shape { get; }
    }

    /// <summary>
    /// Interface for tool settings that support variable opacity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IOpacitySettings"/> is an optional extension to <see cref="IStrokeSettings"/>
    /// for tools that support user-controlled opacity during painting.
    /// </para>
    /// <para>
    /// Tools that don't implement this interface are assumed to use full opacity (255).
    /// </para>
    /// </remarks>
    public interface IOpacitySettings
    {
        /// <summary>
        /// Gets the opacity (0-255).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Opacity controls the maximum alpha applied during stamping.
        /// A value of 255 means fully opaque; 0 means fully transparent.
        /// </para>
        /// </remarks>
        byte Opacity { get; }
    }

    /// <summary>
    /// Interface for tool settings that support density-based soft edge falloff.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IDensitySettings"/> is an optional extension to <see cref="IStrokeSettings"/>
    /// for tools that support soft edges via density-based falloff.
    /// </para>
    /// <para>
    /// Tools that don't implement this interface are assumed to use hard edges (density 255).
    /// </para>
    /// </remarks>
    public interface IDensitySettings
    {
        /// <summary>
        /// Gets the density controlling the hard-edge to soft-edge ratio (0-255).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Density controls the proportion of the brush radius that is fully opaque
        /// versus the soft falloff region.
        /// </para>
        /// <list type="bullet">
        /// <item>255 = Fully hard edge (no falloff)</item>
        /// <item>128 = Half hard, half soft falloff</item>
        /// <item>0 = Completely soft (maximum falloff from center)</item>
        /// </list>
        /// </remarks>
        byte Density { get; }
    }

    /// <summary>
    /// Interface for tool settings that support custom brush shapes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ICustomBrushSettings"/> is an optional extension to <see cref="IStrokeSettings"/>
    /// for tools that support loading custom brush shapes from .mrk files.
    /// </para>
    /// </remarks>
    public interface ICustomBrushSettings
    {
        /// <summary>
        /// Gets the full name of the selected custom brush (author.brushname), or null if using built-in shape.
        /// </summary>
        string? CustomBrushFullName { get; }

        /// <summary>
        /// Gets whether a custom brush is currently selected.
        /// </summary>
        bool IsCustomBrushSelected { get; }
    }

    /// <summary>
    /// Interface for tool settings with brush-like behavior.
    /// </summary>
    /// <remarks>
    /// Combines stroke, opacity, and density settings for full brush configuration.
    /// </remarks>
    public interface IBrushLikeSettings : IStrokeSettings, IOpacitySettings, IDensitySettings
    {
    }
}
