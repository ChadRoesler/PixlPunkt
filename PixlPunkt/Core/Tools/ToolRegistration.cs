using System;
using PixlPunkt.Core.Painting;
using PixlPunkt.Core.Tools.Settings;

namespace PixlPunkt.Core.Tools
{
    /// <summary>
    /// Registration record for a brush/stroke-based tool, containing all information needed to instantiate and use the tool.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ToolRegistration"/> encapsulates:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Id</strong>: Unique string identifier (e.g., "pixlpunkt.brush.brush").</item>
    /// <item><strong>Category</strong>: Tool category for engine routing.</item>
    /// <item><strong>DisplayName</strong>: Human-readable name shown in UI.</item>
    /// <item><strong>Settings</strong>: The tool's configuration object (implements IStrokeSettings if stroke-based).</item>
    /// <item><strong>PainterFactory</strong>: Factory function to create the tool's painter.</item>
    /// </list>
    /// <para>
    /// Built-in tools are registered at application startup via <see cref="BuiltInBrushTools.RegisterAll"/>.
    /// Plugin tools can be registered dynamically through <see cref="IToolRegistry.Register"/>.
    /// </para>
    /// </remarks>
    /// <param name="Id">Unique string identifier following vendor.category.name convention.</param>
    /// <param name="Category">Tool category for engine routing.</param>
    /// <param name="DisplayName">Human-readable name for UI display.</param>
    /// <param name="Settings">The tool's settings object (may be null for tools without settings).</param>
    /// <param name="PainterFactory">
    /// Factory function that creates an <see cref="IStrokePainter"/> for this tool.
    /// Null for non-painting tools (e.g., Pan, Magnifier, Dropper).
    /// </param>
    public sealed partial record ToolRegistration(
        string Id,
        ToolCategory Category,
        string DisplayName,
        ToolSettingsBase? Settings,
        Func<IStrokePainter>? PainterFactory
    ) : IToolRegistration
    {

        /// <summary>
        /// Gets whether this tool has a painter (i.e., performs pixel operations).
        /// </summary>
        public bool HasPainter => PainterFactory != null;

        /// <summary>
        /// Gets whether this tool has stroke settings.
        /// </summary>
        public bool HasStrokeSettings => Settings is IStrokeSettings;

        /// <summary>
        /// Gets the settings as <see cref="IStrokeSettings"/> if applicable.
        /// </summary>
        public IStrokeSettings? StrokeSettings => Settings as IStrokeSettings;

        /// <summary>
        /// Creates a new painter instance for this tool.
        /// </summary>
        /// <returns>A new painter instance, or null if this tool doesn't have a painter.</returns>
        public IStrokePainter? CreatePainter() => PainterFactory?.Invoke();
    }
}
