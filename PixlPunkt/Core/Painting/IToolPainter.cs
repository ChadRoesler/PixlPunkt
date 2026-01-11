namespace PixlPunkt.Core.Painting
{
    /// <summary>
    /// Marker interface for all tool painter types in PixlPunkt's plugin architecture.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IToolPainter"/> serves as a unifying base for the three painting strategy interfaces:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="IStrokePainter"/>: Continuous stroke-based tools (Brush, Eraser, Blur, etc.)</item>
    /// <item><see cref="IFillPainter"/>: Click-to-fill tools (Flood Fill, Global Replace)</item>
    /// <item><see cref="IShapeRenderer"/>: Two-point shape tools (Rectangle, Ellipse)</item>
    /// </list>
    /// <para><strong>Plugin Discovery:</strong></para>
    /// <para>
    /// Plugin loaders can use this interface to discover all painter types from an assembly:
    /// </para>
    /// <code>
    /// var painters = assembly.GetTypes()
    ///     .Where(t => typeof(IToolPainter).IsAssignableFrom(t) &amp;&amp; !t.IsInterface);
    /// </code>
    /// <para><strong>Generic Handling:</strong></para>
    /// <para>
    /// UI components can work with painters generically when type-specific behavior isn't needed:
    /// </para>
    /// <code>
    /// void RegisterPainter(IToolPainter painter) { ... }
    /// </code>
    /// <para>
    /// This interface contains no members - it exists purely for type categorization and discovery.
    /// </para>
    /// </remarks>
    public interface IToolPainter
    {
        // Marker interface - no members
    }
}
