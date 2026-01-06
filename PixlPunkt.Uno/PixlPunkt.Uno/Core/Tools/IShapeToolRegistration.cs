using PixlPunkt.Uno.Core.Painting;

namespace PixlPunkt.Uno.Core.Tools
{
    /// <summary>
    /// Interface for shape tool registrations, enabling both built-in and plugin shape tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface allows the canvas host to work with shape tools without knowing
    /// whether they are built-in (<see cref="ShapeToolRegistration"/>) or from plugins
    /// (<see cref="Plugins.PluginShapeToolRegistration"/>).
    /// </para>
    /// </remarks>
    public interface IShapeToolRegistration : IToolRegistration
    {
        /// <summary>
        /// Gets the shape builder for geometry generation.
        /// </summary>
        IShapeBuilder? ShapeBuilder { get; }

        /// <summary>
        /// Gets whether this tool has a shape builder.
        /// </summary>
        bool HasShapeBuilder { get; }

        /// <summary>
        /// Gets whether this tool uses a stroke painter (e.g., Gradient).
        /// </summary>
        bool HasPainter { get; }

        /// <summary>
        /// Gets the shape renderer for this tool.
        /// </summary>
        IShapeRenderer EffectiveRenderer { get; }

        /// <summary>
        /// Creates a new painter instance for this tool (for line-based shapes like Gradient).
        /// </summary>
        /// <returns>A new painter instance, or null if this shape uses builder rendering.</returns>
        IStrokePainter? CreatePainter();
    }
}
